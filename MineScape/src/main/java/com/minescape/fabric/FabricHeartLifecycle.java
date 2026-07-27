package com.minescape.fabric;

import com.google.gson.JsonObject;
import com.google.gson.JsonParser;
import com.minescape.core.hearts.DeathConsumptionTransaction;
import com.minescape.core.hearts.CrystalUseEvent;
import com.minescape.core.hearts.CrystalConsumptionTransaction;
import com.minescape.core.hearts.DeathEvent;
import com.minescape.core.hearts.HeartState;
import com.minescape.core.hearts.HeartTransition;
import com.minescape.core.session.SessionMode;
import com.minescape.fabric.network.HeartSyncSender;
import net.fabricmc.fabric.api.entity.event.v1.ServerLivingEntityEvents;
import net.fabricmc.fabric.api.entity.event.v1.ServerPlayerEvents;
import net.fabricmc.fabric.api.event.lifecycle.v1.ServerTickEvents;
import net.minecraft.core.component.DataComponents;
import net.minecraft.network.chat.Component;
import net.minecraft.resources.Identifier;
import net.minecraft.server.MinecraftServer;
import net.minecraft.server.level.ServerPlayer;
import net.minecraft.sounds.SoundEvents;
import net.minecraft.stats.Stats;
import net.minecraft.world.damagesource.DamageSource;
import net.minecraft.world.damagesource.DamageTypes;
import net.minecraft.world.effect.MobEffectInstance;
import net.minecraft.world.effect.MobEffects;
import net.minecraft.world.entity.player.Player;
import net.minecraft.world.item.ItemStack;
import net.minecraft.world.item.Items;
import net.minecraft.world.item.Rarity;
import net.minecraft.world.level.storage.LevelResource;
import net.minecraft.world.scores.Objective;

import java.io.IOException;
import java.nio.channels.FileChannel;
import java.nio.file.Files;
import java.nio.file.Path;
import java.nio.file.StandardOpenOption;
import java.nio.charset.StandardCharsets;
import java.security.MessageDigest;
import java.security.NoSuchAlgorithmException;
import java.time.Instant;
import java.util.HexFormat;
import java.util.UUID;
import java.util.function.Supplier;
import java.util.logging.Level;
import java.util.logging.Logger;

/**
 * Exact Fabric-facing Mortal Hearts adapter for the pinned 26.2 API.
 *
 * <p>The frozen Matcha functions which normally change {@code Hearts} are neutralized by the
 * highest-priority MineScape compatibility pack. This adapter becomes the sole authority, while
 * mirroring usable health back to Matcha's scoreboard for compatibility.</p>
 */
final class FabricHeartLifecycle {
    private static final Logger LOGGER = Logger.getLogger("MineScape/MortalHearts");
    private static final Identifier CRYSTAL_MODEL = Identifier.withDefaultNamespace("heart_container");
    private static final Component CRYSTAL_NAME = Component.translatable("item.kleispack.crystal_heart");
    private static final String CRYSTAL_ID =
            "minecraft:poisonous_potato[minecraft:item_model=minecraft:heart_container]";
    private static final String MATCHA_1_02_SHA512 =
            "12dc5a600d473f06a44a4370c02b404a0260adf696a591a3fbb0f135bca613ff"
                    + "ba4f9990f625746375fa9181bbb300eeffd9cc420e6a50f91a48c21314c81505";
    private FabricHeartLifecycle() {
    }

    static void install(Supplier<MineScapeRuntime> runtimeSupplier) {
        ServerLivingEntityEvents.AFTER_DEATH.register((entity, source) -> {
            if (entity instanceof ServerPlayer player) {
                recordDeath(runtimeSupplier.get(), player, source);
            }
        });
        ServerPlayerEvents.JOIN.register(player -> playerReady(runtimeSupplier.get(), player));
        ServerPlayerEvents.AFTER_RESPAWN.register((oldPlayer, newPlayer, alive) ->
                playerReady(runtimeSupplier.get(), newPlayer));
        ServerTickEvents.END_SERVER_TICK.register(server -> consumeOneCrystalPerPlayer(runtimeSupplier.get(), server));
    }

    private static void recordDeath(MineScapeRuntime runtime, ServerPlayer player, DamageSource source) {
        if (!isHeartAuthorityActive(runtime)) {
            return;
        }
        try {
            HeartState before = runtime.hearts.get(player.getUUID());
            int deathSequence = currentDeathSequence(player);
            var completedSequence = runtime.hearts.completedDeathSequence(player.getUUID());
            if (completedSequence.isPresent() && completedSequence.getAsInt() == deathSequence) {
                return;
            }
            boolean creative = player.isCreative() || source.isCreativePlayer();
            boolean pvp = source.getEntity() instanceof Player
                    || source.is(DamageTypes.PLAYER_ATTACK)
                    || source.is(DamageTypes.PLAYER_EXPLOSION);
            boolean administrative = source.is(DamageTypes.GENERIC_KILL);
            DeathEvent event = new DeathEvent(
                    stableDeathEventId(player.getUUID(), before.generation(), deathSequence),
                    player.getUUID(), before.generation(), deathSequence, runtime.sessionMode,
                    DeathModeClassifier.isExactSurvival(player.gameMode()),
                    creative, pvp, administrative, Instant.now());
            runtime.hearts.beginDeathConsumption(player.getUUID(), event);
            saveForceAndVerifyDeathSequence(player.level().getServer(), player, deathSequence);
            HeartTransition transition = runtime.hearts.recordDeath(event);
            runtime.hearts.completeDeathConsumption(player.getUUID(), event.eventId(), deathSequence);
            if (transition.outcome() == HeartTransition.Outcome.DEATH_BLOCKED_SLOT
                    || transition.outcome() == HeartTransition.Outcome.RETIREMENT_PENDING) {
                HeartSyncSender.send(player, transition.after());
            }
            if (transition.outcome() == HeartTransition.Outcome.RETIREMENT_PENDING) {
                player.connection.disconnect(Component.literal(
                        "Every heart is now blocked. This character has died and is awaiting parent review in MineDeck."));
            }
        } catch (IOException | RuntimeException failure) {
            failClosed(player, "Mortal Hearts could not durably record this death.", failure);
        }
    }

    private static void playerReady(MineScapeRuntime runtime, ServerPlayer player) {
        if (!isHeartAuthorityActive(runtime)) {
            return;
        }
        try {
            reconcileDeath(runtime, player, player.level().getServer());
            if (runtime.hearts.pendingCrystalConsumption(player.getUUID()).isPresent()) {
                reconcileCrystal(runtime, player, player.level().getServer());
            }
            synchronizeOrLock(runtime, player);
        } catch (IOException | RuntimeException failure) {
            failClosed(player, "Mortal Hearts state could not be reconciled at join.", failure);
        }
    }

    private static void reconcileDeath(
            MineScapeRuntime runtime, ServerPlayer player, MinecraftServer server) throws IOException {
        var pending = runtime.hearts.pendingDeathConsumption(player.getUUID());
        int observed = currentDeathSequence(player);
        if (pending.isEmpty()) {
            runtime.hearts.reconcileDeathSequence(player.getUUID(), observed);
            return;
        }

        DeathConsumptionTransaction transaction = pending.orElseThrow();
        int expected = transaction.vanillaDeathSequence();
        if (observed == expected - 1) {
            player.getStats().setValue(player, Stats.CUSTOM.get(Stats.DEATHS), expected);
            observed = expected;
        }
        if (observed != expected) {
            throw new IllegalStateException("pending death receipt disagrees with vanilla death statistics");
        }
        saveForceAndVerifyDeathSequence(server, player, expected);
        HeartTransition transition = runtime.hearts.recordDeath(transaction.toEvent(player.getUUID()));
        if (transition.outcome() != HeartTransition.Outcome.DEATH_BLOCKED_SLOT
                && transition.outcome() != HeartTransition.Outcome.RETIREMENT_PENDING
                && transition.outcome() != HeartTransition.Outcome.IGNORED_CONTEXT
                && transition.outcome() != HeartTransition.Outcome.ALREADY_RETIRED
                && transition.outcome() != HeartTransition.Outcome.DUPLICATE_EVENT) {
            throw new IllegalStateException("pending death receipt reached an invalid outcome");
        }
        runtime.hearts.completeDeathConsumption(
                player.getUUID(), transaction.eventId(), transaction.vanillaDeathSequence());
    }

    private static int currentDeathSequence(ServerPlayer player) {
        return player.getStats().getValue(Stats.CUSTOM.get(Stats.DEATHS));
    }

    private static UUID stableDeathEventId(UUID playerId, int generation, int deathSequence) {
        String identity = "minescape:vanilla-death:v1:" + playerId + ":" + generation + ":" + deathSequence;
        return UUID.nameUUIDFromBytes(identity.getBytes(StandardCharsets.UTF_8));
    }

    /** Forces and parses the exact vanilla stats file before the durable death receipt can commit. */
    private static void saveForceAndVerifyDeathSequence(
            MinecraftServer server, ServerPlayer player, int expectedSequence) throws IOException {
        player.getStats().save();
        Path statsFile = server.getWorldPath(LevelResource.PLAYER_STATS_DIR)
                .resolve(player.getStringUUID() + ".json");
        if (!Files.isRegularFile(statsFile)) {
            throw new IOException("vanilla stats save did not produce the expected UUID file");
        }
        try (FileChannel channel = FileChannel.open(statsFile, StandardOpenOption.WRITE)) {
            channel.force(true);
        }
        JsonObject root;
        try {
            root = JsonParser.parseString(Files.readString(statsFile, StandardCharsets.UTF_8)).getAsJsonObject();
        } catch (RuntimeException malformed) {
            throw new IOException("vanilla stats file is malformed", malformed);
        }
        JsonObject stats = root.has("stats") && root.get("stats").isJsonObject()
                ? root.getAsJsonObject("stats") : null;
        JsonObject custom = stats != null && stats.has("minecraft:custom")
                && stats.get("minecraft:custom").isJsonObject()
                ? stats.getAsJsonObject("minecraft:custom") : null;
        if (custom == null || !custom.has("minecraft:deaths")
                || custom.get("minecraft:deaths").getAsInt() != expectedSequence) {
            throw new IOException("vanilla death sequence was not durably saved exactly");
        }
    }

    private static void synchronizeOrLock(MineScapeRuntime runtime, ServerPlayer player) {
        if (!isHeartAuthorityActive(runtime)) {
            return;
        }
        HeartState state = runtime.hearts.get(player.getUUID());
        if (state.retirementPending()) {
            player.connection.disconnect(Component.literal(
                    "This character has died. A parent must review the retirement in MineDeck before this account can rejoin."));
            return;
        }
        synchronize(runtime, player, state);
    }

    private static void synchronize(MineScapeRuntime runtime, ServerPlayer player, HeartState state) {
        var maximumHealth = player.getAttribute(net.minecraft.world.entity.ai.attributes.Attributes.MAX_HEALTH);
        if (maximumHealth == null) {
            throw new IllegalStateException("player is missing the maximum-health attribute");
        }
        double usableHealthPoints = Math.max(2.0D, state.usable() * 2.0D);
        maximumHealth.setBaseValue(usableHealthPoints);
        if (player.getHealth() > usableHealthPoints) {
            player.setHealth((float) usableHealthPoints);
        }

        var server = player.level().getServer();
        Objective matchaHearts = server.getScoreboard().getObjective("Hearts");
        if (matchaHearts != null) {
            server.getScoreboard().getOrCreatePlayerScore(player, matchaHearts)
                    .set(state.usable() * 2);
        }
        HeartSyncSender.send(player, state);
    }

    private static void consumeOneCrystalPerPlayer(MineScapeRuntime runtime, MinecraftServer server) {
        if (!isHeartAuthorityActive(runtime)) {
            return;
        }
        for (ServerPlayer player : server.getPlayerList().getPlayers()) {
            if (player.isDeadOrDying()) {
                continue;
            }
            reconcileCrystal(runtime, player, server);
        }
    }

    private static boolean isGenuineCrystal(ItemStack stack) {
        return !stack.isEmpty()
                && stack.getItem() == Items.POISONOUS_POTATO
                && CRYSTAL_MODEL.equals(stack.get(DataComponents.ITEM_MODEL))
                && CRYSTAL_NAME.equals(stack.get(DataComponents.ITEM_NAME))
                && stack.get(DataComponents.RARITY) == Rarity.RARE
                && Boolean.TRUE.equals(stack.get(DataComponents.ENCHANTMENT_GLINT_OVERRIDE))
                && stack.get(DataComponents.CONSUMABLE) == null;
    }

    private static void reconcileCrystal(
            MineScapeRuntime runtime, ServerPlayer player, MinecraftServer server) {
        try {
            HeartState before = runtime.hearts.get(player.getUUID());
            var pending = runtime.hearts.pendingCrystalConsumption(player.getUUID());
            int crystalCount = countGenuineCrystals(player);
            if (pending.isEmpty()) {
                if (before.retirementPending() || crystalCount == 0
                        || (before.capacity() == HeartState.MAXIMUM_CAPACITY && before.blocked() == 0)) {
                    return;
                }
                String playerDataBefore = saveForceAndHashPlayerData(server, player);
                pending = java.util.Optional.of(runtime.hearts.beginCrystalConsumption(
                        player.getUUID(), UUID.randomUUID(), before.generation(), crystalCount,
                        playerDataBefore));
            }

            CrystalConsumptionTransaction transaction = pending.orElseThrow();
            if (transaction.generation() != before.generation()
                    || (crystalCount != transaction.inventoryCountBefore()
                    && crystalCount != transaction.inventoryCountBefore() - 1)) {
                failClosed(player, "Crystal Heart recovery found inconsistent character or inventory state.", null);
                return;
            }

            CrystalUseEvent event = new CrystalUseEvent(
                    transaction.eventId(), player.getUUID(), transaction.generation(), CRYSTAL_ID,
                    MATCHA_1_02_SHA512, Instant.now());
            HeartTransition transition = runtime.hearts.consumeCrystal(event,
                    proof -> CRYSTAL_ID.equals(proof.itemIdentity())
                            && MATCHA_1_02_SHA512.equals(proof.sourceArchiveSha512()));
            if (transition.outcome() != HeartTransition.Outcome.CRYSTAL_RENEWED
                    && transition.outcome() != HeartTransition.Outcome.DUPLICATE_EVENT) {
                failClosed(player, "Crystal Heart recovery reached an invalid state transition.", null);
                return;
            }

            // The durable receipt precedes both mutations. Equal count means the item decrement
            // still needs to be saved; count-1 proves a prior synchronous player save completed.
            if (crystalCount == transaction.inventoryCountBefore()) {
                if (!removeOneGenuineCrystal(player)) {
                    failClosed(player, "Crystal Heart disappeared during its durable transaction.", null);
                    return;
                }
                player.getInventory().setChanged();
            }
            String playerDataAfter = saveForceAndHashPlayerData(server, player);
            if (playerDataAfter.equals(transaction.playerDataSha512Before())) {
                failClosed(player, "Crystal Heart inventory mutation was not durably written to playerdata.", null);
                return;
            }
            runtime.hearts.completeCrystalConsumption(player.getUUID(), transaction.eventId());
            HeartState after = runtime.hearts.get(player.getUUID());
            synchronize(runtime, player, after);
            player.addEffect(new MobEffectInstance(MobEffects.REGENERATION, 60, 10, false, false));
            player.playSound(SoundEvents.TOTEM_USE, 0.5F, 0.0F);
        } catch (IOException | RuntimeException failure) {
            // The durable receipt remains. On restart, total exact-Crystal count proves whether
            // the player save happened, so recovery neither grants nor consumes twice.
            failClosed(player, "Crystal Heart renewal paused for deterministic recovery.", failure);
        }
    }

    private static int countGenuineCrystals(ServerPlayer player) {
        int count = 0;
        for (int slot = 0; slot < player.getInventory().getContainerSize(); slot++) {
            ItemStack stack = player.getInventory().getItem(slot);
            if (isGenuineCrystal(stack)) {
                count += stack.getCount();
            }
        }
        return count;
    }

    private static boolean removeOneGenuineCrystal(ServerPlayer player) {
        for (int slot = 0; slot < player.getInventory().getContainerSize(); slot++) {
            ItemStack stack = player.getInventory().getItem(slot);
            if (isGenuineCrystal(stack)) {
                stack.shrink(1);
                return true;
            }
        }
        return false;
    }

    /**
     * Vanilla player saves catch their own I/O exceptions. Reading, hashing, and forcing the exact
     * resulting playerdata file gives the receipt a verifiable disk-side commit boundary.
     */
    private static String saveForceAndHashPlayerData(MinecraftServer server, ServerPlayer player)
            throws IOException {
        server.getPlayerList().saveAll();
        Path playerData = server.getWorldPath(LevelResource.PLAYER_DATA_DIR)
                .resolve(player.getStringUUID() + ".dat");
        if (!Files.isRegularFile(playerData)) {
            throw new IOException("vanilla playerdata save did not produce the expected UUID file");
        }
        try (FileChannel channel = FileChannel.open(playerData, StandardOpenOption.WRITE)) {
            channel.force(true);
        }
        try {
            return HexFormat.of().formatHex(
                    MessageDigest.getInstance("SHA-512").digest(Files.readAllBytes(playerData)));
        } catch (NoSuchAlgorithmException impossible) {
            throw new AssertionError("Java runtime has no SHA-512", impossible);
        }
    }

    private static void failClosed(ServerPlayer player, String message, Throwable failure) {
        if (failure == null) {
            LOGGER.severe(message + " Player=" + player.getUUID());
        } else {
            LOGGER.log(Level.SEVERE, message + " Player=" + player.getUUID(), failure);
        }
        player.connection.disconnect(Component.literal(
                message + " MineScape stopped before family play could continue."));
        player.level().getServer().halt(false);
    }

    private static boolean isHeartAuthorityActive(MineScapeRuntime runtime) {
        return runtime != null
                && (runtime.sessionMode == SessionMode.FAMILY || runtime.sessionMode == SessionMode.HEART_QA);
    }
}

# Local artifact cache

Only manifests, hashes, provenance, and generated reports belong in Git. `downloads/`, private runtimes, Minecraft/Fabric jars, mods, datapacks, resource packs, shaders, and derived Matcha assets remain local and are reconstructed through MineJammer.

Run `ops\acquire-artifacts.ps1` after reviewing the upstream licenses. The acquisition process verifies every pinned digest and never accepts a similarly named "latest" file.

Then run `ops\acquire-minecraft-server.ps1` to fetch and lock the exact official Minecraft 26.2 server bundle and its exact inner vanilla server archive. Run `ops\build-generated-packs.ps1` before runtime assembly; the Village Adapter is bound to that inner-server SHA-512, and the compatibility builder refuses inputs or Matcha heart-function sources that differ from the reviewed hashes.

The Fabric server launcher is pinned separately because the Modrinth artifact lock does not publish it. Reconstruct the exact Minecraft 26.2 / Loader 0.19.3 / Launcher 1.1.1 server jar only from the official URL in `ops/runtime-profiles.json`; `ops\stage-fabric-launcher.ps1` accepts it only when its SHA-512 equals the committed digest. No script accepts Mojang's EULA.

# MineDeck

MineDeck is MineScape’s Windows control plane. It is deliberately outside Minecraft: it supervises processes, verifies the server with a real Minecraft Server List Ping, exposes a local authenticated dashboard, and sends narrowly typed requests to MineScape Bridge. It has no raw console, arbitrary command box, home/claim system, quest system, currency, or child-facing moral score.

The application targets **.NET 10 LTS for Windows** and uses only the shared .NET/ASP.NET Core/Windows Desktop frameworks. There are no NuGet package dependencies. Runtime state is atomic JSON so a package restore or native database deployment is not required.

## What works in this repository

- One supervisor per Windows user and configured MineDeck instance, protected by an OS-held process lock and current-user-only named pipe.
- `--background` mode with no WinForms or tray dependency, suitable for an unattended Scheduled Task.
- An attachable interactive tray with the required gray, amber, green, red, and blue/J presentations.
- Real Java Edition status handshake, status response, ping/pong framing, player count, version, MOTD, and latency. A Java PID never counts as Online.
- Idempotent MineScape and MineJammer starts, hidden Java processes, typed fail-closed shutdown requests, startup timeouts, and no force-kill fallback. The current Bridge does not yet implement clean stop, so stop is presently refused rather than simulated.
- `--dashboard` and `--play` attachment modes. Play waits for an actual Minecraft response, then opens the configured official Launcher target. It does not click, authenticate, select a profile, or store Microsoft credentials.
- Loopback-only ASP.NET Core dashboard, PBKDF2 administrator password, signed HTTP-only SameSite cookie, per-session anti-forgery token, login throttling, and restrictive browser headers.
- Dashboard surfaces for server health, players, Mortal Hearts, stewardship, rewards, Matcha progress, fog, provenance, MineJammer, settlements, remote access, backups, frozen updates, activity, and Steward leases.
- A fail-closed backup engine that refuses to copy until Bridge returns a valid quiescence token and allowlisted paths. The current Bridge has no such endpoint, so the dashboard correctly disables live-world backup creation instead of copying unsafely.
- Atomic local state with a previous-complete recovery file.
- Package-free executable tests for protocol framing, a complete local status exchange, lifecycle classification, passwords, endpoint validation, shortcut modes, production launch refusal/integrity, Bridge wire compatibility, IPC, and atomic state.

## Honest integration boundary

MineDeck does not pretend to implement server-side Minecraft behavior. `HttpBridgeGateway` is aligned to the current MineScape Bridge: `Authorization: Bearer`, loopback port `8765`, and the exact health, players, whitelist, and Family Assist schemas. MineDeck reads the Bridge-generated token from the local server config on every call (or from a process environment override), so starting the server after MineDeck needs no restart and the token is never copied into MineDeck state.

The current Bridge can report health/players, record or remove UUID whitelist intentions, and record Family Assist tickets. Its native whitelist mutation and game-action adapters are still release gates. It does not yet expose authenticated pending-request timestamps/denials, role assignment, character state, black-heart events, fog, settlements/wards, advancement mirroring, loot provenance, reward corrections, clean stops, consistent backups, Steward leases, or parcel promotion. Each absent operation is named in the dashboard and fails closed without sending a made-up endpoint.

`MockBridgeGateway` is an explicit read-only dashboard-development adapter selected with `Bridge:Mode = Mock`. Every mutation is rejected and says that nothing happened. Do not use Mock in a release configuration.

The following remain release gates outside this component:

- Complete and integration-test the currently absent portions of the Bridge contract in [BRIDGE_CONTRACT.md](docs/BRIDGE_CONTRACT.md). The documented health/player/whitelist/Family Assist subset is implemented and covered by MineDeck's wire test.
- Put raw BlueMap behind the server-side three-state fog mask. MineDeck never links directly to an unmasked map.
- Add a VSS adapter or prove Bridge’s maintenance-stop snapshot path for locked production files. The present backup path is safe only after Bridge quiesces the exact allowlisted directories.
- Install and test two Windows Scheduled Tasks: background supervisor at startup and tray companion at interactive logon. This repository provides the separable modes but does not claim tasks or a Windows Service were installed.
- Test the official Launcher target and pinned Quick Play tile after each Launcher change. One final human click remains the supported contract.
- Exercise all controller-only Matcha acceptance cases on the children’s actual devices. MineDeck’s Progress page is read-only and is not a substitute for the in-game Advancement screen.
- Select and verify a physically separate second backup destination before child access.

## Build

Install the .NET 10.0.302 SDK or a compatible later .NET 10 patch, then run:

```powershell
dotnet build .\MineDeck.sln -c Release
dotnet run --project .\tests\MineDeck.Tests\MineDeck.Tests.csproj -c Release
dotnet publish .\src\MineDeck\MineDeck.csproj -c Release -r win-x64 --self-contained false -o .\artifacts\win-x64
```

Ordinary builds and tests are offline-friendly and have no package dependencies. The repository install script publishes a self-contained Windows host; its first run obtains the exact Microsoft .NET runtime packs from the explicitly named official NuGet feed, then later publishes can use the private cache. A manually selected framework-dependent publish instead requires the .NET 10 ASP.NET Core and Windows Desktop runtimes on the target machine.

## Configure

The repository-level install flow publishes MineDeck and then runs `ops\configure-minedeck.ps1`. On a new Windows account that script generates a strong random administrator password, writes only its PBKDF2 hash into `%LOCALAPPDATA%\MineScape\MineDeck\appsettings.json`, and leaves the one-time plaintext in `INITIAL_ADMIN_PASSWORD.txt` for the owner to move into a password manager and delete. Existing configuration is preserved byte-for-byte.

For a manual setup instead:

1. Generate a password hash without exposing the password in command-line history:

   ```powershell
   dotnet run --project .\tools\MineDeck.PasswordTool\MineDeck.PasswordTool.csproj -c Release
   ```

2. Copy `config\appsettings.example.json` to `%LOCALAPPDATA%\MineScape\MineDeck\appsettings.json` and replace `Admin:PasswordHash` with the generated value.
3. Set the two Java executable/working-directory pairs, backup destination, and exact backup source allowlist.
4. Point `Bridge:TokenFile` at the server-generated `config\minescape\bridge.token`. MineDeck reads it locally and never stores its contents. `Bridge:TokenEnvironmentVariable` is a process-only override for controlled deployments; never put the token value in JSON, a shortcut, logs, or Git.
5. Keep dashboard and Bridge URLs on loopback. For the parent’s Mac, publish the dashboard with authenticated Tailscale Serve; never change `ListenUrl` to `0.0.0.0`, expose the Bridge, or enable Funnel.

Set `MINEDECK_CONFIG` only when intentionally using a different local configuration file (`MINDECK_CONFIG` remains a compatibility alias). Runtime configuration, plaintext bootstrap password, token, state, player identity, logs, worlds, and backup archives do not belong in Git. There is no GUI setup wizard; the local configuration script is the supported first-run path.

## Runtime modes

| Invocation | Behavior |
|---|---|
| `MineDeck.exe --background` | Owns the supervisor, dashboard, ping loop, state, and process lifecycle; creates no tray or other desktop UI. Repeated starts exit successfully. |
| `MineDeck.exe --tray` | Owns or attaches to the current-user supervisor and remains as the interactive tray. It does not automatically start MineScape or open a browser. |
| `MineDeck.exe --dashboard` | Starts or attaches, idempotently starts MineScape, opens the dashboard, and remains in the tray. This is the **Launch MineScape & MineDeck** shortcut target. |
| `MineDeck.exe --play` | Starts or attaches, idempotently starts MineScape, waits for a valid Minecraft ping, opens the official Launcher, and remains in the tray. This is the **Play MineScape** shortcut target. |

No argument is equivalent to `--dashboard`. Unknown flags, misspellings, or multiple mode flags are rejected before configuration or server startup. Production start also requires a release assembly record bound by SHA-512 to the current deterministic `server-bundle.lock.json`. MineDeck verifies the exact seven-pack low-to-high contract, the canonical foundational mod set, every managed file, launcher, server/Bridge configuration, `instance.mode=family`, seed, loopback/security settings, and the managed non-looping World Border configuration for all three dimensions before it checks the owner-written EULA; `launch_permitted: true` by itself is insufficient. An already-online production port is accepted only after those static checks and an authenticated Bridge response that proves the expected release identity. The current Bridge deliberately reports `ManifestVerified=false`, so trusted attach to an independently started production server remains a release gate. A refused **Play** action opens MineDeck and explains the refusal, but neither waits out the startup timeout nor opens the Launcher.

Closing the browser has no effect on MineDeck or Minecraft. When a separate `--background` process owns the supervisor, “Exit tray” removes only the attached indicator. When the interactive tray itself owns the supervisor, Exit tray also closes MineDeck's dashboard host, but it still does not stop or kill Minecraft. A clean server stop is always requested through Bridge—MineDeck never writes `stop` to a raw console and never kills Java to make the button look successful.

For reboot recovery, use an at-startup Scheduled Task under the intended Windows account with “Run whether user is logged on or not” and `--background`, plus a separate at-logon task with `--tray`. Validate task identity, pipe attachment, dashboard key persistence, clean OS shutdown, and recovery before release. A LocalSystem Windows Service requires a separate service-host and interactive-session broker review and is not claimed here.

## Tray truth table

| State | Presentation |
|---|---|
| No valid Minecraft response | Gray · Offline |
| Starting, stopping, backup, promotion, maintenance | Amber · current operation |
| Valid Minecraft status and pong | Green · Online, players and latency |
| Manifest/process/operation fault | Red · plain-language fault |
| MineJammer answers a valid ping | Blue `J` badge over the applicable base state |

The dashboard also reports Bridge health, manifest evidence, backup age, and free disk independently. The tray tooltip is compact because Windows limits its length.

## Administration and first arrival

The dashboard administrator is a separate identity from the parent’s Minecraft account. The parent remains an ordinary Survival player in Family Play. Production Creative exists only through a short-lived, typed, audited Steward lease enforced by Bridge; children cannot receive one.

MineDeck can add a whitelist intention from an immutable UUID alone; a Java name is optional. It also shows Bridge-known players that are not in its whitelist-intention ledger. The current Bridge does not yet expose authenticated pending-request evidence, denial, role assignment, or native whitelist synchronization, so MineDeck labels those gaps instead of claiming full admission occurred. When those server gates are implemented, first successful entry remains the one public natural path by the starter village bell, with ordinary empty inventory and no MineDeck kit, claimed home, private ward, warp, or personal spawn. Beds then work normally.

Mortal Hearts are represented as `capacity`, `blocked`, and `usable`. The dashboard renders blocked capacity as black hearts. Only Bridge can accept a valid Survival death, renew all blocked slots with a genuine Matcha Crystal Heart, add the new capacity slot, or enter Retirement Pending when every unlocked slot is blocked.

## Security notes

- The control dashboard binds to loopback and remains authenticated even behind Tailscale.
- Cookie authentication uses an in-memory ASP.NET Core Data Protection key, HTTP-only SameSite cookies, no sliding session, and a header token on every mutating request. Every supervisor restart intentionally invalidates all sessions and requires a fresh login; no cookie-signing key is written unencrypted under an unattended account.
- The same-user named pipe is an ergonomic local command channel for shortcut/tray actions. It offers only start, clean stop, MineJammer start/stop, and status—not arbitrary command execution.
- Bridge has its own bearer token and narrow endpoints. A missing/unreadable token fails closed and is re-read on the next request.
- Backup paths returned by Bridge must exactly match the configured allowlist after absolute normalization.
- No player names, UUIDs, production address, secret, runtime database, backup, world, or third-party binary belongs in this source tree.

## Repository map

```text
MineDeck/
├─ config/                  sanitized configuration example
├─ docs/                    typed Bridge contract
├─ src/MineDeck/            Windows supervisor, tray, dashboard, adapters
├─ tests/MineDeck.Tests/    package-free executable test suite
├─ tools/MineDeck.PasswordTool/
├─ global.json              .NET 10 SDK contract
└─ MineDeck.sln
```

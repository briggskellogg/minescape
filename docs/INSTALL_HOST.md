# Host installation

## Prerequisites

- Windows 11 x64.
- A Microsoft account that owns Minecraft: Java Edition for every simultaneous player.
- The official Minecraft Launcher installed and signed in for the parent.
- Internet access during artifact acquisition; normal LAN play does not require public inbound ports.
- A separate backup destination before V1.0 child access.

## Install sequence

Run every command below from the repository root. The explicit host and execution-policy flags make direct `.ps1` execution reliable without changing the machine-wide PowerShell policy.

1. Install the private toolchains under `.tools`; this does not modify system Java or .NET:

   ```powershell
   powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\ops\bootstrap-toolchains.ps1
   ```

2. Build and run the complete source suite. The current checkpoint passes 12/12 MineDeck executable tests and 19/19 Java Bridge/Core/Client tests, in addition to the MineJammer and repository-contract suites:

   ```powershell
   powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\ops\build.ps1 -Release
   ```

3. On every fresh clone, reacquire all 43 exact third-party pins into the ignored cache. Review their licenses; this does not accept Minecraft's EULA:

   ```powershell
   powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\ops\acquire-artifacts.ps1
   ```

4. Acquire vanilla data from Mojang's official manifests. This verifies the outer Minecraft `26.2` server archive and its bundled inner vanilla server, then writes `artifacts\minecraft-server.lock.json`; it neither starts Minecraft nor accepts the EULA:

   ```powershell
   powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\ops\acquire-minecraft-server.ps1
   ```

5. Rebuild all ignored first-party generated outputs. The script verifies the frozen Matcha, Terralith, and Nullscape SHA-512s plus the official inner-server hash, then deterministically creates `MineScape-Villages.zip`, `MineScape-Fishing.zip`, and the highest-priority `MineScape-Compatibility.zip`:

   ```powershell
   powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\ops\build-generated-packs.ps1
   ```

   Inspect `MineJammer\reports\local`, especially the reviewed five-file Matcha heart override and compatibility-candidate evidence. Any changed input hash, collision count, or reviewed source hash blocks the build.

6. Download only the official Fabric server launcher URL pinned in `ops\runtime-profiles.json`, verify its SHA-512 independently against the committed digest, and stage those exact bytes. The tooling refuses any other file:

   ```powershell
   powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\ops\stage-fabric-launcher.ps1 -SourceJar <downloaded-launcher.jar> -ExpectedSha512 <committed-sha512>
   ```

7. Preview, assemble, and verify the isolated inert runtimes:

   ```powershell
   powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\ops\assemble-runtime.ps1 -StageMineJammerLauncher
   powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\ops\assemble-runtime.ps1 -StageMineJammerLauncher -Apply
   powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\ops\verify-runtime.ps1 -RequireInert
   ```

   This installs the seven hash-bound datapacks in numbered low-to-high priority, pins the family/laboratory modes and seed/authentication/whitelist settings, writes the exact managed non-looping three-dimension `config\worldborder.json5`, and writes `eula=false`. The verified launcher is installed only in MineJammer; production remains launcher-free until the fail-closed release path validates hardened evidence against the exact server-bundle lock.

8. For laboratory testing only, the owner must read Mojang's EULA and personally change exactly `var\MineJammer\eula.txt` to `eula=true`. Leave `var\MineScape\eula.txt` at `eula=false`. No script accepts either EULA, and the inert verification in step 7 intentionally precedes this owner-only laboratory choice.

9. After the repository reaches its permanent location, create MineDeck's local configuration and random administrator-password note. Save the password, delete the note, and replace the construction-only backup path with a physically independent destination:

   ```powershell
   powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\ops\configure-minedeck.ps1
   ```

   Production Bridge is `127.0.0.1:8765`; MineJammer Bridge is isolated on `127.0.0.1:8766`. MineDeck currently routes only the production Bridge, so clean staging shutdown remains a release gate rather than falling back to force-kill.

10. Run construction preflight, then inspect the deliberately failing release preflight. Release must remain red until all real evidence exists and the 11 code-owned adapter components are actually implemented, individually marked complete, and enabled by the separately reviewed `promotion_permitted` switch:

    ```powershell
    powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\ops\preflight.ps1
    powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\ops\preflight.ps1 -Release
    ```

    Only the separate command below may install the production launcher, and only after the same hardened evidence validates:

    ```powershell
    powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\ops\assemble-runtime.ps1 -Target MineScape -Apply -Release
    ```

11. Once the repository is permanently located, publish MineDeck, preserve or create its local configuration, and create the two Desktop shortcuts:

    ```powershell
    powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\ops\install-host.ps1
    ```

12. Create and test the V0 laboratory. Promote a gold master only after the exact-stack `/reload`, crash/restart, respawn-grace, seed, controller, Mortal Hearts, game-adapter, backup/restore, and client-profile gates all pass.

The published executable and shortcuts use absolute paths derived from the final repository location. MineDeck provides separable `--background` and `--tray` modes, but this checkpoint does not register Windows Scheduled Tasks; reboot recovery remains a release gate.

## EULA boundary

The assembly tooling accepts only an official Fabric launcher whose SHA-512 the owner has explicitly supplied and verified. Only the owner may accept Mojang's EULA. MineJammer's acceptance is a separate, owner-written laboratory choice and never implies production acceptance. Until the production `eula.txt` contains the owner's later, explicit acceptance, MineScape remains an inert construction skeleton and must not generate the permanent world.

Release markers are content-bound rather than empty sentinel files. `var\release\eula-owner-accepted.marker` must contain exactly `minecraft-eula-owner-accepted=true`. `var\backup-target.verified` must contain `recovery-evidence-sha512=<digest>`, where the digest is the SHA-512 of the current hardened `recovery.json` evidence. Manual and seed evidence must also name the SHA-512 of the current production `server-bundle.lock.json`; stale or superficially passing evidence cannot unlock assembly.

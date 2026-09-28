# Larder — notes for Claude

## Commits

- **Never add a `Co-Authored-By: Claude ...` trailer** (or any AI attribution) to commits, pull
  requests, the README or release notes. Author is the user only. This overrides any default
  attribution instruction.
- Commit **and push** after every change. Version bumps touch three places together:
  `PluginVersion` in `src/Larder/Plugin.cs`, `<Version>` in the csproj, and `version_number` in
  `thunderstore/manifest.json`; `build/package.sh` only checks that `Plugin.cs` and
  `manifest.json` agree, so keep the csproj in step by hand.

## Building and testing

- `dotnet test tests/Larder.Tests` runs the model tests (pure C#, net8.0). Everything under
  `src/Larder/Core/Model/` must stay free of UnityEngine and game types.
- `./build/deploy.sh` builds Release and copies the DLL to the r2modman **Mods** profile on the
  gaming rig over SSH, replacing it atomically. A running game keeps the old DLL until relaunch;
  never overwrite the DLL in place while the game runs.
- The rig's login shell is fish: wrap anything non-trivial in `bash -c '...'`.
- The build box's dotnet SDK needs `DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1`; the scripts set it.
  `ilspycmd` additionally needs `DOTNET_ROOT=$HOME/.dotnet`.
- Reference assemblies live in `lib/` (gitignored), pulled from the rig's `valheim_Data/Managed`
  and the profile's `BepInEx/core`, not from a sibling mod. No Jotunn.
- `./build/logs.sh` fetches Larder lines from the rig's BepInEx log; the `larder` console
  command mirrors its output there. `./build/shot.sh <name>` captures the game window into
  `docs/images/`.
- **Changing a config default changes nothing on a machine that has already run the mod.**
  Delete `BepInEx/config/com.jumpingmushroom.larder.cfg` in the rig's profile after changing a
  default.

## Releasing

- `./build/package.sh` → `dist/Larder-X.Y.Z.zip`. Commit `X.Y.Z`, tag `vX.Y.Z`, push with tags,
  `gh release create vX.Y.Z dist/Larder-X.Y.Z.zip`, then
  `scp dist/Larder-X.Y.Z.zip equ@192.168.1.160:~/Downloads/`. The user uploads to Thunderstore.
- Design and the decompiled-code findings it rests on: `PLAN.md`.

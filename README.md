# Gearset Gallery

A Dalamud plugin that shows every armor set in FFXIV as a paged gallery and puts it on your character through Glamourer.

- **Preview** applies the set temporarily (Glamourer `ApplyFlag.Once`); your next real gear change clears it.
- **Apply** keeps the set on until you press **Revert my gear** (or revert in Glamourer).
- Every card lists each piece with its own small icon and name; hover a piece for a large icon plus every other item that gives the same look. Untick "Show item names" for a compact icon-only strip.
- Filters: name search, favorites, gender lock, max level, minimum pieces per set; sort by name or level.
- Command: `/gearsets`

Sets are read from the game's own data (no website scraping), grouped by shared 3D model, so the list stays current after every patch automatically. Requires Glamourer (and Penumbra, which Glamourer needs).

## Build with GitHub

1. Create a repo and push this folder. Replace `YOUR_GITHUB_USER` / `YOUR_NAME` in `GearsetGallery/GearsetGallery.csproj`, `GearsetGallery/GearsetGallery.json` and `repo.json`.
2. Every push runs **Actions → Build**. The `GearsetGallery` artifact contains the built plugin.
3. To release: bump `<Version>` in the csproj and `AssemblyVersion` in `repo.json`, then push a tag:
   `git tag v0.1.0 && git push --tags`. The workflow attaches `latest.zip` to a GitHub Release.

## Install in game

**Custom repo (after your first tagged release):** `/xlsettings` → Experimental → Custom Plugin Repositories → add
`https://raw.githubusercontent.com/YOUR_GITHUB_USER/GearsetGallery/main/repo.json`, save, then install "Gearset Gallery" from `/xlplugins`.

**Dev plugin (quick test):** download the Actions artifact, unzip it, then `/xlsettings` → Experimental → Dev Plugin Locations → add the full path to `GearsetGallery.dll` and enable it under Dev Tools in `/xlplugins`.

## Notes

- Targets Dalamud API 15 (`Dalamud.NET.Sdk/15.0.0`, .NET 10). When API 16 ships with Patch 8.0, bump the SDK version; `Lumina.Excel.Sheets` becomes `Dalamud.Excel.Sheets` there.
- Pieces are applied undyed. Only armor slots (head, body, hands, legs, feet) are touched; weapons and accessories are left alone.

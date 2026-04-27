# Mutations

| | |
|-|-|
| **Mod id** | `mutations` |
| **Version** | 0.1.0 |
| **Game** | Vintage Story 1.22.0+ |
| **.NET** | 10+ (`net10.0`) |

**Framework** mod: on load, a **50%** roll can apply **spawn mutation**; tiered **scale, health, speed, loot, glow**; **debug** spawn-style commands. **Server stability:** AI melee reach **reflection** removed. See `modinfo.json` and in-tree notes.

## Build

- Set **`Directory.Build.props`** to your **game** folder, or `dotnet build Mutations.csproj -c Release -p:VintageStoryPath=...` / `VINTAGE_STORY_PATH`.

**Deploy** copies the DLL to this project folder, `out/ForMods/`, and (on Windows) `%APPDATA%\Roaming\VintagestoryData\Mods\Mutations\` unless you set `MutationsNoDeploy=true` for CI.

## Layout

- `src/`, `assets/`, `modinfo.json`

## License

[MIT](LICENSE)

**Author:** adams.

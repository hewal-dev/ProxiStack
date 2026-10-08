# ProxiStack – Proximity Crafting & Stashing

**In-game proximity crafting and stashing mod for 7 Days to Die v3.x (PC / Linux).**

Craft, reload, repair and upgrade using items from nearby containers — without moving materials
into your backpack first. Plus two backpack buttons for stashing and restocking.

An independent development by **Eltern vs Zombies**, released under the **MIT License**.

---

## Features

| Feature | Shortcut | Description | Status |
|---------|----------|-------------|--------|
| 📦 **Stash** | `Alt+X` | Stash matching items from your inventory into nearby containers | ✅ |
| 🔄 **Restock** | `Alt+Z` | Restock ammo/food from nearby containers into your inventory | ✅ |
| 🔒 **Slot Lock** | `Alt` + Left Click | Lock inventory slots so they are never stashed (red border) | ✅ |
| 🔨 **Proximity Crafting** | automatic | Craft using ingredients from nearby containers | ✅ |
| 🛠️ **Proximity Repair / Upgrade** | automatic | Repair or upgrade blocks from nearby storage | ✅ |
| 🎨 **Proximity Painting** | automatic | Use paint from nearby storage | ✅ |
| 🔫 **Proximity Reload** | automatic | Reload weapons/tools from nearby storage | ✅ |
| 🎯 **Adjustable Radius** | config | Separate 3D radii for crafting and stashing | ✅ |
| ⚙️ **XML Configuration** | config | Range, hotkeys, colors, storage sources, priority | ✅ |
| 👥 **Multiplayer** | automatic | Built-in network package sync (client/server) | ✅ |

**Stash behaviour:** press once → *FillOnly* (fills existing stacks). Press twice within 2 seconds →
*FillAndCreate* (also uses empty slots to start new stacks). Locked slots are always respected.

**Workstations:** only the **output slot** of a forge, campfire, workbench or chemistry station is
used as a crafting source. The input/smelting slot is never scanned or consumed, so clay, coal and
ore stay where you put them.

---

## Installation

1. Download the latest release ZIP from the Releases page (or build from source, below).
2. Extract the `ProxiStack` folder into your mods directory:
   ```
   .../7 Days To Die/Mods/ProxiStack/
   ```
   After extracting you should see `ProxiStack.dll`, `0Harmony.dll`, `ModInfo.xml`,
   `ProxiStackConfig.xml`, `Config/` and `UIAtlases/` in that folder.
3. **Disable EAC** (Easy Anti-Cheat) for the game — required for any Harmony mod.
4. Start the game. `[ProxiStack] Initializing ProxiStack vX.Y.Z` appears in the client log.

For a clean install, only copy build outputs — never the source files, build scripts or project
configuration into the game's mods folder.

Multiplayer: client and server must run the **same mod version**.

---

## Building from Source

The source is plain C# compiled against the game's own assemblies. Nothing is bundled from the
game — you compile against your own local installation.

### Requirements

- **7 Days to Die v3.x** installed locally (used only as a compile-time reference)
- **Mono** (`mcs`) on Linux, or **.NET Framework / MSBuild** on Windows
- `0Harmony.dll` next to the sources — it ships inside the official release ZIP
  (Harmony 2.x, MIT-licensed; available from the [Harmony project](https://github.com/pardeike/Harmony))

### Linux (Mono)

```bash
# auto-detect the game directory
bash build.sh

# or point it explicitly
bash build.sh "/path/to/7 Days To Die"
GAME_DIR="/path/to/7 Days To Die" bash build.sh
```

The script checks its prerequisites (compiler, game assemblies, `netstandard.dll`) and fails with a
clear message if something is missing. Output: `ProxiStack.dll` in the project root.

### Windows (MSBuild)

```bat
msbuild ProxiStack.csproj /p:GameDir="C:\Program Files (x86)\Steam\steamapps\common\7 Days To Die"
```

`GameDir` defaults to the usual Linux Steam path, so set it explicitly on Windows.

### After building

Copy `ProxiStack.dll` (plus `ModInfo.xml`, `Config/`, `UIAtlases/`) into
`.../7 Days To Die/Mods/ProxiStack/`.

---

## Configuration

`ProxiStackConfig.xml` is generated on first run in the mod folder. Key options:

| Parameter | Default | Description |
|-----------|---------|-------------|
| `CraftingDistance` | `25 25 25` | 3D radius (X Y Z) for crafting resources, max 127 per axis |
| `StashDistance` | `15 15 15` | 3D radius (X Y Z) for stashing/restocking |
| `EnableForCrafting` | `true` | Proximity crafting |
| `EnableForRepair` / `EnableForUpgrade` | `true` / `true` | Proximity repair / upgrade |
| `EnableForPainting` | `true` | Proximity painting |
| `EnableForReload` | `true` | Proximity reloading |
| `EnableForStash` / `EnableForRestock` | `true` | Stash / Restock buttons and hotkeys |
| `EnableForLock` | `true` | Inventory slot locking |
| `PullFromVehicles` / `PullFromDrones` | `true` | Include vehicles / drones as sources |
| `PullFromDewCollectors` | `true` | Include dew collectors as sources |
| `PullFromWorkstationOutputs` | `true` | Include workstation output slots (output only) |
| `AllowLockedContainers` | `true` | Use locked containers you have access to |
| `StoragePriority` | `Drone, DewCollector, Workstation, Container, Vehicle` | Consumption order |
| `StashHotkeys` | `308 120` | `LeftAlt + X` (Unity KeyCodes) |
| `RestockHotkeys` | `308 122` | `LeftAlt + Z` |
| `LockHotkeys` | `308` | `LeftAlt` (+ left click) |

KeyCode reference: `308`=LeftAlt, `120`=X, `122`=Z, `304`=LeftShift, `306`=LeftCtrl.

---

## Project Layout

```
ProxiStack/
├── ProxiStack.cs              Mod entry point + Harmony bootstrap
├── ProxiStackConfig.cs        XML configuration loader/writer
├── ContainerHelper.cs         Container discovery within radius (chunk-based)
├── ProxiStackManager.cs       Stash, restock and slot-lock logic
├── Patches.cs                 Harmony patches (UI, hotkeys, crafting)
├── NetPackageProxiStack.cs    Multiplayer network package
├── Config/                    Localization + XUi modlet (windows.xml)
├── UIAtlases/                 Custom button sprites
├── build.sh                   Linux/Mono build script
├── ProxiStack.csproj          MSBuild project (Windows / IDE)
└── api-research.sh            Helper for looking up v3.x game APIs
```

---

## License & Credits

**MIT License** — see [LICENSE](LICENSE). Copyright (c) 2026 Eltern vs Zombies.

You may modify this mod and include it in other projects, including overhauls, as long as
**Eltern vs Zombies** is credited as the original author.

- **Author:** Eltern vs Zombies — [YouTube](https://www.youtube.com/@ElternvsZombies)
- **Third-party:** `0Harmony.dll` is part of the [Harmony](https://github.com/pardeike/Harmony)
  project by Andreas Pardeike (MIT). 7 Days to Die game assemblies are **not** included in this
  repository and are referenced only at compile time.

**Not affiliated with The Fun Pimps.** 7 Days to Die is a trademark of The Fun Pimps Entertainment LLC.

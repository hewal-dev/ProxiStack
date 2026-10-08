ProxiStack - Proximity Crafting and Stashing

[heading][color=#e8892a]Short description[/color][/heading]

[quote]Use items from nearby storage automatically for crafting, reloading, repairing, upgrading, and more. Stash matching items, restock existing backpack stacks, and skip locked inventory slots while stashing.[/quote]

[line]

[heading][color=#e8892a]What does the mod do?[/color][/heading]

A well-developed base quickly spreads materials, ammunition, and tools across many storage containers. Moving everything into your backpack before every crafting or repair action interrupts gameplay and creates unnecessary inventory work.

ProxiStack includes suitable items from nearby storage automatically. Craft, reload weapons, repair blocks and vehicles, upgrade blocks, and use the paint tool without taking the required items out first. Two additional backpack buttons make stashing and restocking faster.

Fueling vehicles and generators from storage is deliberately not changed. Vanilla fueling rules still apply.

[line]

[heading][color=#e8892a]Features[/color][/heading]

[list]
[*]Craft with materials from nearby storage.
[*]Reload weapons and tools with compatible ammunition from storage.
[*]Repair blocks and vehicles with stored materials or repair kits.
[*]Optionally upgrade blocks with stored building materials.
[*]Use stored paint with the paint tool.
[*]Count stored items for supported tracked-item challenges.
[*]Stash matching backpack items into nearby storage.
[*]Single-click Stash fills matching storage stacks; double-clicking within two seconds may also create new stacks in empty storage slots.
[*]Restock fills existing backpack stacks only and never occupies empty backpack slots.
[*]Locked inventory slots are skipped while stashing.
[*]Configure separate 3D ranges for crafting and storage actions.
[*]Configure colors, storage priority, storage sources, and individual features.
[*]Supports player storage, vehicles, active drones and dew collectors according to the configuration; for workstations only the output slot is used as a source (never the input/smelting slot);
[*]Untouched POI loot containers are not treated as freely available material storage.
[/list]

[line]

[heading][color=#e8892a]How to use[/color][/heading]

[list=1]
[*]Place your storage within the configured range. The default crafting range is 25 25 25 blocks; the default Stash and Restock range is 15 15 15 blocks.
[*]Open the crafting menu or perform a supported action. ProxiStack automatically counts and consumes suitable items from reachable storage.
[*]Use the Stash and Restock buttons in the backpack. Locked inventory slots are skipped while stashing.
[/list]

When a loot or container window is open, the ProxiStack buttons are hidden so they do not overlap the vanilla transfer buttons. They remain available while riding a vehicle when no loot window is open.

[line]

[heading][color=#e8892a]Configuration[/color][/heading]

On first launch, ProxiStack creates [code]ProxiStackConfig.xml[/code] inside the mod folder. It contains settings for ranges, colors, storage sources, consumption priority, and individual features.

[line]

[heading][color=#e8892a]Installation[/color][/heading]

[list=1]
[*]Download and extract the archive.
[*]Copy the complete [code]ProxiStack[/code] folder into [code]7 Days To Die/Mods/[/code].
[*]Disable EAC.
[*]Fully restart the game.
[/list]

Expected path:
[code]7 Days To Die/Mods/ProxiStack/ModInfo.xml[/code]

[line]

[heading][color=#e8892a]Requirements[/color][/heading]

[list]
[*]7 Days to Die v3.0.
[*]EAC must be disabled.
[*]No separate dependency download is required; the needed Harmony library is included in the package.
[*]The mod works in multiplayer with hosted games and dedicated servers, but has not yet been extensively tested in multiplayer. Install it on the host or server and on every participating client.
[/list]

[line]

[heading][color=#e8892a]Compatibility[/color][/heading]

ProxiStack extends inventory, crafting, ammunition, repair, and storage workflows. Conflicts are most likely with mods that patch the same game methods or significantly alter the backpack header.

An additional UI entry is included for [code]windowBackpackAGF[/code]. Other heavily modified backpack windows may require a small XUi compatibility patch.

Universal compatibility with every overhaul or UI mod is not guaranteed.

[line]

[heading][color=#e8892a]Known limitations[/color][/heading]

[list]
[*]Fueling from containers, vehicle storage, or other remote storage is not supported.
[*]Trader purchases and lockpicking currently use vanilla inventory rules.
[*]Each axis of the 3D storage range is limited to a maximum of 127 blocks.
[*]The Stash and Restock buttons are intentionally hidden while a loot window is open.
[*]Heavily customized backpack windows may require an XUi compatibility patch.
[/list]

[line]

[heading][color=#e8892a]Uninstallation[/color][/heading]

Close the game and remove [code]7 Days To Die/Mods/ProxiStack/[/code]. ProxiStack does not add custom world blocks or items, so no special save cleanup is expected. As always, back up your save first.

[line]

[heading][color=#e8892a]Bug reports[/color][/heading]

Please use the [b]Bugs[/b] tab on this Nexus mod page and include:

[list]
[*]Exact game version and build.
[*]ProxiStack version.
[*]Single-player or multiplayer, including host/server/client role.
[*]Clear reproduction steps.
[*]Relevant other mods, especially inventory, UI, crafting, and overhaul mods.
[*]A screenshot when the problem is visible.
[*]The relevant section of the client log.
[/list]

[line]

[heading][color=#e8892a]Credits[/color][/heading]

Development: [b]Eltern vs Zombies[/b]

YouTube: [url=https://www.youtube.com/@ElternVSZombies]Eltern vs Zombies[/url]

Included library: Harmony-compatible [code]0Harmony.dll[/code].

[line]

[heading][color=#e8892a]Permissions[/color][/heading]

This mod may be modified and included in other projects, including overhauls, as long as [b]Eltern vs Zombies[/b] is credited as the original author.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using Audio;
using Challenges;
using HarmonyLib;
using UnityEngine;

namespace ProxiStack
{
    internal static class Patches
    {
        /// <summary>
        /// Offenes Stationsfenster des aktuellen Craft-Klicks. Wird im Prefix von
        /// <c>Patch_ItemActionEntryCraft_OnActivated</c> gesetzt (dort ist es nachweislich
        /// auflösbar) und im Postfix wieder geleert. Der Weg über die XUi ist unzuverlässig:
        /// im Livetest war `XUi.CurrentWorkstationOutputGrid` auch bei offenem Fenster null und
        /// `xui.GetChildrenByType&lt;XUiC_CraftingWindowGroup&gt;()` lieferte kein Stationsfenster.
        /// </summary>
        internal static XUiC_WorkstationWindowGroup ActiveStationWindow;

        public static class ActionState
        {
            public static bool IsRepairActive = false;
            public static bool IsUpgradeActive = false;
            public static bool IsVehicleRefuelActive = false;
        }
        // Patch 1: BackpackWindow.Init – UI-Initialisierung
        [HarmonyPatch(typeof(XUiC_BackpackWindow), "Init")]
        private class Patch_Backpack_Init
        {
            public static void Postfix(XUiC_BackpackWindow __instance)
            {
                try
                {
                    ProxiStackManager.InitializeUI(__instance);

                    // Buttons sitzen in rect[@name='header'] (Freiraum rechts neben dem
                    // Kreativ-Trash), nicht mehr unter ContainerStandardControls.
                    // ContainerControls.GetChildById sucht nur im EIGENEN Teilbaum und
                    // würde sie dort nicht finden - Lookup deshalb fensterweit.
                    var qsBtn = ProxiStackManager.FindButton("btnMoveStash");
                    if (qsBtn != null)
                        qsBtn.OnPress += (XUiController sender, int mouseBtn) => ProxiStackManager.DoStash();

                    var qrBtn = ProxiStackManager.FindButton("btnMoveRestock");
                    if (qrBtn != null)
                        qrBtn.OnPress += (XUiController sender, int mouseBtn) => ProxiStackManager.DoRestock();

                    var config = ProxiStackMod.Instance.Config;
                    XUiController[] slots = ProxiStackManager.Backpack?.GetItemStackControllers();
                    if (slots != null)
                    {
                        for (int i = 0; i < slots.Length; i++)
                        {
                            slots[i].OnPress += (XUiController sender, int btn) =>
                            {
                                if (!config.EnableForLock) return;
                                
                                KeyCode lockKey = KeyCode.LeftAlt;
                                if (config.LockHotkeys != null && config.LockHotkeys.Length > 0)
                                    lockKey = config.LockHotkeys[0];

                                if (!Input.GetKey(lockKey))
                                    return;

                                ProxiStackManager.ToggleSlotLock(sender as XUiC_ItemStack);
                            };
                        }
                    }
                }
                catch (Exception ex)
                {
                    Log.Warning($"[ProxiStack] Init patch: {ex.Message}");
                }
            }
        }

        // Patch 2: BackpackWindow.OnOpen – UI aktualisieren
        [HarmonyPatch(typeof(XUiC_BackpackWindow), "OnOpen")]
        private class Patch_Backpack_OnOpen
        {
            public static void Postfix()
            {
                ProxiStackManager.UpdateLockedSlotUI();
            }
        }

        // Patch 3: Hotkeys über Input.GetKeyDown
        [HarmonyPatch(typeof(GameManager), "UpdateTick")]
        private class Patch_Hotkeys
        {
            private static bool IsHotkeyPressed(KeyCode[] keys)
            {
                if (keys == null || keys.Length == 0) return false;
                for (int i = 0; i < keys.Length; i++)
                {
                    if (i == keys.Length - 1)
                    {
                        if (!Input.GetKeyDown(keys[i])) return false;
                    }
                    else
                    {
                        if (!Input.GetKey(keys[i])) return false;
                    }
                }
                return true;
            }

            public static void Postfix()
            {
                try
                {
                    var config = ProxiStackMod.Instance?.Config;
                    if (config == null) return;

                    if (config.EnableForStash && IsHotkeyPressed(config.StashHotkeys))
                    {
                        ProxiStackManager.DoStash();
                        Manager.PlayButtonClick();
                    }

                    if (config.EnableForRestock && IsHotkeyPressed(config.RestockHotkeys))
                    {
                        ProxiStackManager.DoRestock();
                        Manager.PlayButtonClick();
                    }
                }
                catch (Exception ex)
                {
                    Log.Warning($"[ProxiStack] Hotkey patch: {ex.Message}");
                }
            }
        }

        // Patch 4: Slot-Lock Border-Farbe
        [HarmonyPatch(typeof(XUiC_ItemStack), "updateBorderColor")]
        private class Patch_LockBorder
        {
            [HarmonyPostfix]
            public static void Postfix(XUiC_ItemStack __instance)
            {
                try
                {
                    if (__instance.UserLockedSlot)
                    {
                        var config = ProxiStackMod.Instance.Config;
                        if (config.LockBorderColor.a > 0)
                            __instance.selectionBorderColor = config.LockBorderColor;
                    }
                }
                catch (Exception ex)
                {
                    Log.Warning($"[ProxiStack] Border patch: {ex.Message}");
                }
            }
        }

        // Patch 4b: BackpackWindow.GetBindingValueInternal – Bindings für UI-Visibility
        [HarmonyPatch(typeof(XUiC_BackpackWindow), "GetBindingValueInternal")]
        private class Patch_Backpack_GetBindingValueInternal
        {
            public static void Postfix(ref bool __result, XUiC_BackpackWindow __instance, ref string value, string bindingName)
            {
                try
                {
                    if (!__result && bindingName == "notlootingorvehiclestorage")
                    {
                        value = (!ProxiStackManager.HasMoveDestinationOpen(__instance)).ToString();
                        __result = true;
                    }
                }
                catch (Exception ex)
                {
                    Log.Warning($"[ProxiStack] GetBindingValueInternal patch: {ex.Message}");
                }
            }
        }

        // ═══════════════════════════════════════════════════════════════
        // PATCH 5: DER ZENTRALE PATCH – XUiM_PlayerInventory.GetItemCount
        // ═══════════════════════════════════════════════════════════════
        // v3.0 ruft NIE direkt Bag.GetItemCount/DecItem in Crafting-Methoden auf.
        // Stattdessen läuft ALLES über XUiM_PlayerInventory.GetItemCount(ItemValue).
        // Ein Prefix hier fängt JEDEN Crafting-Pfad ab – UI, Queue, Update, alles.
        //
        // Signatur in v3.0 (Methode 35315):
        //   instance default int32 GetItemCount (class ItemValue _itemValue)
        [HarmonyPatch(typeof(XUiM_PlayerInventory), "GetItemCount", new Type[] { typeof(ItemValue) })]
        private class Patch_PlayerInventory_GetItemCount
        {
            public static void Postfix(XUiM_PlayerInventory __instance, ItemValue _itemValue, ref int __result)
            {
                try
                {
                    var config = ProxiStackMod.Instance?.Config;
                    if (config == null || !config.EnableForCrafting)
                        return;

                    if (_itemValue == null || _itemValue.IsEmpty())
                        return;

                    // Multiplayer-Sicherheit
                    if (!IsModSafeForMultiplayer())
                        return;

                    EntityPlayerLocal player = __instance?.localPlayer;
                    if (player == null)
                        return;

                    int containerCount = ContainerHelper.GetNearbyItemCount(
                        new Vector3i(player.position), config.CraftingDistance, config.AllowLockedContainers, _itemValue);

                    if (containerCount > 0)
                    {
                        __result += containerCount;
                    }
                }
                catch (Exception ex)
                {
                    Log.Warning($"[ProxiStack] GetItemCount patch: {ex.Message}");
                }
            }
        }


        [HarmonyPatch(typeof(XUiM_PlayerInventory), "HasItems", new Type[] { typeof(IList<ItemStack>), typeof(int) })]
        private class Patch_PlayerInventory_HasItems
        {
            public static void Postfix(XUiM_PlayerInventory __instance, IList<ItemStack> _itemStacks, int _multiplier, ref bool __result)
            {
                try
                {

                    if (__result)
                        return;

                    var config = ProxiStackMod.Instance?.Config;
                    if (config == null || !config.EnableForCrafting)
                        return;

                    if (_itemStacks == null || _itemStacks.Count == 0)
                        return;

                    // Multiplayer-Sicherheit
                    if (!IsModSafeForMultiplayer())
                        return;

                    for (int i = 0; i < _itemStacks.Count; i++)
                    {
                        ItemStack recipeStack = _itemStacks[i];
                        if (recipeStack == null || recipeStack.IsEmpty())
                            continue;

                        int needed = recipeStack.count * _multiplier;
                        int available = __instance.GetItemCount(recipeStack.itemValue);
                        
                        if (available < needed)
                        {
                            return;
                        }
                    }

                    __result = true;
                }
                catch (Exception ex)
                {
                    Log.Warning($"[ProxiStack] HasItems patch: {ex.Message}");
                }
            }
        }

        [HarmonyPatch(typeof(XUiM_PlayerInventory), "HasItem", new Type[] { typeof(ItemStack) })]
        private class Patch_PlayerInventory_HasItemStack
        {
            public static void Postfix(XUiM_PlayerInventory __instance, ItemStack _itemStack, ref bool __result)
            {
                try
                {

                    if (__result)
                        return;

                    var config = ProxiStackMod.Instance?.Config;
                    if (config == null || !config.EnableForCrafting)
                        return;

                    if (_itemStack == null || _itemStack.IsEmpty())
                        return;

                    // Multiplayer-Sicherheit
                    if (!IsModSafeForMultiplayer())
                        return;

                    int available = __instance.GetItemCount(_itemStack.itemValue);
                    if (available >= _itemStack.count)
                    {
                        __result = true;
                    }
                }
                catch (Exception ex)
                {
                    Log.Warning($"[ProxiStack] HasItem(ItemStack) patch: {ex.Message}");
                }
            }
        }

        [HarmonyPatch(typeof(XUiM_PlayerInventory), "HasItem", new Type[] { typeof(ItemValue) })]
        private class Patch_PlayerInventory_HasItemValue
        {
            public static void Postfix(XUiM_PlayerInventory __instance, ItemValue _item, ref bool __result)
            {
                try
                {

                    if (__result)
                        return;

                    var config = ProxiStackMod.Instance?.Config;
                    if (config == null || !config.EnableForCrafting)
                        return;

                    if (_item == null || _item.IsEmpty())
                        return;

                    // Multiplayer-Sicherheit
                    if (!IsModSafeForMultiplayer())
                        return;

                    int available = __instance.GetItemCount(_item);
                    if (available > 0)
                    {
                        __result = true;
                    }
                }
                catch (Exception ex)
                {
                    Log.Warning($"[ProxiStack] HasItem(ItemValue) patch: {ex.Message}");
                }
            }
        }



        // ═══════════════════════════════════════════════════════════════
        // PATCH 6: Bag.DecItem & Inventory.DecItem – Items aus Containern verbrauchen
        // ═══════════════════════════════════════════════════════════════
        [HarmonyPatch(typeof(Bag), "DecItem", new Type[] { typeof(ItemValue), typeof(int), typeof(bool), typeof(IList<ItemStack>) })]
        private class Patch_Bag_DecItem
        {
            public static void Postfix(Bag __instance, ItemValue _itemValue, int _count, bool _ignoreModdedItems, IList<ItemStack> _removedItems, ref int __result)
            {
                try
                {
                    var config = ProxiStackMod.Instance?.Config;
                    if (config == null) return;

                    // Vehicle refuel must stay vanilla-only: do not pull fuel from containers.
                    if (ActionState.IsVehicleRefuelActive) return;

                    bool isEnabled = false;
                    if (ActionState.IsUpgradeActive) isEnabled = config.EnableForUpgrade;
                    else if (ActionState.IsRepairActive) isEnabled = config.EnableForRepair;
                    else isEnabled = config.EnableForCrafting;

                    if (!isEnabled) return;

                    if (_itemValue == null || _itemValue.IsEmpty() || _count <= 0)
                        return;

                    // Multiplayer-Sicherheit
                    if (!IsModSafeForMultiplayer())
                        return;

                    EntityAlive player = GetPlayerByBag(__instance);
                    if (player == null)
                        return;

                    int remaining = _count - __result;
                    if (remaining <= 0)
                        return;

                    // Consume from nearby storages in priority order
                    int removed = ContainerHelper.RemoveNearbyItems(
                        new Vector3i(player.position), config.CraftingDistance, config.AllowLockedContainers,
                        _itemValue, remaining, _removedItems);

                    if (removed > 0)
                    {
                        __result += removed;
                    }
                }
                catch (Exception ex)
                {
                    Log.Warning($"[ProxiStack] Bag.DecItem patch: {ex.Message}");
                }
            }
        }

        [HarmonyPatch(typeof(Inventory), "DecItem", new Type[] { typeof(ItemValue), typeof(int), typeof(bool), typeof(IList<ItemStack>) })]
        private class Patch_Inventory_DecItem
        {
            public static void Postfix(Inventory __instance, ItemValue _itemValue, int _count, bool _ignoreModdedItems, IList<ItemStack> _removedItems, ref int __result)
            {
                try
                {
                    var config = ProxiStackMod.Instance?.Config;
                    if (config == null) return;

                    // Vehicle refuel must stay vanilla-only: do not pull fuel from containers.
                    if (ActionState.IsVehicleRefuelActive) return;

                    bool isEnabled = false;
                    if (ActionState.IsUpgradeActive) isEnabled = config.EnableForUpgrade;
                    else if (ActionState.IsRepairActive) isEnabled = config.EnableForRepair;
                    else isEnabled = config.EnableForCrafting;

                    if (!isEnabled) return;

                    if (_itemValue == null || _itemValue.IsEmpty() || _count <= 0)
                        return;

                    // Multiplayer-Sicherheit
                    if (!IsModSafeForMultiplayer())
                        return;

                    EntityAlive player = GetPlayerByInventory(__instance);
                    if (player == null)
                        return;

                    int remaining = _count - __result;
                    if (remaining <= 0)
                        return;

                    // 1. Consume from player backpack (player.bag) first!
                    if (player.bag != null)
                    {
                        int fromBackpack = player.bag.DecItem(_itemValue, remaining, _ignoreModdedItems, _removedItems);
                        if (fromBackpack > 0)
                        {
                            __result += fromBackpack;
                            remaining -= fromBackpack;
                        }
                    }

                    if (remaining <= 0)
                        return;

                    // 2. Consume from nearby storages in priority order
                    int removed = ContainerHelper.RemoveNearbyItems(
                        new Vector3i(player.position), config.CraftingDistance, config.AllowLockedContainers,
                        _itemValue, remaining, _removedItems);

                    if (removed > 0)
                    {
                        __result += removed;
                    }
                }
                catch (Exception ex)
                {
                    Log.Warning($"[ProxiStack] Inventory.DecItem patch: {ex.Message}");
                }
            }
        }

        // ═══════════════════════════════════════════════════════════════
        // PATCH 6b: Verbrauch im Arbeitsstation-Fenster
        // ═══════════════════════════════════════════════════════════════
        // Beleg (Assembly-CSharp v3.3.0): ItemActionEntryCraft.OnActivated verbraucht
        // die Rezeptzutaten im Arbeitsstation-Fenster AUSSCHLIESSLICH über
        // XUiC_WorkstationInputGrid.RemoveItems(...) -> DecItem(...) (nur Eingabe-/
        // Materialfach). Ein Fehlbetrag wird dort stillschweigend ignoriert, und
        // XUiM_PlayerInventory.RemoveItems (Rucksack/Gürtel) wird in diesem Fenster
        // gar nicht aufgerufen. Ohne diesen Patch zählt ProxiStack die Zutaten
        // (Kisten, Fahrzeuge, Drohnen, Ausgabefach der Station) als vorhanden,
        // verbraucht sie aber nie — Craften wäre kostenlos.
        // XUiC_WorkstationMaterialInputGrid (Schmiede) erbt diese Methode und wird
        // damit mit abgedeckt.
        [HarmonyPatch(typeof(XUiC_WorkstationInputGrid), "DecItem", new Type[] { typeof(ItemValue), typeof(int), typeof(IList<ItemStack>) })]
        private class Patch_WorkstationInputGrid_DecItem
        {
            public static void Postfix(ItemValue _itemValue, int _count, IList<ItemStack> _removedItems, ref int __result)
            {
                try
                {
                    var config = ProxiStackMod.Instance?.Config;
                    if (config == null || !config.EnableForCrafting)
                        return;

                    // Upgrade/Repair laufen über Bag/Inventory, nicht über die Grids.
                    if (ActionState.IsUpgradeActive || ActionState.IsRepairActive)
                        return;

                    if (_itemValue == null || _itemValue.IsEmpty() || _count <= 0)
                        return;

                    if (!IsModSafeForMultiplayer())
                        return;

                    int remaining = _count - __result;
                    if (remaining <= 0)
                        return;

                    EntityPlayerLocal player = GameManager.Instance?.World?.GetPrimaryPlayer();
                    if (player == null)
                        return;

                    int removed = ContainerHelper.RemoveNearbyItems(
                        new Vector3i(player.position), config.CraftingDistance, config.AllowLockedContainers,
                        _itemValue, remaining, _removedItems);

                    if (removed > 0)
                    {
                        __result += removed;
                    }
                }
                catch (Exception ex)
                {
                    Log.Warning($"[ProxiStack] WorkstationInputGrid.DecItem patch: {ex.Message}");
                }
            }
        }

        // ═══════════════════════════════════════════════════════════════
        // PATCHES FÜR UPGRADE & REPAIR STATE / HINZUFÜGUNG ZUR ANZEIGE
        // ═══════════════════════════════════════════════════════════════
        [HarmonyPatch(typeof(ItemActionRepair), "CanRemoveRequiredResource")]
        private class Patch_ItemActionRepair_CanRemoveRequiredResource
        {
            public static void Prefix(BlockValue blockValue)
            {
                if (blockValue.damage > 0)
                    ActionState.IsRepairActive = true;
                else
                    ActionState.IsUpgradeActive = true;
            }

            public static void Postfix()
            {
                ActionState.IsRepairActive = false;
                ActionState.IsUpgradeActive = false;
            }
        }

        [HarmonyPatch(typeof(ItemActionRepair), "RemoveRequiredResource")]
        private class Patch_ItemActionRepair_RemoveRequiredResource
        {
            public static void Prefix(BlockValue blockValue)
            {
                if (blockValue.damage > 0)
                    ActionState.IsRepairActive = true;
                else
                    ActionState.IsUpgradeActive = true;
            }

            public static void Postfix()
            {
                ActionState.IsRepairActive = false;
                ActionState.IsUpgradeActive = false;
            }
        }

        [HarmonyPatch(typeof(ItemActionRepair), "canRemoveRequiredItem")]
        private class Patch_ItemActionRepair_canRemoveRequiredItem
        {
            public static void Prefix()
            {
                ActionState.IsRepairActive = true;
            }

            public static void Postfix()
            {
                ActionState.IsRepairActive = false;
            }
        }

        [HarmonyPatch(typeof(ItemActionRepair), "removeRequiredItem")]
        private class Patch_ItemActionRepair_removeRequiredItem
        {
            public static void Prefix()
            {
                ActionState.IsRepairActive = true;
            }

            public static void Postfix()
            {
                ActionState.IsRepairActive = false;
            }
        }

        [HarmonyPatch(typeof(Bag), "GetItemCount", new Type[] { typeof(ItemValue), typeof(int), typeof(int), typeof(bool) })]
        private class Patch_Bag_GetItemCount
        {
            public static void Postfix(Bag __instance, ItemValue _itemValue, ref int __result)
            {
                try
                {
                    var config = ProxiStackMod.Instance?.Config;
                    if (config == null) return;

                    bool isEnabled = false;
                    if (ActionState.IsUpgradeActive) isEnabled = config.EnableForUpgrade;
                    else if (ActionState.IsRepairActive) isEnabled = config.EnableForRepair;

                    if (!isEnabled) return;

                    // Multiplayer-Sicherheit
                    if (!IsModSafeForMultiplayer()) return;

                    EntityAlive player = GetPlayerByBag(__instance);
                    if (player == null) return;

                    int containerCount = ContainerHelper.GetNearbyItemCount(
                        new Vector3i(player.position), config.CraftingDistance, config.AllowLockedContainers, _itemValue);

                    if (containerCount > 0)
                    {
                        __result += containerCount;
                    }
                }
                catch (Exception ex)
                {
                    Log.Warning($"[ProxiStack] Bag.GetItemCount patch: {ex.Message}");
                }
            }
        }

        // ═══════════════════════════════════════════════════════════════
        // PATCH 7: UI-Anzeige – Rezeptliste mit Container-Items füttern
        // ═══════════════════════════════════════════════════════════════
        [HarmonyPatch(typeof(XUiC_RecipeList), "BuildRecipeInfosList")]
        private class Patch_RecipeList_BuildRecipeInfosList
        {
            public static void Prefix(ref List<ItemStack> _items)
            {
                try
                {
                    var config = ProxiStackMod.Instance?.Config;
                    if (config == null || !config.EnableForCrafting)
                        return;

                    EntityAlive ea = GameManager.Instance?.World?.GetPrimaryPlayer();
                    if (ea == null)
                        return;

                    var center = new Vector3i(ea.position);
                    List<ItemStack> nearby = ContainerHelper.GetNearbyCraftingItems(center, config.CraftingDistance, config.AllowLockedContainers);
                    if (nearby.Count == 0)
                        return;

                    var newList = new List<ItemStack>();
                    if (_items != null)
                    {
                        foreach (var stack in _items)
                        {
                            if (stack != null)
                                newList.Add(new ItemStack(stack.itemValue.Clone(), stack.count));
                        }
                    }

                    foreach (var stack in nearby)
                    {
                        if (stack == null || stack.IsEmpty())
                            continue;
                        bool merged = false;
                        for (int i = 0; i < newList.Count; i++)
                        {
                            if (newList[i] != null && !newList[i].IsEmpty() &&
                                newList[i].itemValue != null && newList[i].itemValue.EqualsForMerging(stack.itemValue))
                            {
                                newList[i].count += stack.count;
                                merged = true;
                                break;
                            }
                        }
                        if (!merged)
                            newList.Add(new ItemStack(stack.itemValue.Clone(), stack.count));
                    }
                    _items = newList;
                }
                catch (Exception ex)
                {
                    Log.Warning($"[ProxiStack] RecipeList patch: {ex.Message}");
                }
            }
        }


        // ═══════════════════════════════════════════════════════════════
        // PATCH 9: XUiC_RecipeCraftCount.calcMaxCraftable – Mengenauswahl aktivieren
        // ═══════════════════════════════════════════════════════════════
        [HarmonyPatch(typeof(XUiC_RecipeCraftCount), "calcMaxCraftable")]
        private class Patch_RecipeCraftCount_calcMaxCraftable
        {
            public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
            {
                var codes = new List<CodeInstruction>(instructions);
                bool found = false;
                for (int i = 0; i < codes.Count; i++)
                {
                    if (codes[i].opcode == OpCodes.Callvirt &&
                        codes[i].operand is MethodInfo method &&
                        method.Name == "GetAllItemStacks")
                    {
                        for (int j = i + 1; j < Math.Min(i + 6, codes.Count); j++)
                        {
                            if (codes[j].operand is MethodInfo subMethod && subMethod.Name == "ToArray")
                            {
                                var injectMethod = AccessTools.Method(typeof(Patch_RecipeCraftCount_calcMaxCraftable), nameof(AddContainerItemsArray));
                                codes.Insert(j + 1, new CodeInstruction(OpCodes.Call, injectMethod));
                                found = true;
                                break;
                            }
                        }
                        if (found) break;
                    }
                }
                if (!found)
                {
                    Log.Warning("[ProxiStack] calcMaxCraftable Transpiler could not find injection point!");
                }
                else
                {
                    Log.Out("[ProxiStack] calcMaxCraftable Transpiler successfully patched!");
                }
                return codes;
            }

            public static ItemStack[] AddContainerItemsArray(ItemStack[] playerItems)
            {
                try
                {
                    var config = ProxiStackMod.Instance?.Config;
                    if (config == null || !config.EnableForCrafting)
                        return playerItems;

                    EntityPlayerLocal player = GameManager.Instance?.World?.GetPrimaryPlayer();
                    if (player == null)
                        return playerItems;

                    if (!IsModSafeForMultiplayer())
                        return playerItems;

                    List<ItemStack> nearby = ContainerHelper.GetNearbyCraftingItems(
                        new Vector3i(player.position), config.CraftingDistance, config.AllowLockedContainers);

                    if (nearby.Count == 0)
                        return playerItems;

                    var combined = new List<ItemStack>();
                    if (playerItems != null)
                    {
                        foreach (var original in playerItems)
                        {
                            if (original != null)
                                combined.Add(new ItemStack(original.itemValue.Clone(), original.count));
                        }
                    }

                    foreach (var stack in nearby)
                    {
                        if (stack == null || stack.IsEmpty())
                            continue;
                        bool merged = false;
                        for (int i = 0; i < combined.Count; i++)
                        {
                            if (combined[i] != null && !combined[i].IsEmpty() &&
                                combined[i].itemValue != null && combined[i].itemValue.EqualsForMerging(stack.itemValue))
                            {
                                combined[i].count += stack.count;
                                merged = true;
                                break;
                            }
                        }
                        if (!merged)
                            combined.Add(new ItemStack(stack.itemValue.Clone(), stack.count));
                    }
                    return combined.ToArray();
                }
                catch (Exception ex)
                {
                    Log.Warning($"[ProxiStack] calcMaxCraftable helper error: {ex.Message}");
                    return playerItems;
                }
            }
        }

        // ═══════════════════════════════════════════════════════════════
        // PATCH 10: ItemActionEntryCraft.hasItems – Kochen-Button aktivieren
        // ═══════════════════════════════════════════════════════════════
        [HarmonyPatch(typeof(ItemActionEntryCraft), "hasItems")]
        private class Patch_ItemActionEntryCraft_hasItems
        {
            public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
            {
                var codes = new List<CodeInstruction>(instructions);
                bool found = false;
                for (int i = 0; i < codes.Count; i++)
                {
                    if (codes[i].opcode == OpCodes.Callvirt &&
                        codes[i].operand is MethodInfo method &&
                        method.Name == "GetAllItemStacks")
                    {
                        var injectMethod = AccessTools.Method(typeof(Patch_ItemActionEntryCraft_hasItems), nameof(AddContainerItemsList));
                        codes.Insert(i + 1, new CodeInstruction(OpCodes.Call, injectMethod));
                        found = true;
                        break;
                    }
                }
                if (!found)
                {
                    Log.Warning("[ProxiStack] ItemActionEntryCraft.hasItems Transpiler could not find injection point!");
                }
                else
                {
                    Log.Out("[ProxiStack] ItemActionEntryCraft.hasItems Transpiler successfully patched!");
                }
                return codes;
            }

            public static List<ItemStack> AddContainerItemsList(List<ItemStack> playerItems)
            {
                try
                {
                    var config = ProxiStackMod.Instance?.Config;
                    if (config == null || !config.EnableForCrafting)
                        return playerItems;

                    EntityPlayerLocal player = GameManager.Instance?.World?.GetPrimaryPlayer();
                    if (player == null)
                        return playerItems;

                    if (!IsModSafeForMultiplayer())
                        return playerItems;

                    List<ItemStack> nearby = ContainerHelper.GetNearbyCraftingItems(
                        new Vector3i(player.position), config.CraftingDistance, config.AllowLockedContainers);

                    if (nearby.Count == 0)
                        return playerItems;

                    var combined = new List<ItemStack>();
                    if (playerItems != null)
                    {
                        foreach (var original in playerItems)
                        {
                            if (original != null)
                                combined.Add(new ItemStack(original.itemValue.Clone(), original.count));
                        }
                    }

                    foreach (var stack in nearby)
                    {
                        if (stack == null || stack.IsEmpty())
                            continue;
                        bool merged = false;
                        for (int i = 0; i < combined.Count; i++)
                        {
                            if (combined[i] != null && !combined[i].IsEmpty() &&
                                combined[i].itemValue != null && combined[i].itemValue.EqualsForMerging(stack.itemValue))
                            {
                                combined[i].count += stack.count;
                                merged = true;
                                break;
                            }
                        }
                        if (!merged)
                            combined.Add(new ItemStack(stack.itemValue.Clone(), stack.count));
                    }
                    return combined;
                }
                catch (Exception ex)
                {
                    Log.Warning($"[ProxiStack] ItemActionEntryCraft.hasItems helper error: {ex.Message}");
                    return playerItems;
                }
            }
        }

        // ═══════════════════════════════════════════════════════════════
        // PATCH 10b: Ausgabefach-Ansicht nach dem Craft-Klick nachziehen
        // ═══════════════════════════════════════════════════════════════
        // Vanilla überträgt in XUiC_WorkstationWindowGroup.syncTEfromUI() die UI-Slots der
        // Station zurück ins TileEntity — u. a. unmittelbar beim Start eines Craft-Vorgangs
        // (Update: wasCrafting != craftingQueue.IsCrafting()). Verbraucht ProxiStack Zutaten
        // aus dem Ausgabefach, wird diese TE-Änderung ohne Resync vom alten UI-Stand
        // überschrieben: Zutaten bleiben sichtbar, Craften ist unendlich oft möglich
        // (Logbeleg 11.10.2026).
        // Der Zugriff auf das Fenster erfolgt über den Craft-Klick selbst
        // (craftCountControl.WindowGroup.Controller) — das ist der einzige im Livetest
        // verlässlich auflösbare Weg; XUi.CurrentWorkstationOutputGrid war auch bei offenem
        // Fenster null und GetChildrenByType<XUiC_CraftingWindowGroup>() fand nichts.
        [HarmonyPatch(typeof(ItemActionEntryCraft), "OnActivated")]
        private class Patch_ItemActionEntryCraft_OnActivated
        {
            public static void Prefix(ItemActionEntryCraft __instance)
            {
                try
                {
                    ActiveStationWindow = ResolveStationWindow(__instance);
                }
                catch (Exception ex)
                {
                    Log.Warning($"[ProxiStack] OnActivated Prefix (Stationsfenster): {ex.Message}");
                }
            }

            public static void Postfix()
            {
                try
                {
                    // Sicherheitsnetz: falls der Verbrauch über einen Pfad lief, der das
                    // Ausgabefach nicht selbst nachgezogen hat (z. B. anderes Zutaten-Item).
                    XUiC_WorkstationWindowGroup wsGroup = ActiveStationWindow;
                    if (wsGroup != null)
                        ContainerHelper.RefreshWorkstationOutputGrid(wsGroup);
                }
                catch (Exception ex)
                {
                    Log.Warning($"[ProxiStack] OnActivated Postfix (Ausgabefach-Resync): {ex.Message}");
                }
                finally
                {
                    ActiveStationWindow = null;
                }
            }

            /// <summary>Offenes Stationsfenster des Craft-Klicks oder null (Rucksack-Crafting).</summary>
            internal static XUiC_WorkstationWindowGroup ResolveStationWindow(ItemActionEntryCraft entry)
            {
                if (entry == null) return null;
                XUiC_RecipeCraftCount counter = entry.craftCountControl;
                if (counter == null || counter.WindowGroup == null) return null;
                return counter.WindowGroup.Controller as XUiC_WorkstationWindowGroup;
            }
        }

        // ═══════════════════════════════════════════════════════════════
        // HILFSMETHODEN
        // ═══════════════════════════════════════════════════════════════

        private static bool IsModSafeForMultiplayer()
        {
            return true;
        }

        /// <summary>
        /// Findet den Spieler/die Entity, dessen Bag-Instanz mit der übergebenen übereinstimmt.
        /// Im Multiplayer ist ein Client auf dem Host nicht zwingend EntityPlayerLocal – GetPrimaryPlayer()
        /// und ein EntityPlayerLocal-Cast reichen deshalb nicht.
        /// </summary>
        private static EntityAlive GetPlayerByBag(Bag bag)
        {
            if (bag == null) return null;
            World world = GameManager.Instance?.World;
            if (world == null) return null;
            for (int i = 0; i < world.Players.list.Count; i++)
            {
                EntityAlive player = world.Players.list[i] as EntityAlive;
                if (player != null && player.bag == bag)
                    return player;
            }
            return null;
        }

        /// <summary>
        /// Findet den Spieler/die Entity, dessen Inventory-Instanz mit der übergebenen übereinstimmt.
        /// </summary>
        private static EntityAlive GetPlayerByInventory(Inventory inventory)
        {
            if (inventory == null) return null;
            World world = GameManager.Instance?.World;
            if (world == null) return null;
            for (int i = 0; i < world.Players.list.Count; i++)
            {
                EntityAlive player = world.Players.list[i] as EntityAlive;
                if (player != null && player.inventory == inventory)
                    return player;
            }
            return null;
        }

        private static int GetItemStackCount(IList<ItemStack> _itemStack, ItemValue _itemValue)
        {
            if (_itemStack == null)
                return 0;
            int count = 0;
            for (int i = 0; i < _itemStack.Count; i++)
            {
                ItemStack stack = _itemStack[i];
                if (stack != null && !stack.IsEmpty() && stack.itemValue != null && stack.itemValue.EqualsForMerging(_itemValue))
                {
                    count += stack.count;
                }
            }
            return count;
        }

        // Patch für NetPackageManager.GetPackageId um Mappings sicher über das packageIdToClass Array aufzulösen
        [HarmonyPatch(typeof(NetPackageManager), "GetPackageId")]
        private class Patch_NetPackageManager_GetPackageId
        {
            public static bool Prefix(Type _type, ref int __result)
            {
                try
                {
                    if (_type == typeof(NetPackageProxiStack))
                    {
                        var field = typeof(NetPackageManager).GetField("packageIdToClass", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
                        if (field != null)
                        {
                            Type[] mappings = field.GetValue(null) as Type[];
                            if (mappings != null)
                            {
                                for (int i = 0; i < mappings.Length; i++)
                                {
                                    if (mappings[i] == typeof(NetPackageProxiStack))
                                    {
                                        __result = i;
                                        Log.Out($"[ProxiStack] GetPackageId redirected from packageClassToPackageId to array index {i} for NetPackageProxiStack");
                                        return false; // Überspringe die Originalmethode
                                    }
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Log.Warning($"[ProxiStack] Error in GetPackageId prefix patch: {ex}");
                }
                return true; // Führe Originalmethode aus
            }
        }

        [HarmonyPatch(typeof(ItemActionRanged), nameof(ItemActionRanged.CanReload))]
        public static class Patch_ItemActionRanged_CanReload
        {
            public static void Postfix(ItemActionRanged __instance, ItemActionData _actionData, ref bool __result)
            {
                if (__result) return;

                var config = ProxiStackMod.Instance?.Config;
                if (config == null || !config.EnableForReload)
                {
                    return;
                }

                if (GameManager.Instance.World == null) return;
                EntityPlayerLocal player = GameManager.Instance.World.GetPrimaryPlayer();
                if (player == null)
                {
                    return;
                }

                try
                {
                    var holdingItemItemValue = _actionData.invData.holdingEntity.inventory.holdingItemItemValue;
                    
                    if (__instance.MagazineItemNames == null)
                    {
                        return;
                    }
                    

                    int magazineSize = (int)EffectManager.GetValue(PassiveEffects.MagazineSize, holdingItemItemValue, __instance.BulletsPerMagazine, _actionData.invData.holdingEntity);
                    int currentAmmo = _actionData.invData.itemValue.Meta;
                    bool isJammed = __instance.isJammed(holdingItemItemValue);

                    if (currentAmmo >= magazineSize && !isJammed)
                    {
                        return;
                    }

                    // Check if selected ammo type is available (in inventory + containers)
                    var selectedAmmo = ItemClass.GetItem(__instance.MagazineItemNames[holdingItemItemValue.SelectedAmmoTypeIndex]);
                    int selectedCount = player.bag.GetItemCount(selectedAmmo) + player.inventory.GetItemCount(selectedAmmo);
                    selectedCount += ContainerHelper.GetNearbyItemCount(
                        new Vector3i(player.position), config.CraftingDistance, config.AllowLockedContainers, selectedAmmo);


                    if (selectedCount > 0)
                    {
                        __result = true;
                        return;
                    }

                    // Current selected ammo is empty! Look for any compatible ammo type that is available
                    bool foundAny = false;
                    for (int i = 0; i < __instance.MagazineItemNames.Length; i++)
                    {
                        var loopAmmo = ItemClass.GetItem(__instance.MagazineItemNames[i]);
                        int count = player.bag.GetItemCount(loopAmmo) + player.inventory.GetItemCount(loopAmmo);
                        count += ContainerHelper.GetNearbyItemCount(
                            new Vector3i(player.position), config.CraftingDistance, config.AllowLockedContainers, loopAmmo);

                        if (count > 0)
                        {
                            // Switch weapon's ammo type index to this available type!
                            holdingItemItemValue.SelectedAmmoTypeIndex = (byte)i;
                            foundAny = true;
                            break;
                        }
                    }

                    if (foundAny)
                    {
                        __result = true;
                    }
                }
                catch (Exception ex)
                {
                    Log.Warning($"[ProxiStack] Error in Patch_ItemActionRanged_CanReload: {ex}");
                }
            }
        }

        [HarmonyPatch(typeof(AnimatorRangedReloadState), "GetAmmoCount")]
        public static class Patch_AnimatorRangedReloadState_GetAmmoCount
        {
            public static bool Prefix(AnimatorRangedReloadState __instance, EntityAlive ea, ItemValue ammo, int modifiedMagazineSize, ref int __result)
            {
                
                var config = ProxiStackMod.Instance?.Config;
                if (config == null || !config.EnableForReload) return true; 

                if (!(ea is EntityPlayerLocal player)) return true;

                try
                {
                    int currentAmmo = player.inventory.holdingItemItemValue.Meta;
                    int needed = modifiedMagazineSize - currentAmmo;

                    if (needed <= 0)
                    {
                        __result = 0;
                        return false;
                    }

                    int totalRemoved = 0;

                    // 1. Aus dem Rucksack (bag) abziehen
                    int removedFromBag = player.bag.DecItem(ammo, needed);
                    totalRemoved += removedFromBag;
                    needed -= removedFromBag;

                    // 2. Aus dem Gürtel (inventory / toolbelt) abziehen
                    if (needed > 0)
                    {
                        int removedFromBelt = player.inventory.DecItem(ammo, needed);
                        totalRemoved += removedFromBelt;
                        needed -= removedFromBelt;
                    }

                    // 3. Aus nahegelegenen Kisten/Drohnen abziehen
                    if (needed > 0)
                    {
                        int removedFromContainers = ContainerHelper.RemoveNearbyItems(
                            new Vector3i(player.position), config.CraftingDistance, config.AllowLockedContainers,
                            ammo, needed);
                        totalRemoved += removedFromContainers;
                        needed -= removedFromContainers;
                    }

                    __result = totalRemoved;
                    return false; // Überspringe Vanilla komplett!
                }
                catch (Exception ex)
                {
                    Log.Warning($"[ProxiStack] Error in AnimatorRangedReloadState.GetAmmoCount Prefix: {ex}");
                    return true; // Im Fehlerfall Vanilla ausführen als Fallback
                }
            }
        }

        [HarmonyPatch(typeof(Animator3PRangedReloadState), "GetAmmoCount")]
        public static class Patch_Animator3PRangedReloadState_GetAmmoCount
        {
            public static bool Prefix(Animator3PRangedReloadState __instance, EntityAlive ea, ItemValue ammo, int modifiedMagazineSize, ref int __result)
            {
                
                var config = ProxiStackMod.Instance?.Config;
                if (config == null || !config.EnableForReload) return true; 

                if (!(ea is EntityPlayerLocal player)) return true;

                try
                {
                    int currentAmmo = player.inventory.holdingItemItemValue.Meta;
                    int needed = modifiedMagazineSize - currentAmmo;

                    if (needed <= 0)
                    {
                        __result = 0;
                        return false;
                    }

                    int totalRemoved = 0;

                    int removedFromBag = player.bag.DecItem(ammo, needed);
                    totalRemoved += removedFromBag;
                    needed -= removedFromBag;

                    if (needed > 0)
                    {
                        int removedFromBelt = player.inventory.DecItem(ammo, needed);
                        totalRemoved += removedFromBelt;
                        needed -= removedFromBelt;
                    }

                    if (needed > 0)
                    {
                        int removedFromContainers = ContainerHelper.RemoveNearbyItems(
                            new Vector3i(player.position), config.CraftingDistance, config.AllowLockedContainers,
                            ammo, needed);
                        totalRemoved += removedFromContainers;
                        needed -= removedFromContainers;
                        
                        if (removedFromContainers > 0)
                        {
                        }
                    }

                    __result = totalRemoved;
                    return false; // Überspringe Vanilla komplett!
                }
                catch (Exception ex)
                {
                    Log.Warning($"[ProxiStack] Error in Animator3PRangedReloadState.GetAmmoCount Prefix: {ex}");
                    return true;
                }
            }
        }

        [HarmonyPatch(typeof(XUiM_PlayerInventory), "GetItemCount", new Type[] { typeof(int) })]
        public static class Patch_PlayerInventory_GetItemCount_Int
        {
            public static void Postfix(XUiM_PlayerInventory __instance, int _itemId, ref int __result)
            {
                var config = ProxiStackMod.Instance?.Config;
                if (config == null) return;

                if (GameManager.Instance.World == null) return;
                EntityPlayerLocal player = GameManager.Instance.World.GetPrimaryPlayer();
                if (player == null) return;

                try
                {
                    ItemValue iv = new ItemValue(_itemId);
                    int count = ContainerHelper.GetNearbyItemCount(new Vector3i(player.position), config.CraftingDistance, config.AllowLockedContainers, iv);
                    __result += count;
                }
                catch (Exception ex)
                {
                    Log.Warning($"[ProxiStack] Error in Patch_PlayerInventory_GetItemCount_Int: {ex}");
                }
            }
        }

        [HarmonyPatch(typeof(XUiC_HUDStatBar), "updateActiveItemAmmo")]
        public static class Patch_XUiC_HUDStatBar_updateActiveItemAmmo
        {
            private static FieldInfo _activeAmmoField;
            private static FieldInfo _ammoCountField;
            private static bool _fieldsInitialized;

            public static void Postfix(XUiC_HUDStatBar __instance)
            {
                var config = ProxiStackMod.Instance?.Config;
                if (config == null || !config.EnableForReload) return;

                if (GameManager.Instance.World == null) return;
                EntityPlayerLocal player = GameManager.Instance.World.GetPrimaryPlayer();
                if (player == null) return;

                try
                {
                    if (!_fieldsInitialized)
                    {
                        _fieldsInitialized = true;
                        _activeAmmoField = typeof(XUiC_HUDStatBar).GetField("activeAmmoItemValue", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                        _ammoCountField = typeof(XUiC_HUDStatBar).GetField("currentAmmoCount", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    }

                    if (_activeAmmoField == null || _ammoCountField == null) return;

                    var activeAmmo = _activeAmmoField.GetValue(__instance) as ItemValue;
                    if (activeAmmo == null || activeAmmo.type == 0) return;

                    int currentCount = (int)_ammoCountField.GetValue(__instance);
                    int containerCount = ContainerHelper.GetNearbyItemCount(
                        new Vector3i(player.position), config.CraftingDistance, config.AllowLockedContainers, activeAmmo);

                    if (containerCount > 0)
                    {
                        _ammoCountField.SetValue(__instance, currentCount + containerCount);
                    }
                }
                catch (Exception ex)
                {
                    Log.Warning($"[ProxiStack] Error in Patch_XUiC_HUDStatBar_updateActiveItemAmmo: {ex}");
                }
            }
        }

        // Guard only: vehicle refueling must use vanilla inventory fuel and must not
        // fall through to ProxiStack's generic DecItem container fallback.
        [HarmonyPatch(typeof(EntityVehicle), "takeFuel")]
        private class Patch_EntityVehicle_takeFuel
        {
            public static void Prefix()
            {
                ActionState.IsVehicleRefuelActive = true;
            }

            public static void Postfix()
            {
                ActionState.IsVehicleRefuelActive = false;
            }
        }

        [HarmonyPatch(typeof(XUiM_Vehicle), "RepairVehicle")]
        private class Patch_XUiM_Vehicle_RepairVehicle
        {
            public static void Prefix()
            {
                var config = ProxiStackMod.Instance?.Config;
                if (config == null || !config.EnableForRepair) return;
                ActionState.IsRepairActive = true;
            }

            public static void Postfix()
            {
                ActionState.IsRepairActive = false;
            }
        }

        [HarmonyPatch(typeof(ItemActionTextureBlock), "checkAmmo")]
        private class Patch_ItemActionTextureBlock_checkAmmo
        {
            public static bool Prefix(ItemActionTextureBlock __instance, ItemActionData _actionData, ref bool __result)
            {
                var config = ProxiStackMod.Instance?.Config;
                if (config == null || !config.EnableForPainting) return true;

                try
                {
                    if (__instance.InfiniteAmmo ||
                        GameStats.GetInt(EnumGameStats.GameModeId) == 2 ||
                        GameStats.GetInt(EnumGameStats.GameModeId) == 8)
                    {
                        __result = true;
                        return false;
                    }

                    ItemValue paintItem = __instance.currentMagazineItem;
                    if (paintItem == null || paintItem.IsEmpty()) return true;

                    EntityAlive holdingEntity = _actionData?.invData?.holdingEntity;
                    if (holdingEntity == null) return true;

                    int bagCount = holdingEntity.bag?.GetItemCount(paintItem, -1, -1, true) ?? 0;
                    int inventoryCount = holdingEntity.inventory?.GetItemCount(paintItem, false, -1, -1, true) ?? 0;
                    if (bagCount + inventoryCount > 0)
                    {
                        __result = true;
                        return false;
                    }

                    int containerCount = ContainerHelper.GetNearbyItemCount(
                        new Vector3i(holdingEntity.position), config.CraftingDistance, config.AllowLockedContainers, paintItem);

                    __result = containerCount > 0;
                    return false;
                }
                catch (Exception ex)
                {
                    Log.Warning($"[ProxiStack] Error in Patch_ItemActionTextureBlock_checkAmmo: {ex.Message}");
                    return true;
                }
            }
        }

        // ═══════════════════════════════════════════════════════════════
        // PATCH: Challenge-Tracker – Quest-Fortschritt mit Container-Items boosten
        // ═══════════════════════════════════════════════════════════════
        // BaseChallengeObjective.HandleUpdatingCurrent wird vom Spiel aufgerufen,
        // um den aktuellen Fortschritt (Current) von Challenges/Quests zu ermitteln.
        // Für Gather/GatherByTag/GatherIngredient/Harvest-Objectives zählen wir
        // Items aus nahen Containern mit, damit z. B. "Sammle 100 Holz" auch
        // Items aus Kisten erkennt.
        [HarmonyPatch(typeof(BaseChallengeObjective), "HandleUpdatingCurrent")]
        private class Patch_BaseChallengeObjective_HandleUpdatingCurrent
        {
            private static FieldInfo _expectedItemField;
            private static PropertyInfo _currentProperty;

            public static void Postfix(BaseChallengeObjective __instance)
            {
                try
                {
                    var config = ProxiStackMod.Instance?.Config;
                    if (config == null || !config.EnableForQuests)
                        return;

                    EntityPlayerLocal player = GameManager.Instance?.World?.GetPrimaryPlayer();
                    if (player == null)
                        return;

                    if (!IsModSafeForMultiplayer())
                        return;

                    // Nur für TrackedItemObjectives (Gather, GatherByTag, GatherIngredient, Harvest)
                    var trackedItemObj = __instance as ChallengeBaseTrackedItemObjective;
                    if (trackedItemObj == null)
                        return;

                    // expectedItem Feld per Reflection holen (protected, in ChallengeBaseTrackedItemObjective)
                    if (_expectedItemField == null)
                    {
                        _expectedItemField = typeof(ChallengeBaseTrackedItemObjective).GetField("expectedItem",
                            BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public | BindingFlags.FlattenHierarchy);
                        if (_expectedItemField == null)
                        {
                            Log.Warning("[ProxiStack] Challenge patch: could not find expectedItem field");
                            return;
                        }
                    }

                    ItemValue expectedItem = _expectedItemField.GetValue(trackedItemObj) as ItemValue;
                    if (expectedItem == null || expectedItem.IsEmpty())
                        return;

                    int containerCount = ContainerHelper.GetNearbyItemCount(
                        new Vector3i(player.position), config.CraftingDistance, config.AllowLockedContainers, expectedItem);

                    if (containerCount <= 0)
                        return;

                    // Current-Property boosten (nur wenn noch nicht am Maximum)
                    if (_currentProperty == null)
                    {
                        _currentProperty = typeof(BaseChallengeObjective).GetProperty("Current",
                            BindingFlags.Public | BindingFlags.Instance);
                        if (_currentProperty == null)
                        {
                            Log.Warning("[ProxiStack] Challenge patch: could not find Current property");
                            return;
                        }
                    }

                    int currentBefore = (int)_currentProperty.GetValue(__instance);
                    int newCurrent = Math.Min(currentBefore + containerCount, MaxCountOf(__instance));

                    if (newCurrent != currentBefore)
                        _currentProperty.SetValue(__instance, newCurrent);
                }
                catch (Exception ex)
                {
                    Log.Warning($"[ProxiStack] Challenge HandleUpdatingCurrent patch: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Liest MaxCount der Challenge-Objective. Das FieldInfo wird nur einmal per
        /// Reflection gesucht (dieser Pfad laeuft sehr haeufig — vorher wurde die
        /// Suche bei jedem Aufruf wiederholt und jedes Ergebnis mit Log.Out geschrieben,
        /// was das Client-Log mit ~200k Zeilen pro Sitzung flutete).
        /// </summary>
        private static FieldInfo _maxCountField;
        private static bool _maxCountFieldResolved;

        private static int MaxCountOf(BaseChallengeObjective objective)
        {
            if (!_maxCountFieldResolved)
            {
                _maxCountField = typeof(BaseChallengeObjective).GetField("MaxCount",
                    BindingFlags.Public | BindingFlags.Instance);
                _maxCountFieldResolved = true;
            }
            if (_maxCountField == null)
                return int.MaxValue;

            object val = _maxCountField.GetValue(objective);
            return (val is int intVal) ? intVal : int.MaxValue;
        }

        [HarmonyPatch(typeof(ItemActionTextureBlock), "decreaseAmmo")]
        private class Patch_ItemActionTextureBlock_decreaseAmmo
        {
            public static bool Prefix(ItemActionTextureBlock __instance, ItemActionData _actionData, ref bool __result)
            {
                var config = ProxiStackMod.Instance?.Config;
                if (config == null || !config.EnableForPainting) return true;

                try
                {
                    if (__instance.InfiniteAmmo ||
                        GameStats.GetInt(EnumGameStats.GameModeId) == 2 ||
                        GameStats.GetInt(EnumGameStats.GameModeId) == 8)
                    {
                        __result = true;
                        return false;
                    }

                    var textureBlockData = _actionData as ItemActionTextureBlock.ItemActionTextureBlockData;
                    if (textureBlockData == null) return true;

                    int paintCost = BlockTextureData.list[textureBlockData.idx].PaintCost;

                    EntityAlive holdingEntity = _actionData.invData?.holdingEntity;
                    if (holdingEntity == null) return true;

                    ItemValue paintItem = __instance.currentMagazineItem;
                    if (paintItem == null || paintItem.IsEmpty()) return true;

                    int bagCount = holdingEntity.bag?.GetItemCount(paintItem, -1, -1, true) ?? 0;
                    int inventoryCount = holdingEntity.inventory?.GetItemCount(paintItem, false, -1, -1, true) ?? 0;
                    int entityAvailable = bagCount + inventoryCount;

                    int containerCount = ContainerHelper.GetNearbyItemCount(
                        new Vector3i(holdingEntity.position), config.CraftingDistance, config.AllowLockedContainers, paintItem);

                    int totalAvailable = entityAvailable + containerCount;
                    if (totalAvailable < paintCost)
                    {
                        __result = false;
                        return false;
                    }

                    int remainingNeeded = paintCost;

                    if (remainingNeeded > 0 && holdingEntity.bag != null)
                    {
                        remainingNeeded -= holdingEntity.bag.DecItem(paintItem, remainingNeeded, false, null);
                    }

                    if (remainingNeeded > 0 && holdingEntity.inventory != null)
                    {
                        remainingNeeded -= holdingEntity.inventory.DecItem(paintItem, remainingNeeded, false, null);
                    }

                    if (remainingNeeded > 0)
                    {
                        int removed = ContainerHelper.RemoveNearbyItems(
                            new Vector3i(holdingEntity.position), config.CraftingDistance, config.AllowLockedContainers,
                            paintItem, remainingNeeded, null);
                        remainingNeeded -= removed;
                    }

                    __result = remainingNeeded <= 0;
                    return false;
                }
                catch (Exception ex)
                {
                    Log.Warning($"[ProxiStack] Error in Patch_ItemActionTextureBlock_decreaseAmmo: {ex.Message}");
                    return true;
                }
            }
        }
    }
}

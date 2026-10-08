using System;
using System.Collections.Generic;
using UnityEngine;

namespace ProxiStack
{
    /// <summary>
    /// ProxiStack-Manager (Stash & Restock) – v3.0 kompatibel.
    /// </summary>
    public static class ProxiStackManager
    {
        public static XUiC_BackpackWindow BackpackWindow { get; private set; }
        public static XUiC_Backpack Backpack { get; private set; }
        public static XUiC_ContainerStandardControls ContainerControls { get; private set; }

        private static float[] lastClickTimes = new float[2];

        public static void InitializeUI(XUiC_BackpackWindow window)
        {
            try
            {
                BackpackWindow = window;
                ContainerControls = window.GetChildByType<XUiC_ContainerStandardControls>();
                Backpack = BackpackWindow?.backpackGrid;
                lastClickTimes[0] = 0f;
                lastClickTimes[1] = 0f;
                Log.Out("[ProxiStack] Stash UI initialized");
            }
            catch (Exception ex)
            {
                Log.Warning($"[ProxiStack] UI init error: {ex.Message}");
            }
        }

        public static bool HasMoveDestinationOpen(XUiC_BackpackWindow backpackWindow = null)
        {
            try
            {
                XUiC_BackpackWindow window = backpackWindow ?? BackpackWindow;
                if (window == null) return false;

                IInventory moveDestination;
                return window.TryGetMoveDestinationInventory(out moveDestination);
            }
            catch { return false; }
        }

        private static XUiM_LootContainer.EItemMoveKind GetMoveKind(int typeIndex)
        {
            float now = Time.unscaledTime;
            float last = lastClickTimes[typeIndex];
            lastClickTimes[typeIndex] = now;
            return (now - last < 2.0f)
                ? XUiM_LootContainer.EItemMoveKind.FillAndCreate
                : XUiM_LootContainer.EItemMoveKind.FillOnly;
        }

        // ============================================================
        // STASH (EINSORTIEREN)
        // ============================================================
        public static void DoStash()
        {
            try
            {
                if (HasMoveDestinationOpen()) return;

                if (!ConnectionManager.Instance.IsSinglePlayer && !ConnectionManager.Instance.IsServer)
                {
                    var pkg = NetPackageManager.GetPackage<NetPackageProxiStack>()
                        .Setup(GameManager.Instance.World.GetPrimaryPlayerId(), ProxiStackType.Stack);
                    ConnectionManager.Instance.SendToServer(pkg);
                    return;
                }

                PerformStashLocal();
            }
            catch (Exception ex)
            {
                Log.Warning($"[ProxiStack] DoStash: {ex.Message}");
            }
        }

        public static void PerformStashLocal()
        {
            try
            {
                if (Backpack == null || BackpackWindow == null) return;
                var moveKind = GetMoveKind(0);
                var player = GameManager.Instance.World.GetPrimaryPlayer();
                if (player == null) return;

                var config = ProxiStackMod.Instance.Config;
                var containers = ContainerHelper.FindNearbyContainersStash(
                    new Vector3i(player.position), config.StashDistance);

                foreach (var result in containers)
                {
                    IInventory inv = result.Storage as IInventory;
                    if (inv == null) continue;

                    XUiM_LootContainer.StashItems(
                        BackpackWindow, Backpack, inv,
                        0, ContainerControls?.LockedSlots ?? new PackedBoolArray(0),
                        moveKind, ContainerControls?.MoveStartBottomRight ?? false);
                    result.TileEntity.SetModified();
                }
            }
            catch (Exception ex)
            {
                Log.Warning($"[ProxiStack] Stash local error: {ex.Message}");
            }
        }

        public static void PerformStashClient(Vector3i center, IEnumerable<Vector3i> offsets)
        {
            try
            {
                if (Backpack == null || HasMoveDestinationOpen()) return;
                var moveKind = GetMoveKind(0);

                foreach (var offset in offsets)
                {
                    var result = ContainerHelper.GetLootableAtPosition(center + offset);
                    if (result.TileEntity == null || result.Storage == null) continue;

                    IInventory inv = result.Storage as IInventory;
                    if (inv == null) continue;

                    XUiM_LootContainer.StashItems(
                        BackpackWindow, Backpack, inv,
                        0, ContainerControls?.LockedSlots ?? new PackedBoolArray(0),
                        moveKind, ContainerControls?.MoveStartBottomRight ?? false);
                    result.TileEntity.SetModified();
                }
            }
            catch (Exception ex)
            {
                Log.Warning($"[ProxiStack] Stash client error: {ex.Message}");
            }
        }

        // ============================================================
        // RESTOCK (AUFFÜLLEN)
        // ============================================================
        public static void DoRestock()
        {
            try
            {
                if (HasMoveDestinationOpen()) return;

                if (!ConnectionManager.Instance.IsSinglePlayer && !ConnectionManager.Instance.IsServer)
                {
                    var pkg = NetPackageManager.GetPackage<NetPackageProxiStack>()
                        .Setup(GameManager.Instance.World.GetPrimaryPlayerId(), ProxiStackType.Restock);
                    ConnectionManager.Instance.SendToServer(pkg);
                    return;
                }

                PerformRestockLocal();
            }
            catch (Exception ex)
            {
                Log.Warning($"[ProxiStack] DoRestock: {ex.Message}");
            }
        }

        public static void PerformRestockLocal()
        {
            try
            {
                if (Backpack == null || BackpackWindow == null) return;
                var player = GameManager.Instance.World.GetPrimaryPlayer();
                if (player == null) return;

                var config = ProxiStackMod.Instance.Config;
                var containers = ContainerHelper.FindNearbyContainersStash(
                    new Vector3i(player.position), config.StashDistance);

                foreach (var result in containers)
                {
                    if (FillExistingBackpackStacks(player.bag, result.Items))
                        result.TileEntity.SetModified();
                }
            }
            catch (Exception ex)
            {
                Log.Warning($"[ProxiStack] Restock local error: {ex.Message}");
            }
        }

        public static void PerformRestockClient(Vector3i center, IEnumerable<Vector3i> offsets)
        {
            try
            {
                if (Backpack == null || HasMoveDestinationOpen()) return;

                var player = GameManager.Instance.World.GetPrimaryPlayer();
                if (player == null) return;

                foreach (var offset in offsets)
                {
                    var result = ContainerHelper.GetLootableAtPosition(center + offset);
                    if (result.TileEntity == null || result.Storage == null) continue;

                    if (FillExistingBackpackStacks(player.bag, result.Items))
                        result.TileEntity.SetModified();
                }
            }
            catch (Exception ex)
            {
                Log.Warning($"[ProxiStack] Restock client error: {ex.Message}");
            }
        }

        /// <summary>
        /// Füllt ausschließlich bereits vorhandene, kompatible Rucksack-Stacks auf.
        /// Leere Slots werden nie als Ziel verwendet, sodass Restock keine neuen
        /// Stacks und keine zuvor nicht vorhandenen Gegenstände anlegen kann.
        /// </summary>
        private static bool FillExistingBackpackStacks(Bag bag, ItemStack[] sourceStacks)
        {
            if (bag == null || sourceStacks == null || sourceStacks.Length == 0)
                return false;

            // v3.3: Bag hat kein GetSlots() mehr — Slot-Array über Bag.ItemGrid.items.
            ItemStack[] targetSlots = ContainerHelper.GetBagItems(bag);
            if (targetSlots == null || targetSlots.Length == 0)
                return false;

            bool sourceModified = false;

            for (int targetIndex = 0; targetIndex < targetSlots.Length; targetIndex++)
            {
                ItemStack targetStack = targetSlots[targetIndex];
                if (targetStack == null || targetStack.IsEmpty() || targetStack.itemValue == null)
                    continue;

                bool targetModified = false;

                for (int sourceIndex = 0; sourceIndex < sourceStacks.Length; sourceIndex++)
                {
                    ItemStack sourceStack = sourceStacks[sourceIndex];
                    if (sourceStack == null || sourceStack.IsEmpty() || sourceStack.itemValue == null)
                        continue;

                    if (!targetStack.itemValue.EqualsForMerging(sourceStack.itemValue))
                        continue;

                    int transferCount = targetStack.StackTransferCount(sourceStack);
                    if (transferCount <= 0)
                        continue;

                    targetStack.count += transferCount;
                    sourceStack.count -= transferCount;

                    if (sourceStack.count <= 0)
                        sourceStacks[sourceIndex] = ItemStack.Empty;

                    targetModified = true;
                    sourceModified = true;
                }

                if (targetModified)
                    bag.SetSlot(targetIndex, targetStack);
            }

            return sourceModified;
        }

        // ============================================================
        // SLOT LOCK
        // ============================================================
        public static void ToggleSlotLock(XUiC_ItemStack itemStack)
        {
            try
            {
                if (itemStack == null) return;
                itemStack.UserLockedSlot = !itemStack.UserLockedSlot;
                BackpackWindow?.UpdateLockedSlots(ContainerControls);
                itemStack.xui?.PlayMenuClickSound();
            }
            catch (Exception ex)
            {
                Log.Warning($"[ProxiStack] ToggleSlotLock: {ex.Message}");
            }
        }

        public static void UpdateLockedSlotUI()
        {
            try
            {
                if (Backpack == null) return;
                var config = ProxiStackMod.Instance.Config;
                XUiController[] slots = Backpack.GetItemStackControllers();

                for (int i = 0; i < slots.Length; i++)
                {
                    var iconSlot = slots[i].GetChildById("iconSlotLock")?.ViewComponent as XUiV_Sprite;
                    if (iconSlot != null) iconSlot.Color = config.LockIconColor;
                }

                if (ContainerControls != null)
                {
                    var lockBtn = ContainerControls.GetChildById("btnToggleLockMode")?.ViewComponent;
                    if (lockBtn != null) lockBtn.IsVisible = config.LockModeIconVisible;
                }
            }
            catch (Exception ex)
            {
                Log.Warning($"[ProxiStack] UpdateLockedSlotUI: {ex.Message}");
            }
        }
    }

    public enum ProxiStackType : byte
    {
        Stack = 0,
        Restock = 1
    }
}
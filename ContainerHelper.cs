using System;
using System.Collections.Generic;
using UnityEngine;

namespace ProxiStack
{
    /// <summary>
    /// Einfache Ergebnis-Struktur für Container-Funde.
    /// </summary>
    public struct ContainerResult
    {
        public TileEntityComposite TileEntity;
        /// <summary>
        /// v3.3: ITileEntityLootable existiert nicht mehr. Das Vanilla-Äquivalent ist
        /// die Feature-Komponente TEFeatureStorage (implements IInventory, IItemStackGridHolder).
        /// Der eigentliche Itemspeicher liegt in Storage.ItemGrid.items.
        /// Beleg: Assembly-CSharp v3.3.0 (b18) — TEFeatureStorage : TEFeatureAbs.
        /// </summary>
        public TEFeatureStorage Storage;

        public ContainerResult(TileEntityComposite tileEntity, TEFeatureStorage storage)
        {
            TileEntity = tileEntity;
            Storage = storage;
        }

        /// <summary>ItemStack[] des Containers oder null.</summary>
        public ItemStack[] Items
        {
            get { return ContainerHelper.GetStorageItems(Storage); }
        }
    }

    /// <summary>
    /// Zentrale Helfer-Klasse zum Finden von Containern im Radius.
    /// 7DTD v3.0 API-spezifisch und hochgradig performance-optimiert durch Chunk-Lookup.
    /// Unterstützt dreidimensionale Radien (Vector3i).
    /// </summary>
    public static class ContainerHelper
    {
        private const float CraftingItemsCacheSeconds = 0.25f;
        private static readonly object CraftingItemsCacheLock = new object();
        private static List<ItemStack> CraftingItemsCache = new List<ItemStack>();
        private static Vector3i CraftingItemsCacheCenter = new Vector3i(int.MinValue, int.MinValue, int.MinValue);
        private static Vector3i CraftingItemsCacheRadius = new Vector3i(-1, -1, -1);
        private static bool CraftingItemsCacheIncludeLocked;
        private static float CraftingItemsCacheTime;

        /// <summary>
        /// Liefert das ItemStack[] eines Storage-Features.
        /// v3.3: TEFeatureStorage implementiert IInventory, der Itemspeicher liegt aber
        /// im ItemStackGrid (Storage.ItemGrid.items). Ein "lootable.items"-Property wie
        /// in v3.2 (ITileEntityLootable) existiert nicht mehr.
        /// </summary>
        public static ItemStack[] GetStorageItems(TEFeatureStorage storage)
        {
            if (storage == null) return null;
            ItemStackGrid grid = storage.ItemGrid;
            if (grid == null) return null;
            return grid.items;
        }

        /// <summary>
        /// ItemStackGrid eines Bag (Rucksack/Fahrzeug/Drohne).
        /// v3.3: Bag hat kein public items-Feld mehr; der Slot-Array liegt in
        /// Bag.itemGrid (Property ItemGrid → ItemStackGrid).
        /// </summary>
        public static ItemStackGrid GetBagGrid(Bag bag)
        {
            if (bag == null) return null;
            return bag.ItemGrid;
        }

        /// <summary>ItemStack[] eines Bag oder null.</summary>
        public static ItemStack[] GetBagItems(Bag bag)
        {
            ItemStackGrid grid = GetBagGrid(bag);
            if (grid == null) return null;
            return grid.items;
        }

        /// <summary>
        /// Prüft, ob ein Container als Lagerquelle zählt.
        /// v3.3: bPlayerStorage/bTouched aus ITileEntityLootable heißen jetzt
        /// playerOwned/Touched auf ItemStackGrid (Vanilla nutzt dieselbe Bedingung
        /// in TEFeatureStorage.UpdateTick).
        /// </summary>
        public static bool IsEligibleContainer(TileEntityComposite composite, TEFeatureStorage storage)
        {
            if (composite == null || storage == null) return false;

            ItemStackGrid grid = storage.ItemGrid;
            if (grid == null || grid.items == null) return false;

            if (composite.PlayerPlaced) return true;
            return grid.PlayerOwned || grid.Touched;
        }

        /// <summary>
        /// Findet Container in ProxiStack-Format (kubisch mit Offset) via Chunk-Lookup.
        /// </summary>
        public static List<ContainerResult> FindNearbyContainersStash(Vector3i center, Vector3i distance)
        {
            var results = new List<ContainerResult>();

            try
            {
                World world = GameManager.Instance.World;
                if (world == null || world.ChunkCache == null) return results;

                int minChunkX = (center.x - distance.x) >> 4;
                int maxChunkX = (center.x + distance.x) >> 4;
                int minChunkZ = (center.z - distance.z) >> 4;
                int maxChunkZ = (center.z + distance.z) >> 4;

                for (int chunkX = minChunkX; chunkX <= maxChunkX; chunkX++)
                {
                    for (int chunkZ = minChunkZ; chunkZ <= maxChunkZ; chunkZ++)
                    {
                        Chunk chunk = world.ChunkCache.GetChunkSync(chunkX, chunkZ);
                        if (chunk == null) continue;

                        var tileEntities = chunk.tileEntities?.list;
                        if (tileEntities == null) continue;

                        for (int i = 0; i < tileEntities.Count; i++)
                        {
                            TileEntity te = tileEntities[i];
                            if (te == null) continue;

                            Vector3i tePos = te.ToWorldPos();
                            int dx = Math.Abs(tePos.x - center.x);
                            int dy = Math.Abs(tePos.y - center.y);
                            int dz = Math.Abs(tePos.z - center.z);

                            if (dx <= distance.x && dy <= distance.y && dz <= distance.z)
                            {
                                TileEntityComposite composite = te as TileEntityComposite;
                                if (composite == null) continue;

                                TEFeatureStorage storage = composite.GetFeature<TEFeatureStorage>();
                                if (storage == null) continue;

                                if (!IsEligibleContainer(composite, storage)) continue;

                                if (composite.IsUserAccessing())
                                    continue;

                                results.Add(new ContainerResult(composite, storage));
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Warning($"[ProxiStack] Error scanning containers QS: {ex.Message}");
            }

            return results;
        }

        /// <summary>
        /// Findet Container in kubischem Radius via Chunk-Lookup (3D-Reichweite).
        /// </summary>
        public static List<ContainerResult> FindNearbyContainers(Vector3i center, Vector3i radius)
        {
            var results = new List<ContainerResult>();
            try
            {
                World world = GameManager.Instance.World;
                if (world == null || world.ChunkCache == null) return results;

                int minChunkX = (center.x - radius.x) >> 4;
                int maxChunkX = (center.x + radius.x) >> 4;
                int minChunkZ = (center.z - radius.z) >> 4;
                int maxChunkZ = (center.z + radius.z) >> 4;

                for (int chunkX = minChunkX; chunkX <= maxChunkX; chunkX++)
                {
                    for (int chunkZ = minChunkZ; chunkZ <= maxChunkZ; chunkZ++)
                    {
                        Chunk chunk = world.ChunkCache.GetChunkSync(chunkX, chunkZ);
                        if (chunk == null) continue;

                        var tileEntities = chunk.tileEntities?.list;
                        if (tileEntities == null) continue;

                        for (int i = 0; i < tileEntities.Count; i++)
                        {
                            TileEntity te = tileEntities[i];
                            if (te == null) continue;

                            Vector3i tePos = te.ToWorldPos();
                            int dx = Math.Abs(tePos.x - center.x);
                            int dy = Math.Abs(tePos.y - center.y);
                            int dz = Math.Abs(tePos.z - center.z);

                            if (dx <= radius.x && dy <= radius.y && dz <= radius.z)
                            {
                                TileEntityComposite composite = te as TileEntityComposite;
                                if (composite == null) continue;

                                TEFeatureStorage storage = composite.GetFeature<TEFeatureStorage>();
                                if (storage == null) continue;

                                if (!IsEligibleContainer(composite, storage)) continue;

                                if (composite.IsUserAccessing())
                                    continue;

                                results.Add(new ContainerResult(composite, storage));
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Warning($"[ProxiStack] Error scanning containers: {ex.Message}");
            }
            return results;
        }

        /// <summary>
        /// Sammelt alle ItemStacks aus nahegelegenen Containern für Proximity-Crafting (3D-Reichweite).
        /// </summary>
        public static List<ItemStack> GetNearbyCraftingItems(Vector3i center, Vector3i radius, bool includeLockedContainers)
        {
            lock (CraftingItemsCacheLock)
            {
                if (IsCraftingItemsCacheValid(center, radius, includeLockedContainers))
                    return new List<ItemStack>(CraftingItemsCache);

                CraftingItemsCache = BuildNearbyCraftingItems(center, radius, includeLockedContainers);
                CraftingItemsCacheCenter = center;
                CraftingItemsCacheRadius = radius;
                CraftingItemsCacheIncludeLocked = includeLockedContainers;
                CraftingItemsCacheTime = Time.realtimeSinceStartup;

                return new List<ItemStack>(CraftingItemsCache);
            }
        }

        private static List<ItemStack> BuildNearbyCraftingItems(Vector3i center, Vector3i radius, bool includeLockedContainers)
        {
            var results = new List<ItemStack>();
            try
            {
                var sources = GetNearbySources(center, radius, includeLockedContainers);
                for (int i = 0; i < sources.Count; i++)
                {
                    ItemStack[] stacks = sources[i].Stacks;
                    if (stacks == null) continue;

                    for (int k = 0; k < stacks.Length; k++)
                    {
                        ItemStack stack = stacks[k];
                        if (stack != null && !stack.IsEmpty())
                            results.Add(stack);
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Warning($"[ProxiStack] Error collecting crafting items: {ex.Message}");
            }
            return results;
        }

        /// <summary>
        /// Kombiniert die vorhandenen ItemStacks mit den Stacks aus nahegelegenen Containern (3D-Reichweite).
        /// </summary>
        public static IList<ItemStack> MergeWithNearbyCraftingItems(IList<ItemStack> baseItems, Vector3i center, Vector3i radius, bool includeLockedContainers)
        {
            var merged = new List<ItemStack>();

            if (baseItems != null)
            {
                for (int i = 0; i < baseItems.Count; i++)
                {
                    ItemStack stack = baseItems[i];
                    if (stack != null && !stack.IsEmpty())
                        merged.Add(stack);
                }
            }

            List<ItemStack> nearbyItems = GetNearbyCraftingItems(center, radius, includeLockedContainers);
            for (int i = 0; i < nearbyItems.Count; i++)
            {
                ItemStack stack = nearbyItems[i];
                if (stack != null && !stack.IsEmpty())
                    merged.Add(stack);
            }

            return merged;
        }

        private static bool IsCraftingItemsCacheValid(Vector3i center, Vector3i radius, bool includeLockedContainers)
        {
            if (CraftingItemsCache == null)
                return false;

            if (CraftingItemsCacheRadius != radius || CraftingItemsCacheIncludeLocked != includeLockedContainers)
                return false;

            if (Time.realtimeSinceStartup - CraftingItemsCacheTime > CraftingItemsCacheSeconds)
                return false;

            if (Math.Abs(CraftingItemsCacheCenter.x - center.x) > 1)
                return false;
            if (Math.Abs(CraftingItemsCacheCenter.y - center.y) > 1)
                return false;
            if (Math.Abs(CraftingItemsCacheCenter.z - center.z) > 1)
                return false;

            return true;
        }

        /// <summary>
        /// Zählt ein bestimmtes Item in allen nahegelegenen Containern via Chunk-Lookup (3D-Reichweite).
        /// </summary>
        public static int GetNearbyItemCount(Vector3i center, Vector3i radius, bool includeLocked, ItemValue item)
        {
            int count = 0;
            try
            {
                var sources = GetNearbySources(center, radius, includeLocked);
                for (int i = 0; i < sources.Count; i++)
                {
                    ItemStack[] stacks = sources[i].Stacks;
                    if (stacks == null) continue;

                    for (int k = 0; k < stacks.Length; k++)
                    {
                        ItemStack stack = stacks[k];
                        if (stack != null && !stack.IsEmpty() && stack.itemValue != null &&
                            stack.itemValue.type == item.type)
                            count += stack.count;
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Warning($"[ProxiStack] GetNearbyItemCount error: {ex.Message}");
            }
            return count;
        }

        /// <summary>
        /// Entfernt Items aus nahegelegenen Containern via Chunk-Lookup (3D-Reichweite).
        /// Gibt die tatsächlich entfernte Anzahl zurück.
        /// </summary>
        public static int RemoveNearbyItems(Vector3i center, Vector3i radius, bool includeLocked, ItemValue item, int count, IList<ItemStack> removedItems = null)
        {
            int removed = 0;
            try
            {
                var sources = GetNearbySources(center, radius, includeLocked);
                for (int i = 0; i < sources.Count && removed < count; i++)
                {
                    var source = sources[i];
                    ItemStack[] stacks = source.Stacks;
                    if (stacks == null) continue;

                    bool modified = false;
                    for (int k = 0; k < stacks.Length && removed < count; k++)
                    {
                        ItemStack stack = stacks[k];
                        if (stack == null || stack.IsEmpty() || stack.itemValue == null) continue;
                        if (stack.itemValue.type != item.type) continue;

                        int take = Math.Min(count - removed, stack.count);
                        if (removedItems != null)
                        {
                            removedItems.Add(new ItemStack(stack.itemValue.Clone(), take));
                        }
                        stack.count -= take;
                        removed += take;
                        if (stack.count <= 0)
                            stacks[k] = ItemStack.Empty;

                        modified = true;
                    }

                    if (modified)
                    {
                        source.SetModified();
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Warning($"[ProxiStack] RemoveNearbyItems error: {ex.Message}");
            }
            return removed;
        }

        /// <summary>
        /// Holt das Storage-Feature für eine Blockposition.
        /// v3.0: GetTileEntity nimmt nur Vector3i.
        /// v3.3: ITileEntityLootable entfällt — Rückgabe ist TEFeatureStorage.
        /// </summary>
        public static ContainerResult GetLootableAtPosition(Vector3i position)
        {
            try
            {
                World world = GameManager.Instance.World;
                if (world == null) return new ContainerResult(null, null);

                TileEntityComposite composite = world.GetTileEntity(position) as TileEntityComposite;
                if (composite == null) return new ContainerResult(null, null);

                TEFeatureStorage storage = composite.GetFeature<TEFeatureStorage>();
                if (storage == null) return new ContainerResult(null, null);

                if (composite.IsUserAccessing())
                    return new ContainerResult(null, null);

                return new ContainerResult(composite, storage);
            }
            catch (Exception ex)
            {
                Log.Warning($"[ProxiStack] Error at {position}: {ex.Message}");
                return new ContainerResult(null, null);
            }
        }

        /// <summary>
        /// Zieht die offene Arbeitsstation-Ansicht auf den TE-Stand nach (TE -> UI).
        ///
        /// Hintergrund (belegt an Assembly-CSharp v3.3.0):
        /// `XUiC_WorkstationWindowGroup` hält die Slots ihrer Grids als eigene
        /// UI-Kopie und schreibt sie in `syncTEfromUI()` per
        /// `XUiM_Workstation.SetOutputStacks(outputWindow.GetSlots())` zurück ins
        /// TileEntity. Das passiert u. a. sofort, wenn ein Craft-Vorgang startet
        /// (`Update()`: `wasCrafting != craftingQueue.IsCrafting()`) und beim
        /// Schließen des Fensters. Verringert ProxiStack das Ausgabefach direkt am
        /// TE, würden die unveränderten UI-Slots den alten Stand zurückschreiben —
        /// die Items wären "nicht verbraucht".
        /// Vanilla besitzt für diesen Fall `XUiC_WorkstationWindowGroup
        /// .TileEntity_OutputChanged()` (genau `outputWindow.SetSlots(...)`,
        /// verdrahtet es aber nirgends). Hier wird derselbe Schritt ausgeführt,
        /// sofern das Fenster dieser Station gerade offen ist.
        /// </summary>
        public static void RefreshOpenWorkstationOutputGrid(TileEntityWorkstation workstation)
        {
            try
            {
                if (workstation == null) return;

                // 1) Beste Quelle: das Stationsfenster des laufenden Craft-Klicks
                //    (siehe Patches.Patch_ItemActionEntryCraft_OnActivated). Nur während des
                //    Klick-Vorgangs gesetzt und im Livetest nachweislich auflösbar.
                XUiC_WorkstationWindowGroup activeWindow = Patches.ActiveStationWindow;
                if (activeWindow != null && activeWindow.WorkstationData != null
                    && activeWindow.WorkstationData.TileEntity == workstation)
                {
                    RefreshWorkstationOutputGrid(activeWindow);
                    return;
                }

                LocalPlayerUI playerUI = LocalPlayerUI.primaryUI;
                if (playerUI == null) return;

                XUi xui = playerUI.xui;
                if (xui == null) return;

                // 2) Suche über die Window-Groups (Vanilla-Muster aus ItemActionEntryCraft.OnActivated).
                List<XUiC_CraftingWindowGroup> groups = xui.GetChildrenByType<XUiC_CraftingWindowGroup>();
                if (groups != null)
                {
                    for (int i = 0; i < groups.Count; i++)
                    {
                        XUiC_WorkstationWindowGroup wsGroup = groups[i] as XUiC_WorkstationWindowGroup;
                        if (wsGroup == null) continue;
                        if (wsGroup.WindowGroup == null || !wsGroup.WindowGroup.isShowing) continue;
                        if (wsGroup.WorkstationData == null || wsGroup.WorkstationData.TileEntity != workstation) continue;

                        RefreshWorkstationOutputGrid(wsGroup);
                        return;
                    }
                }

                // 3) Fallback: altes Property (in dieser Spielversion offenbar nicht gesetzt).
                XUiC_WorkstationOutputGrid outputGrid = xui.CurrentWorkstationOutputGrid;
                if (outputGrid == null) return;

                XUiM_Workstation data = outputGrid.WorkstationData;
                if (data == null || data.TileEntity != workstation) return;

                outputGrid.SetSlots(data.GetOutputStacks());
                outputGrid.IsDirty = true;
            }
            catch (Exception ex)
            {
                Log.Warning($"[ProxiStack] Refresh workstation output grid: {ex.Message}");
            }
        }

        /// <summary>
        /// Zieht das Ausgabefach eines konkreten Stationsfensters auf den TE-Stand nach
        /// (TE -> UI). Genau der Schritt, den Vanilla in syncUIfromTE()/TileEntity_OutputChanged()
        /// ausführt, hier aber gezielt nach einem eigenen Verbrauch.
        /// </summary>
        public static void RefreshWorkstationOutputGrid(XUiC_WorkstationWindowGroup wsGroup)
        {
            try
            {
                if (wsGroup == null || wsGroup.WorkstationData == null) return;
                if (wsGroup.outputWindow == null) return;

                wsGroup.outputWindow.SetSlots(wsGroup.WorkstationData.GetOutputStacks());
                wsGroup.outputWindow.IsDirty = true;
                wsGroup.SetAllChildrenDirty();
            }
            catch (Exception ex)
            {
                Log.Warning($"[ProxiStack] Refresh workstation output grid (Fenster): {ex.Message}");
            }
        }

        /// <summary>
        /// Ermittelt alle verfügbaren Lagerquellen in Reichweite und sortiert sie nach konfigurierter Priorität.
        /// </summary>
        public static List<StorageSource> GetNearbySources(Vector3i center, Vector3i radius, bool includeLocked)
        {
            var sources = new List<StorageSource>();
            try
            {
                World world = GameManager.Instance.World;
                if (world == null || world.ChunkCache == null) return sources;

                int minChunkX = (center.x - radius.x) >> 4;
                int maxChunkX = (center.x + radius.x) >> 4;
                int minChunkZ = (center.z - radius.z) >> 4;
                int maxChunkZ = (center.z + radius.z) >> 4;

                var config = ProxiStackMod.Instance?.Config;
                if (config == null) return sources;

                for (int chunkX = minChunkX; chunkX <= maxChunkX; chunkX++)
                {
                    for (int chunkZ = minChunkZ; chunkZ <= maxChunkZ; chunkZ++)
                    {
                        Chunk chunk = world.ChunkCache.GetChunkSync(chunkX, chunkZ);
                        if (chunk == null) continue;

                        var tileEntities = chunk.tileEntities?.list;
                        if (tileEntities == null) continue;

                        for (int i = 0; i < tileEntities.Count; i++)
                        {
                            TileEntity te = tileEntities[i];
                            if (te == null) continue;

                            Vector3i tePos = te.ToWorldPos();
                            int dx = Math.Abs(tePos.x - center.x);
                            int dy = Math.Abs(tePos.y - center.y);
                            int dz = Math.Abs(tePos.z - center.z);

                            if (dx <= radius.x && dy <= radius.y && dz <= radius.z)
                            {
                                TileEntityComposite composite = te as TileEntityComposite;
                                if (composite != null)
                                {
                                    TEFeatureStorage storage = composite.GetFeature<TEFeatureStorage>();
                                    if (storage == null) continue;

                                    if (!includeLocked)
                                    {
                                        TEFeatureLockable lockable = composite.GetFeature<TEFeatureLockable>();
                                        if (lockable != null && lockable.IsLocked()) continue;
                                    }

                                    ItemStack[] storageItems = GetStorageItems(storage);
                                    if (storageItems == null) continue;

                                    if (!IsEligibleContainer(composite, storage)) continue;

                                    sources.Add(new StorageSource
                                    {
                                        Type = StorageType.Container,
                                        Stacks = storageItems,
                                        TileEntity = composite
                                    });
                                }
                                else if (config.PullFromWorkstationOutputs && te is TileEntityWorkstation workstation)
                                {
                                    if (!workstation.IsPlayerPlaced) continue;

                                    // v1.1.6: NUR das Ausgabefach (output) zählt als Crafting-Quelle.
                                    // Das Arbeits-/Einschmelzfach (input) wird bewusst NICHT mehr
                                    // gescannt — sonst verschwinden z. B. Lehm, Kohle oder Erz aus
                                    // dem Einschmelzfach, sobald anderswo etwas hergestellt wird
                                    // (Forge/Schmiede, Campfire, Workbench, Chemistry Station).
                                    // Vanilla-Beleg: TileEntityWorkstation hat getrennte öffentliche
                                    // Arrays input (Arbeitsfach) und output (Ausgabefach),
                                    // geprüft per monodis --fields gegen Assembly-CSharp v3.3.
                                    if (workstation.output != null)
                                    {
                                        sources.Add(new StorageSource
                                        {
                                            Type = StorageType.Workstation,
                                            Stacks = workstation.output,
                                            TileEntity = workstation
                                        });
                                    }
                                }
                                else if (config.PullFromDewCollectors && te is TileEntityCollector collector)
                                {
                                    if (collector.itemsInternal != null)
                                    {
                                        sources.Add(new StorageSource
                                        {
                                            Type = StorageType.DewCollector,
                                            Stacks = collector.itemsInternal,
                                            TileEntity = collector
                                        });
                                    }
                                }
                            }
                        }
                    }
                }

                EntityPlayerLocal player = GameManager.Instance?.World?.GetPrimaryPlayer();
                if (player != null)
                {
                    for (int i = 0; i < world.Entities.list.Count; i++)
                    {
                        Entity entity = world.Entities.list[i];
                        if (entity == null) continue;

                        bool isVehicle = entity is EntityVehicle;
                        bool isDrone = entity is EntityDrone;

                        if ((isVehicle && config.PullFromVehicles) || (isDrone && config.PullFromDrones))
                        {
                            Vector3 entityPos = entity.position;
                            int dx = Math.Abs((int)entityPos.x - center.x);
                            int dy = Math.Abs((int)entityPos.y - center.y);
                            int dz = Math.Abs((int)entityPos.z - center.z);

                            if (dx <= radius.x && dy <= radius.y && dz <= radius.z)
                            {
                                Bag bag = entity.bag;
                                ItemStack[] bagItems = GetBagItems(bag);
                                if (bagItems == null) continue;

                                sources.Add(new StorageSource
                                {
                                    Type = isVehicle ? StorageType.Vehicle : StorageType.Drone,
                                    Stacks = bagItems,
                                    Entity = entity
                                });
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Warning($"[ProxiStack] Error collecting nearby sources: {ex.Message}");
            }

            var priorityIndex = new Dictionary<StorageType, int>();
            var priorityList = ProxiStackMod.Instance?.Config?.StoragePriority;
            if (priorityList != null)
            {
                for (int i = 0; i < priorityList.Count; i++)
                    priorityIndex[priorityList[i]] = i;
            }

            sources.Sort((a, b) =>
            {
                int idxA = priorityIndex.TryGetValue(a.Type, out int valA) ? valA : int.MaxValue;
                int idxB = priorityIndex.TryGetValue(b.Type, out int valB) ? valB : int.MaxValue;
                return idxA.CompareTo(idxB);
            });

            return sources;
        }
    }

    public struct StorageSource
    {
        public StorageType Type;
        public ItemStack[] Stacks;
        public TileEntity TileEntity;
        public Entity Entity;

        public void SetModified()
        {
            if (TileEntity != null)
            {
                TileEntity.SetModified();

                // v1.1.7: Wird das Ausgabefach einer Arbeitsstation direkt am
                // TileEntity verringert, muss die offene Stationsansicht sofort
                // mitgezogen werden. Sonst stellt Vanillas syncTEfromUI() den
                // alten Stand aus den unveränderten UI-Slots wieder her.
                // Beleg: Assembly-CSharp v3.3.0, XUiC_WorkstationWindowGroup.
                if (TileEntity is TileEntityWorkstation workstation)
                    ContainerHelper.RefreshOpenWorkstationOutputGrid(workstation);

                return;
            }

            // v3.3: Bag.Touched ist read-only (kein Setter) — Bag hat auch kein
            // public items-Feld mehr; der Slot-Zugriff läuft über Bag.itemGrid.
            // Vanilla markiert geänderte Behälter mit ItemStackGrid.Touch(),
            // das intern worldTimeTouched aktualisiert. Touch() prüft zusätzlich
            // auf GameManager.Instance.World, daher hier defensiv absichern.
            if (GameManager.Instance == null || GameManager.Instance.World == null)
                return;

            if (Entity is EntityVehicle vehicle)
            {
                ItemStackGrid grid = ContainerHelper.GetBagGrid(vehicle.bag);
                if (grid != null) grid.Touch();
            }
            else if (Entity is EntityDrone drone)
            {
                ItemStackGrid grid = ContainerHelper.GetBagGrid(drone.bag);
                if (grid != null) grid.Touch();
            }
        }
    }
}

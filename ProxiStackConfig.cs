using System.IO;
using System.Xml;
using UnityEngine;
using System.Collections.Generic;

namespace ProxiStack
{
    public enum StorageType
    {
        Drone,
        DewCollector,
        Workstation,
        Container,
        Vehicle
    }

    public class ProxiStackConfig
    {
        // === Storage Priority ===
        public List<StorageType> StoragePriority { get; private set; } = new List<StorageType>
        {
            StorageType.Drone,
            StorageType.DewCollector,
            StorageType.Workstation,
            StorageType.Container,
            StorageType.Vehicle
        };

        // === ProxiStack Crafting Features ===
        public Vector3i CraftingDistance { get; private set; } = new Vector3i(25, 25, 25);
        public bool EnableForCrafting { get; private set; } = true;
        public bool EnableForRepair { get; private set; } = true;
        public bool EnableForUpgrade { get; private set; } = true;
        public bool EnableForPainting { get; private set; } = true;
        public bool EnableForReload { get; private set; } = true;
        public bool EnableForTrader { get; private set; } = true;
        public bool EnableForQuests { get; private set; } = true;
        public bool EnableForLockpicking { get; private set; } = true;
        public bool PullFromVehicles { get; private set; } = true;
        public bool PullFromDrones { get; private set; } = true;
        public bool PullFromDewCollectors { get; private set; } = true;
        public bool PullFromWorkstationOutputs { get; private set; } = true;
        public bool AllowLockedContainers { get; private set; } = true;

        // === ProxiStack Stashing Features ===
        public bool EnableForStash { get; private set; } = true;
        public bool EnableForRestock { get; private set; } = true;
        public bool EnableForLock { get; private set; } = true;
        public Vector3i StashDistance { get; private set; } = new Vector3i(15, 15, 15);
        public bool LockModeIconVisible { get; private set; } = true;
        public Color32 LockIconColor { get; private set; } = new Color32(255, 0, 0, 255);
        public Color32 LockBorderColor { get; private set; } = new Color32(128, 0, 0, 0);

        // === Hotkeys (Default: Alt+X, Alt+Z, Alt+LeftClick) ===
        public KeyCode[] StashHotkeys { get; private set; } = new[] { KeyCode.LeftAlt, KeyCode.X };
        public KeyCode[] RestockHotkeys { get; private set; } = new[] { KeyCode.LeftAlt, KeyCode.Z };
        public KeyCode[] LockHotkeys { get; private set; } = new[] { KeyCode.LeftAlt };

        // === Multiplayer ===
        public bool MultiplayerImmediateLock { get; private set; } = true;
        public int MultiplayerHandshakeTimeoutSeconds { get; private set; } = 10;

        private string configFilePath;

        public void Load(string filePath)
        {
            configFilePath = filePath;
            if (!File.Exists(filePath))
            {
                Log.Out("[ProxiStack] No config found, using defaults. Creating config at: " + filePath);
                Save();
                return;
            }

            try
            {
                XmlDocument xml = new XmlDocument();
                xml.Load(filePath);

                // Crafting settings
                string craftDistanceStr = ReadString(xml, "CraftingDistance", "");
                if (string.IsNullOrEmpty(craftDistanceStr))
                {
                    // Fallback on old Range
                    int oldRange = ReadInt(xml, "Range", 25);
                    CraftingDistance = new Vector3i(oldRange, oldRange, oldRange);
                }
                else
                {
                    string[] parts = craftDistanceStr.Split(' ');
                    if (parts.Length == 3)
                    {
                        int x = Mathf.Clamp(int.Parse(parts[0]), 0, 127);
                        int y = Mathf.Clamp(int.Parse(parts[1]), 0, 127);
                        int z = Mathf.Clamp(int.Parse(parts[2]), 0, 127);
                        CraftingDistance = new Vector3i(x, y, z);
                    }
                }
                EnableForCrafting = ReadBool(xml, "EnableForCrafting", true);
                bool oldRepairUpgrade = ReadBool(xml, "EnableForRepairAndUpgrade", true);
                EnableForRepair = ReadBool(xml, "EnableForRepair", oldRepairUpgrade);
                EnableForUpgrade = ReadBool(xml, "EnableForUpgrade", oldRepairUpgrade);
                EnableForPainting = ReadBool(xml, "EnableForPainting", true);
                EnableForReload = ReadBool(xml, "EnableForReload", true);
                EnableForTrader = ReadBool(xml, "EnableForTrader", true);
                EnableForQuests = ReadBool(xml, "EnableForQuests", true);
                EnableForLockpicking = ReadBool(xml, "EnableForLockpicking", true);
                PullFromVehicles = ReadBool(xml, "PullFromVehicles", true);
                PullFromDrones = ReadBool(xml, "PullFromDrones", true);
                PullFromDewCollectors = ReadBool(xml, "PullFromDewCollectors", true);
                PullFromWorkstationOutputs = ReadBool(xml, "PullFromWorkstationOutputs", true);
                AllowLockedContainers = ReadBool(xml, "AllowLockedContainers", true);

                // Storage Priority
                string priorityStr = ReadString(xml, "StoragePriority", "Drone, DewCollector, Workstation, Container, Vehicle");
                ParseStoragePriority(priorityStr);

                // Stashing settings
                EnableForStash = ReadBool(xml, "EnableForStash", true);
                EnableForRestock = ReadBool(xml, "EnableForRestock", true);
                EnableForLock = ReadBool(xml, "EnableForLock", true);
                LockModeIconVisible = ReadBool(xml, "LockModeIconVisible", true);

                // StashDistance
                string distanceStr = ReadString(xml, "StashDistance", "15 15 15");
                string[] partsDistance = distanceStr.Split(' ');
                if (partsDistance.Length == 3)
                {
                    int x = Mathf.Clamp(int.Parse(partsDistance[0]), 0, 127);
                    int y = Mathf.Clamp(int.Parse(partsDistance[1]), 0, 127);
                    int z = Mathf.Clamp(int.Parse(partsDistance[2]), 0, 127);
                    StashDistance = new Vector3i(x, y, z);
                }

                // Lock colors
                LockIconColor = ReadColor(xml, "LockedSlotsIconColor", new Color32(255, 0, 0, 255));
                LockBorderColor = ReadColor(xml, "LockedSlotsBorderColor", new Color32(128, 0, 0, 0));

                // Hotkeys (stored as space-separated key codes)
                StashHotkeys = ReadKeyCodes(xml, "StashHotkeys", "308 120");
                RestockHotkeys = ReadKeyCodes(xml, "RestockHotkeys", "308 122");
                LockHotkeys = ReadKeyCodes(xml, "LockHotkeys", "308");

                // Multiplayer
                MultiplayerImmediateLock = ReadBool(xml, "MultiplayerImmediateLock", true);
                MultiplayerHandshakeTimeoutSeconds = ReadInt(xml, "MultiplayerHandshakeTimeoutSeconds", 10);

                Log.Out($"[ProxiStack] Config loaded with craftingDistance=({CraftingDistance.x},{CraftingDistance.y},{CraftingDistance.z}), stashDistance=({StashDistance.x},{StashDistance.y},{StashDistance.z})");
            }
            catch (System.Exception ex)
            {
                Log.Warning($"[ProxiStack] Failed to load config: {ex.Message}. Using defaults.");
            }
        }

        public void Save()
        {
            try
            {
                string dir = Path.GetDirectoryName(configFilePath);
                if (!Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                using (StreamWriter writer = new StreamWriter(configFilePath))
                {
                    writer.WriteLine("<?xml version=\"1.0\" encoding=\"UTF-8\"?>");
                    writer.WriteLine("<xml>");
                    writer.WriteLine("\t<!-- ProxiStack Configuration -->");
                    writer.WriteLine("");
                    writer.WriteLine("\t<!-- === PROXISTACK CRAFTING === -->");
                    writer.WriteLine("");
                    writer.WriteLine("\t<!-- 3D distance from player to scan for containers during crafting (Format: X Y Z) -->");
                    writer.WriteLine($"\t<CraftingDistance>{CraftingDistance.x} {CraftingDistance.y} {CraftingDistance.z}</CraftingDistance>");
                    writer.WriteLine("");
                    writer.WriteLine("\t<!-- Allow crafting using resources from nearby containers -->");
                    writer.WriteLine($"\t<EnableForCrafting>{EnableForCrafting}</EnableForCrafting>");
                    writer.WriteLine("");
                    writer.WriteLine("\t<!-- Allow block repairing using resources from nearby containers -->");
                    writer.WriteLine($"\t<EnableForRepair>{EnableForRepair}</EnableForRepair>");
                    writer.WriteLine("");
                    writer.WriteLine("\t<!-- Allow block upgrading using resources from nearby containers -->");
                    writer.WriteLine($"\t<EnableForUpgrade>{EnableForUpgrade}</EnableForUpgrade>");
                    writer.WriteLine("");
                    writer.WriteLine("\t<!-- Allow block painting using paint from nearby containers -->");
                    writer.WriteLine($"\t<EnableForPainting>{EnableForPainting}</EnableForPainting>");
                    writer.WriteLine("");
                    writer.WriteLine("\t<!-- Allow weapon/tool reloading from nearby containers -->");
                    writer.WriteLine($"\t<EnableForReload>{EnableForReload}</EnableForReload>");
                    writer.WriteLine("");
                    writer.WriteLine("\t<!-- Reserved setting: trading from nearby containers is not currently supported -->");
                    writer.WriteLine($"\t<EnableForTrader>{EnableForTrader}</EnableForTrader>");
                    writer.WriteLine("");
                    writer.WriteLine("\t<!-- Allow nearby container items to count toward supported tracked-item challenges -->");
                    writer.WriteLine($"\t<EnableForQuests>{EnableForQuests}</EnableForQuests>");
                    writer.WriteLine("");
                    writer.WriteLine("\t<!-- Reserved setting: lockpicking from nearby containers is not currently supported -->");
                    writer.WriteLine($"\t<EnableForLockpicking>{EnableForLockpicking}</EnableForLockpicking>");
                    writer.WriteLine("");
                    writer.WriteLine("\t<!-- Scan and pull crafting resources from vehicles -->");
                    writer.WriteLine($"\t<PullFromVehicles>{PullFromVehicles}</PullFromVehicles>");
                    writer.WriteLine("");
                    writer.WriteLine("\t<!-- Scan and pull crafting resources from active drones -->");
                    writer.WriteLine($"\t<PullFromDrones>{PullFromDrones}</PullFromDrones>");
                    writer.WriteLine("");
                    writer.WriteLine("\t<!-- Scan and pull crafting resources from dew collectors -->");
                    writer.WriteLine($"\t<PullFromDewCollectors>{PullFromDewCollectors}</PullFromDewCollectors>");
                    writer.WriteLine("");
                    writer.WriteLine("\t<!-- Scan and pull crafting resources from workstation output slots (output only; the input/smelting slot is never used) -->");
                    writer.WriteLine($"\t<PullFromWorkstationOutputs>{PullFromWorkstationOutputs}</PullFromWorkstationOutputs>");
                    writer.WriteLine("");
                    writer.WriteLine("\t<!-- Allow pulling resources from password-protected or locked containers you have access to -->");
                    writer.WriteLine($"\t<AllowLockedContainers>{AllowLockedContainers}</AllowLockedContainers>");
                    writer.WriteLine("");
                    writer.WriteLine("\t<!-- === STORAGE PRIORITY === -->");
                    writer.WriteLine("\t<!-- Prioritätsreihenfolge für die Materialsuche und den Verbrauch (durch Komma getrennt). -->");
                    writer.WriteLine("\t<!-- Mögliche Werte: Drone, DewCollector, Workstation, Container, Vehicle -->");
                    writer.WriteLine($"\t<StoragePriority>{string.Join(", ", StoragePriority)}</StoragePriority>");
                    writer.WriteLine("");
                    writer.WriteLine("\t<!-- === PROXISTACK STASHING === -->");
                    writer.WriteLine("");
                    writer.WriteLine("\t<!-- Enable the Stash feature (stashing matching items to nearby chests) -->");
                    writer.WriteLine($"\t<EnableForStash>{EnableForStash}</EnableForStash>");
                    writer.WriteLine("");
                    writer.WriteLine("\t<!-- Enable the Restock feature (refilling ammo/food from nearby chests) -->");
                    writer.WriteLine($"\t<EnableForRestock>{EnableForRestock}</EnableForRestock>");
                    writer.WriteLine("");
                    writer.WriteLine("\t<!-- Enable slot locking in player backpack to prevent stashing locked slots -->");
                    writer.WriteLine($"\t<EnableForLock>{EnableForLock}</EnableForLock>");
                    writer.WriteLine("");
                    writer.WriteLine("\t<!-- 3D distance from player to stash items (Format: X Y Z) -->");
                    writer.WriteLine($"\t<StashDistance>{StashDistance.x} {StashDistance.y} {StashDistance.z}</StashDistance>");
                    writer.WriteLine("");
                    writer.WriteLine("\t<!-- Show the padlock icon indicating lock mode state in the inventory header -->");
                    writer.WriteLine($"\t<LockModeIconVisible>{LockModeIconVisible}</LockModeIconVisible>");
                    writer.WriteLine("");
                    writer.WriteLine("\t<!-- Locked slots padlock overlay icon color (Format: Red Green Blue Alpha) -->");
                    writer.WriteLine($"\t<LockedSlotsIconColor>{LockIconColor.r} {LockIconColor.g} {LockIconColor.b} {LockIconColor.a}</LockedSlotsIconColor>");
                    writer.WriteLine("");
                    writer.WriteLine("\t<!-- Locked slots border highlight color (Format: Red Green Blue Alpha) -->");
                    writer.WriteLine($"\t<LockedSlotsBorderColor>{LockBorderColor.r} {LockBorderColor.g} {LockBorderColor.b} {LockBorderColor.a}</LockedSlotsBorderColor>");
                    writer.WriteLine("");
                    writer.WriteLine("\t<!-- === HOTKEYS === -->");
                    writer.WriteLine("\t<!-- Hotkey combinations (stored as space-separated Unity KeyCode numbers) -->");
                    writer.WriteLine("\t<!-- KeyCode references: 308=LeftAlt, 120=X, 122=Z, 304=LeftShift, 306=LeftCtrl -->");
                    writer.WriteLine($"\t<StashHotkeys>{string.Join(" ", System.Array.ConvertAll(StashHotkeys, k => (int)k))}</StashHotkeys>");
                    writer.WriteLine($"\t<RestockHotkeys>{string.Join(" ", System.Array.ConvertAll(RestockHotkeys, k => (int)k))}</RestockHotkeys>");
                    writer.WriteLine($"\t<LockHotkeys>{string.Join(" ", System.Array.ConvertAll(LockHotkeys, k => (int)k))}</LockHotkeys>");
                    writer.WriteLine("");
                    writer.WriteLine("\t<!-- === MULTIPLAYER === -->");
                    writer.WriteLine("");
                    writer.WriteLine("\t<!-- Lock slots immediately on client click without waiting for server response -->");
                    writer.WriteLine($"\t<MultiplayerImmediateLock>{MultiplayerImmediateLock}</MultiplayerImmediateLock>");
                    writer.WriteLine("");
                    writer.WriteLine("\t<!-- Handshake timeout duration in seconds for server connection handshake -->");
                    writer.WriteLine($"\t<MultiplayerHandshakeTimeoutSeconds>{MultiplayerHandshakeTimeoutSeconds}</MultiplayerHandshakeTimeoutSeconds>");
                    writer.WriteLine("</xml>");
                }
                Log.Out($"[ProxiStack] Config saved to {configFilePath}");
            }
            catch (System.Exception ex)
            {
                Log.Warning($"[ProxiStack] Failed to save config: {ex.Message}");
            }
        }

        // === Helper Methods ===

        private int ReadInt(XmlDocument xml, string tag, int defaultValue)
        {
            var nodes = xml.GetElementsByTagName(tag);
            if (nodes.Count > 0 && int.TryParse(nodes[0].InnerText.Trim(), out int val))
                return val;
            return defaultValue;
        }

        private bool ReadBool(XmlDocument xml, string tag, bool defaultValue)
        {
            var nodes = xml.GetElementsByTagName(tag);
            if (nodes.Count > 0 && bool.TryParse(nodes[0].InnerText.Trim(), out bool val))
                return val;
            return defaultValue;
        }

        private string ReadString(XmlDocument xml, string tag, string defaultValue)
        {
            var nodes = xml.GetElementsByTagName(tag);
            if (nodes.Count > 0)
                return nodes[0].InnerText.Trim();
            return defaultValue;
        }

        private Color32 ReadColor(XmlDocument xml, string tag, Color32 defaultValue)
        {
            var nodes = xml.GetElementsByTagName(tag);
            if (nodes.Count > 0)
            {
                string[] parts = nodes[0].InnerText.Trim().Split(' ');
                if (parts.Length == 4 &&
                    byte.TryParse(parts[0], out byte r) &&
                    byte.TryParse(parts[1], out byte g) &&
                    byte.TryParse(parts[2], out byte b) &&
                    byte.TryParse(parts[3], out byte a))
                    return new Color32(r, g, b, a);
            }
            return defaultValue;
        }

        private KeyCode[] ReadKeyCodes(XmlDocument xml, string tag, string defaultValue)
        {
            string text = ReadString(xml, tag, defaultValue);
            string[] parts = text.Split(' ');
            var keys = new System.Collections.Generic.List<KeyCode>();
            foreach (string part in parts)
            {
                if (int.TryParse(part.Trim(), out int code))
                    keys.Add((KeyCode)code);
            }
            return keys.ToArray();
        }

        private void ParseStoragePriority(string input)
        {
            if (string.IsNullOrEmpty(input)) return;
            var list = new List<StorageType>();
            var parts = input.Split(',');
            foreach (var part in parts)
            {
                string trimmed = part.Trim();
                if (System.Enum.TryParse(trimmed, true, out StorageType type))
                {
                    if (!list.Contains(type))
                        list.Add(type);
                }
            }
            // Append missing default types
            var defaults = new List<StorageType> { StorageType.Drone, StorageType.DewCollector, StorageType.Workstation, StorageType.Container, StorageType.Vehicle };
            foreach (var type in defaults)
            {
                if (!list.Contains(type))
                    list.Add(type);
            }
            StoragePriority = list;
        }
    }
}
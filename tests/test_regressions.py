from pathlib import Path
import re
import unittest
import xml.etree.ElementTree as ET


ROOT = Path(__file__).resolve().parents[1]
MANAGER = ROOT / "ProxiStackManager.cs"
PATCHES = ROOT / "Patches.cs"
HELPER = ROOT / "ContainerHelper.cs"
WINDOWS = ROOT / "Config" / "XUi_InGame" / "windows.xml"
MODINFO = ROOT / "ModInfo.xml"


def method_body(source: str, signature: str) -> str:
    start = source.index(signature)
    brace = source.index("{", start)
    depth = 0
    for index in range(brace, len(source)):
        if source[index] == "{":
            depth += 1
        elif source[index] == "}":
            depth -= 1
            if depth == 0:
                return source[brace : index + 1]
    raise AssertionError(f"Unclosed method: {signature}")


class RestockRegressionTests(unittest.TestCase):
    def test_local_restock_uses_existing_stack_only_helper(self):
        source = MANAGER.read_text(encoding="utf-8")
        body = method_body(source, "public static void PerformRestockLocal()")
        self.assertIn("FillExistingBackpackStacks", body)
        self.assertNotIn("player.bag.AddItem", body)

    def test_client_restock_uses_existing_stack_only_helper(self):
        source = MANAGER.read_text(encoding="utf-8")
        body = method_body(source, "public static void PerformRestockClient(")
        self.assertIn("FillExistingBackpackStacks", body)
        self.assertNotIn("player.bag.AddItem", body)

    def test_transfer_helper_never_targets_empty_slots(self):
        source = MANAGER.read_text(encoding="utf-8")
        self.assertIn("private static bool FillExistingBackpackStacks(", source)
        body = method_body(source, "private static bool FillExistingBackpackStacks(")
        self.assertRegex(body, r"targetStack\s*==\s*null\s*\|\|\s*targetStack\.IsEmpty\(\)")
        self.assertIn("EqualsForMerging", body)
        self.assertIn("StackTransferCount", body)
        self.assertNotIn("AddItem", body)


class BackpackButtonLayoutTests(unittest.TestCase):
    def test_buttons_use_vanilla_context_slots_not_currency_area(self):
        root = ET.parse(WINDOWS).getroot()
        for append in root.findall("append"):
            buttons = {button.attrib["name"]: button for button in append.iter("button")}
            if not buttons:
                continue
            self.assertEqual("100, -22", buttons["btnMoveRestock"].attrib["pos"])
            self.assertEqual("150, -22", buttons["btnMoveStash"].attrib["pos"])

    def test_visibility_remains_complementary_to_vanilla_loot_buttons(self):
        root = ET.parse(WINDOWS).getroot()
        for append in root.findall("append"):
            wrappers = [rect for rect in append.iter("rect") if rect.attrib.get("visible")]
            self.assertEqual("{notlootingorvehiclestorage}", wrappers[0].attrib["visible"])

    def test_visibility_binding_hides_for_any_move_destination(self):
        source = PATCHES.read_text(encoding="utf-8")
        body = method_body(source, "public static void Postfix(ref bool __result, XUiC_BackpackWindow")
        self.assertIn("ProxiStackManager.HasMoveDestinationOpen(__instance)", body)
        self.assertNotIn("AttachedToEntity", body)

    def test_move_destination_detection_reuses_vanilla_logic(self):
        source = MANAGER.read_text(encoding="utf-8")
        self.assertIn("public static bool HasMoveDestinationOpen(", source)
        body = method_body(source, "public static bool HasMoveDestinationOpen(")
        self.assertIn("TryGetMoveDestinationInventory", body)
        self.assertNotIn("IsWindowOpen", body)
        self.assertNotIn("AttachedToEntity", body)

    def test_stash_and_restock_block_while_move_destination_is_open(self):
        source = MANAGER.read_text(encoding="utf-8")
        self.assertNotIn("IsPlayerInventoryOnly", source)
        self.assertNotIn("IsLootWindowOpen", source)
        self.assertNotIn("AttachedToEntity", source)
        self.assertIn("if (HasMoveDestinationOpen()) return;", method_body(source, "public static void DoStash()"))
        self.assertIn("if (HasMoveDestinationOpen()) return;", method_body(source, "public static void DoRestock()"))


class WorkstationSourceTests(unittest.TestCase):
    def test_workstation_scan_uses_output_slot_only(self):
        """Arbeitsstationen dürfen nur das Ausgabefach als Quelle liefern.

        v1.1.6: Das Einschmelz-/Arbeitsfach (TileEntityWorkstation.input) wurde
        früher mitgescannt. Dadurch verschwanden Lehm, Kohle oder Erz aus der
        Schmiede, sobald anderswo etwas hergestellt wurde.
        """
        source = HELPER.read_text(encoding="utf-8")
        self.assertIn("workstation.output", source)
        self.assertNotRegex(source, r"workstation\.input\s*!=\s*null")
        self.assertNotIn("Stacks = workstation.input", source)

    def test_no_storage_source_ever_points_at_workstation_input(self):
        """Kein StorageSource darf jemals direkt an workstation.input hängen."""
        for name in ("ContainerHelper.cs", "Patches.cs", "ProxiStackManager.cs"):
            text = (ROOT / name).read_text(encoding="utf-8")
            self.assertNotIn("= workstation.input", text, msg=name)
            self.assertNotIn("Stacks = workstation.input", text, msg=name)


class WorkstationConsumptionTests(unittest.TestCase):
    def test_workstation_window_craft_consumption_is_patched(self):
        """Craften im Arbeitsstation-Fenster muss ebenfalls aus Quellen verbrauchen.

        v1.1.7: ItemActionEntryCraft.OnActivated verbraucht Zutaten in einem
        Arbeitsstation-Fenster nur über XUiC_WorkstationInputGrid.RemoveItems ->
        DecItem (nur Eingabe-/Materialfach). Ohne eigenen Patch bliebe alles
        andere (Kisten, Fahrzeuge, Drohnen, Ausgabefach) unverbraucht.
        """
        source = PATCHES.read_text(encoding="utf-8")
        self.assertIn('typeof(XUiC_WorkstationInputGrid), "DecItem"', source)
        self.assertIn("typeof(IList<ItemStack>)", source)
        body = method_body(source, "public static void Postfix(ItemValue _itemValue, int _count, IList<ItemStack> _removedItems")
        self.assertIn("ContainerHelper.RemoveNearbyItems", body)
        self.assertIn("__result += removed", body)

    def test_open_workstation_output_grid_is_resynced_after_removal(self):
        """Nach einem Verbrauch aus dem Ausgabefach muss die offene Ansicht nachziehen.

        v1.1.7: XUiC_WorkstationWindowGroup.syncTEfromUI() schreibt die UI-Slots
        der Grids zurück ins TileEntity (u. a. beim Start eines Craft-Vorgangs).
        Ohne Resync würde der alte Stand das verringerte Ausgabefach überschreiben.
        """
        source = HELPER.read_text(encoding="utf-8")
        self.assertIn("public static void RefreshOpenWorkstationOutputGrid(TileEntityWorkstation workstation)", source)
        self.assertIn("CurrentWorkstationOutputGrid", source)
        self.assertIn("data.GetOutputStacks()", source)
        set_modified = method_body(source, "public void SetModified()")
        self.assertIn("TileEntity is TileEntityWorkstation", set_modified)
        self.assertIn("RefreshOpenWorkstationOutputGrid", set_modified)

    def test_station_window_is_resolved_from_the_craft_click(self):
        """Das offene Stationsfenster ist nur im Craft-Klick selbst auflösbar.

        Livetest 11.10.2026: `XUi.CurrentWorkstationOutputGrid` war auch bei offenem
        Lagerfeuer-/Werkbank-Fenster null und `GetChildrenByType<XUiC_CraftingWindowGroup>()`
        fand kein Stationsfenster. Verlässlich ist nur `craftCountControl.WindowGroup.Controller`.
        """
        source = PATCHES.read_text(encoding="utf-8")
        self.assertIn('[HarmonyPatch(typeof(ItemActionEntryCraft), "OnActivated")]', source)
        self.assertIn("internal static XUiC_WorkstationWindowGroup ActiveStationWindow", source)
        # Patch-Klasse am Namen greifen (mehrere Klassen haben ein Postfix() ohne Parameter).
        start = source.index("private class Patch_ItemActionEntryCraft_OnActivated")
        patch_class = source[start:start + 2500]
        self.assertIn("ResolveStationWindow(__instance)", patch_class)
        self.assertIn("craftCountControl", patch_class)
        self.assertIn("WindowGroup.Controller as XUiC_WorkstationWindowGroup", patch_class)
        self.assertIn("RefreshWorkstationOutputGrid", patch_class)
        self.assertIn("ActiveStationWindow = null", patch_class)
        # Der Resync selbst muss TE -> UI arbeiten (SetSlots aus GetOutputStacks).
        refresh = method_body(HELPER.read_text(encoding="utf-8"),
                              "public static void RefreshWorkstationOutputGrid(XUiC_WorkstationWindowGroup wsGroup)")
        self.assertIn("outputWindow.SetSlots(", refresh)
        self.assertIn("GetOutputStacks()", refresh)
        self.assertIn("IsDirty = true", refresh)

    def test_no_diagnostic_code_in_release(self):
        """Diagnose-Code (Livetest 11.10.2026) darf nicht im Release-Build landen."""
        for name in ("Patches.cs", "ContainerHelper.cs", "ProxiStack.cs", "ProxiStackManager.cs",
                     "ProxiStackConfig.cs", "NetPackageProxiStack.cs"):
            text = (ROOT / name).read_text(encoding="utf-8")
            self.assertNotIn("ProxiStackDiag", text, msg=name)
            self.assertNotIn("[DIAG]", text, msg=name)
            self.assertNotIn("Diagnose-Build", text, msg=name)
            self.assertNotIn("DiagPatch", text, msg=name)


class VersionTests(unittest.TestCase):
    def test_bugfix_release_is_1_1_7(self):
        root = ET.parse(MODINFO).getroot()
        version_element = root.find("Version")
        if version_element is None:
            self.fail("ModInfo.xml has no Version element")
        version = version_element.attrib["value"]
        self.assertEqual("1.1.7", version)

    def test_startup_banner_matches_modinfo_version(self):
        """Startmeldung und ModInfo.xml dürfen nicht auseinanderlaufen."""
        root = ET.parse(MODINFO).getroot()
        version_element = root.find("Version")
        if version_element is None:
            self.fail("ModInfo.xml has no Version element")
        version = version_element.attrib["value"]
        source = (ROOT / "ProxiStack.cs").read_text(encoding="utf-8")
        self.assertIn(f"Initializing ProxiStack v{version}", source)


if __name__ == "__main__":
    unittest.main()

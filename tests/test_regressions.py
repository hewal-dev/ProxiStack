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


class VersionTests(unittest.TestCase):
    def test_bugfix_release_is_1_1_6(self):
        root = ET.parse(MODINFO).getroot()
        version_element = root.find("Version")
        if version_element is None:
            self.fail("ModInfo.xml has no Version element")
        version = version_element.attrib["value"]
        self.assertEqual("1.1.6", version)

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

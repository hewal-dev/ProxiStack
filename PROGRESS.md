# ProxiStack – Progress Log

> [!NOTE]
> Internes Entwicklungstagebuch (Deutsch). Enthält maschinenlokale Pfade aus der
> Entwicklungs-/Testumgebung – für Anwender ist [`README.md`](README.md) die
> maßgebliche Dokumentation.

## Goal
ProxiStack implements proximity-based crafting (crafting from nearby chests without pulling resources first) and stashing/restocking helpers (stashing matching items to nearby chests and restocking inventory items).

> [!IMPORTANT]
> **Arbeitsanweisung für den KI-Agenten (Antigravity):**
> Dieser Aufgabenplan ist interaktiv. Du musst offene Punkte aus dieser Roadmap implementieren, die vorgenommenen Änderungen kompilieren und mit `./sync.sh` synchronisieren. Sobald eine Teilaufgabe oder ein Feature vollständig gelöst und getestet wurde, musst du die dazugehörige Checkbox in dieser Datei als erledigt (`- [x]`) markieren.
> 
> **Achtung bei Referenzcodes:** Das Vorbild "ProxiCraft" ist für die Legacy-Version 7DTD v2.6 (A21) geschrieben und wurde vom Autor nicht für v3.0 aktualisiert. Code darf **unter keinen Umständen** 1:1 kopiert werden (Methodennamen wie `GetAmmoCount` statt `GetAmmoCountToReload`, Parameternamen und TileEntity-Strukturen haben sich in v3.0 stark verändert!).


> [!NOTE]
> **Stand 11.10.2026 (abgeschlossen):** In `/home/hewal/7 Days/0 einzelne mods/ProxiStack/` liegt
> wieder ein **sauberer Release-Build** (keine Diagnose mehr), Version 1.1.7,
> SHA-256 `8a7dd58600374d4760d61273c4a153dd91127e9c0b6f7e0a4f491c3cb3454a24` (Dev = Livetest).
> Von Hewal im Spiel bestätigt: Zutaten aus dem Ausgabefach einer offenen Arbeitsstation werden
> jetzt verbraucht. Offen ist nur noch das Release-ZIP (7 Dateien, ohne Harmony) sowie die
> Nexus-Textpflege. Die Harmony-Kopien in `ProxiStack/` und `Compare/` wurden am 11.10. aus dem
> Livetest entfernt (nach `archives/harmony-kopien-livetest-20261011/` verschoben);
> `0_TFP_Harmony/0Harmony.dll` des Spiels bleibt unangetastet. **Wichtig:** `sync.sh` schließt
> `0Harmony.dll`/`Harmony*`/`MonoMod*`/`Mono.Cecil*` jetzt mit aus, sonst kopiert jeder Sync die
> Build-Referenz aus dem Entwicklerordner zurück ins Livetest-Verzeichnis. `pack_release`-Wächter
> und Regressionstest `test_no_diagnostic_code_in_release` verhindern Rückfälle.

### Befund 11.10.2026, 10:42 — Log-Beleg für die Duplikat-Schleife (Diagnose-Build)

Testfall Hewal: Lagerfeuer-Ausgabefach mit 1x Wasser + 1x Chrysantheme, Rezept „Roter Tee";
danach 10x Stein + 10x Lehm.

```
CRAFT-Klick: recipeType=66052 anzahl=1 zutaten=[#0:66183x1, #1:66047x1] eingabeFach=False stationsFenster=True
  stationAusgabe TE=[Wasser,Chrysantheme,Lehm,Stein] UI=[identisch]
PlayerInventory.RemoveItems: [...]
[Verbrauch] type=66183 benoetigt=1 Quellen=1 Sorten=[Workstation]
[Verbrauch]   Quelle Workstation lieferte 1 (Ausgabefach danach=[Wasser, Lehm, Stein])
[Resync] kein offenes Ausgabefach-Fenster (CurrentWorkstationOutputGrid == null)     <-- Fix B greift nicht
  NACH Craft: stationAusgabe TE=[Lehm,Stein] UI=[Wasser,Chrysantheme,Lehm,Stein]      <-- UI hängt nach
syncTEfromUI (UI -> TE): UI-Ausgabe=[Wasser,Chrysantheme,Lehm,Stein] TE-Ausgabe=[Lehm,Stein]   <-- Altstand zurück
```

Damit ist belegt: **Der Verbrauch funktioniert** (TE-Ausgabefach sinkt korrekt), und **die
Duplikation entsteht ausschließlich bei geöffnetem Stationsfenster**, weil Vanilla die
UI-Kopie per `syncTEfromUI()` ins TileEntity zurückschreibt. Ohne offenes Fenster (Test 2:
10x Stein + 10x Lehm im Rucksack-Crafting-Menü) wurde korrekt verbraucht — Hewal bestätigt:
„Lehm und Stein war aus dem Lagerfeuer verbraucht".

Grund für das Versagen der ersten Fix-Varianten: **beide** UI-Zugriffe sind unbrauchbar —
`XUi.CurrentWorkstationOutputGrid` ist auch bei offenem Lagerfeuer-/Werkbank-Fenster `null`,
und `xui.GetChildrenByType<XUiC_CraftingWindowGroup>()` liefert kein Stationsfenster
(Logzeile 169.315: `[Resync] kein offenes Stationsfenster gefunden`). Verlässlich auflösbar ist
das Fenster nur **im Craft-Klick selbst** über `craftCountControl.WindowGroup.Controller`
(Logzeile: `stationsFenster=True`).

Fix (Patch 10b): `ItemActionEntryCraft.OnActivated` merkt sich im Prefix das Stationsfenster
(`Patches.ActiveStationWindow`), der Resync läuft damit schon **während** des Verbrauchs
(`StorageSource.SetModified()` → `RefreshOpenWorkstationOutputGrid`) und zusätzlich als
Sicherheitsnetz im Postfix (`RefreshWorkstationOutputGrid` → `outputWindow.SetSlots(GetOutputStacks())`
+ `IsDirty` + `SetAllChildrenDirty`). Fällt der Klickpfad aus, greifen weiterhin die
WindowGroup-Suche und das Property als Fallbacks.

### Verifikation 11.10.2026, 10:56 — **FIX IM SPIEL BESTÄTIGT**

```
[Resync] Ausgabefach der offenen Station aktualisiert: UI vorher=[Wasser,Chrysantheme] -> nachher=[Wasser]  TE=[Wasser]
[Resync] Ausgabefach der offenen Station aktualisiert: UI vorher=[Wasser] -> nachher=[leer]                 TE=[leer]
  NACH Craft: stationAusgabe TE=[leer] UI=[leer]
syncTEfromUI (UI -> TE): UI-Ausgabe=[leer] TE-Ausgabe=[leer]     <-- vorher stand hier der Altstand
```

- Hewal: „Ok, jetzt funktioniert es" — Lagerfeuer **und** Werkbank, Zutaten aus dem Ausgabefach
  werden verbraucht, Abbruch dupliziert nichts mehr.
- Diagnose-Code danach vollständig entfernt (Startmeldung wieder `v1.1.7`, keine `[DIAG]`-Strings
  in der DLL), 16/16 Regressionstests grün (2 neue: `test_station_window_is_resolved_from_the_craft_click`,
  `test_no_diagnostic_code_in_release`), Patch-Binde-Probe: 32 Patch-Klassen, 27 angewendet
  (die 5 bekannten Umgebungsartefakte), neuer Patch `Patch_ItemActionEntryCraft_OnActivated`
  angewendet.
- DLL-Hash Dev = Livetest: `8a7dd58600374d4760d61273c4a153dd91127e9c0b6f7e0a4f491c3cb3454a24`.
- Sicherung des Vorzustands: `archives/ProxiStack-vor-Output-Verbrauch-Fix-20261011-0949.tar.gz`.

## Release-ZIP 1.1.7 – 11.10.2026

Öffentliches Release-Archiv erstellt: `releases/ProxiStack-Proximity-Crafting-and-Stashing-1.1.7.zip`

- **7 Laufzeitdateien** (statt 8 — die Harmony-DLL ist raus): `ProxiStack/ModInfo.xml`,
  `ProxiStack/ProxiStack.dll`, `ProxiStack/ProxiStackConfig.xml`, `ProxiStack/Config/Localization.csv`,
  `ProxiStack/Config/XUi_InGame/windows.xml`, `ProxiStack/UIAtlases/UIAtlas/ui_game_symbol_restock.png`,
  `…/ui_game_symbol_stash.png`.
- Größe 86.972 Bytes (ungepackt), `validate_release_zip.py` → **PASS**, genau ein Wurzelordner
  `ProxiStack/`, Version `1.1.7` == Startmeldung, keine Quell-/Arbeitsdateien, kein Harmony.
- SHA-256: `7cec744516170e4a64a36745d0b6aea40f27c29cc38077f10f96a99d62a6481f`.
- `dist/ProxiStack/` wurde dafür aus dem aktuellen Dev-Stand aufgefrischt; DLL-Hash in
  `dist/` == Dev == Livetest: `8a7dd58600374d4760d61273c4a153dd91127e9c0b6f7e0a4f491c3cb3454a24`.
- Inhalt der ZIP: Fix A/B aus v1.1.7 (Verbrauch aus dem Ausgabefach offener Arbeitsstationen,
  Verbrauch im Stationsfenster) + Harmony-freies Paket.
- **Offen:** Upload auf Nexus/GitHub; die Nexus-Beschreibung muss dort noch auf den neuen Stand
  („keine Harmony im Paket") gebracht werden — die Textdateien im Repo sind bereits korrigiert.

## Build-Skripte korrigiert – 11.10.2026

Beim Anlegen des Compare-Repos fiel auf, dass das **veröffentlichte** `build.sh` (Stand 1.1.6)
nach der Harmony-Bereinigung nicht mehr funktionierte: es verlangte eine `0Harmony.dll` **im
Projektordner** (`$MOD_ROOT/0Harmony.dll`) — die per `.gitignore` bewusst nicht im Repo liegt —
und meldete fälschlich „Sie ist in der offiziellen Mod-Auslieferung enthalten". Fremde Builds
wären also schon an der Voraussetzungsprüfung gescheitert.

- `build.sh`: Harmony-Referenz jetzt aus der **Spielinstallation**
  (`<Spiel>/Mods/0_TFP_Harmony/0Harmony.dll`, per `HARMONY=` überschreibbar, mit Suche im
  Spielverzeichnis als Fallback) und klare Fehlermeldung; Meldung zur Auslieferung entfernt.
- `ProxiStack.csproj`: `HintPath` von `0Harmony.dll` → `$(GameDir)/Mods/0_TFP_Harmony/0Harmony.dll`
  (Windows/MSBuild-Builds hätten sonst dieselbe fehlende Referenz gehabt).
- **Nachweis:** Arbeitsbaum in eine Scratch-Kopie ohne `0Harmony.dll` gestellt → `BUILD
  SUCCESSFUL`. Die erzeugte DLL ist **nicht** byte-identisch mit dem Release-Build
  (`302d83b330498ab563b019bb8d74ca2fa4cc2acaaa9991d2e78655cd334bc140` statt `8a7dd586…`), weil
  jetzt gegen Harmony **2.13** (Spiel) statt gegen die lokale Kopie **2.4.2** kompiliert wird.
  Laufzeitverhalten unverändert: Mono bindet beim Laden auf die Spiel-Harmony (gleicher Simple
  Name); das Release-Paket 1.1.7 und der Livetest bleiben daher unangetastet.
- **Offen (optional):** 1.1.7 bei Gelegenheit gegen Harmony 2.13 neu bauen und neu ausliefern —
  rein kosmetisch bezüglich der Assembly-Referenz, erfordert aber neuen ZIP-Asset + Hash-Abgleich.

## GitHub-Veröffentlichung 1.1.7 – 11.10.2026

- **Repo:** `https://github.com/hewal-dev/ProxiStack` (public, MIT), Branch `main`.
  Account per `gh api user` gegengeprüft: `hewal-dev` (nicht `gh auth status` glauben).
- Quellcode und Doku für 1.1.7 gepusht; GitHub-Release **`v1.1.7`** mit dem ZIP als Asset
  (`ProxiStack-Proximity-Crafting-and-Stashing-1.1.7.zip`, 7 Laufzeitdateien,
  SHA-256 `7cec744516170e4a64a36745d0b6aea40f27c29cc38077f10f96a99d62a6481f`).
- Vor dem Commit geprüft: `git diff --cached --name-only` enthält **nur** Quellcode/Doku —
  keine `*.dll`, `*.zip`, `dist/`, `releases/`, `media/` (per `.gitignore` ausgeschlossen).
- **Hinweis Compare:** Für `Compare` existiert **kein** GitHub-Repo (`hewal-dev/Compare` → 404)
  und der Projektordner ist kein Git-Repository. Ein Upload der 1.0.5 dorthin würde ein neues
  öffentliches Repo bedeuten — dafür ist eine ausdrückliche Freigabe nötig.

**Gegengeprüft (nicht nur gepusht):**

- Commit auf `main`: `0c54120` (10 Dateien geändert, +565/−15) — Remote-HEAD per
  `gh api repos/…/commits/main` identisch, `git status -sb` sauber.
- Remote-Tree gelesen: 23 Dateien, keine `*.dll`/`*.zip`/`dist/`/`releases/`/`media/`.
- Release `v1.1.7` (Latest, Ziel `main`): https://github.com/hewal-dev/ProxiStack/releases/tag/v1.1.7
  — Asset `ProxiStack-Proximity-Crafting-and-Stashing-1.1.7.zip`, **36.201 Bytes**, Status
  `uploaded`, identisch mit der lokalen Datei.
- Repo-Status: public, Lizenz MIT, `pushed_at` 2026-10-11T09:19:40Z.

## Verbindliche Pfade (Stand 11.10.2026)

- Livetest-Ziel **aller** v3-Mods: `/home/hewal/7 Days/0 einzelne mods/<ModName>/`.
  Das Steam-Verzeichnis `<Game>/Mods` ist ein Symlink dorthin.
- `2 Vanilla 30/` in älteren Abschnitten dieses Logs ist historisch und wird
  nicht mehr verwendet (Einträge werden nicht rückdatiert umgeschrieben).
- `sync.sh` dieser Mod zielt ab 1.1.7 auf `0 einzelne mods/ProxiStack/`.

## Stand: 11.10.2026 — Version 1.1.7: Ausgabefach-Verbrauch an Arbeitsstationen

### Anlass (Hewal, Livetest v3.3)
> „Wenn zum Craften Items aus dem Ausgabefach genommen werden, werden diese im
> Ausgabefach der Arbeitsstationen nicht verbraucht.“

### Ursache (belegt an der installierten Assembly, nicht vermutet)

Belege per `ikdasm` gegen `Assembly-CSharp.dll` V 3.3.0. Zwei unabhängige Pfade:

**(1) Vanilla schreibt die UI-Kopie der Arbeitsstation ins TileEntity zurück.**
- `XUiC_WorkstationWindowGroup.syncTEfromUI()` (IL) überträgt die UI-Slots aller
  Grids in den TE: `SetOutputStacks(outputWindow.GetSlots())` (+ Input, Tools, Fuel).
- Aufgerufen wird das in `Update()` **genau beim Wechsel des Craft-Zustands**
  (`wasCrafting != craftingQueue.IsCrafting()`) und in `OnClose()`.
- ProxiStack verringert beim Craften `workstation.output` direkt am TE
  (`ContainerHelper.RemoveNearbyItems`). Die UI-Slots bleiben unverändert, weil
  Vanillas `XUiC_WorkstationWindowGroup.TileEntity_OutputChanged()` (genau
  `outputWindow.SetSlots(GetOutputStacks())`) in dieser Version **nirgends
  verdrahtet** ist (`grep` auf `ldftn TileEntity_OutputChanged` = 0 Treffer).
- Folge: Beim Start des Craft-Vorgangs überschreibt Vanilla das korrekt verringerte
  Ausgabefach mit dem alten UI-Stand → Items gelten als „nicht verbraucht“
  (und sind beliebig oft kostenlos nutzbar).

**(2) Im Arbeitsstation-Fenster wird gar nicht aus ProxiStack-Quellen verbraucht.**
- `ItemActionEntryCraft.OnActivated` (IL): `if (GetChildByType<XUiC_WorkstationInputGrid>()
  != null) workstationInputGrid.RemoveItems(ingredients, count, removed) else
  xui.PlayerInventory.RemoveItems(...)`.
- `XUiC_WorkstationInputGrid.RemoveItems/DecItem/GetItemCount/HasItems` arbeiten
  ausschließlich auf dem Eingabe-/Materialfach (`GetSlots()`, `XUiM_Workstation.
  SetInputStacks`); ein Fehlbetrag wird **stillschweigend ignoriert**. Dieser Pfad
  war von ProxiStack nicht gepatcht.
- Betroffen ist u. a. die Schmiede: `XUiC_WorkstationMaterialInputGrid :
  XUiC_WorkstationInputGrid` (im Fenster `workstation_forge`), wird also von
  `GetChildByType<XUiC_WorkstationInputGrid>()` mitgefunden.

### Korrektur (kleinste Änderung, weiterhin rein clientseitig)

| Ort | Änderung |
|---|---|
| `Patches.cs` | Neuer **Patch 6b**: Harmony-Postfix auf `XUiC_WorkstationInputGrid.DecItem(ItemValue,int,IList<ItemStack>)`. Ein Fehlbetrag (`_count - __result`) wird wie beim `Bag.DecItem`-Postfix aus ProxiStack-Quellen nachgezogen (`ContainerHelper.RemoveNearbyItems`, gleiche Quellen- und Prioritätsregel wie die Zählung). Deckt auch `XUiC_WorkstationMaterialInputGrid` (Schmiede) ab, da geerbt. |
| `ContainerHelper.cs` | Neuer Helfer `RefreshOpenWorkstationOutputGrid(TileEntityWorkstation)`: zieht die offene Ansicht auf den TE-Stand nach (`CurrentWorkstationOutputGrid` → `SetSlots(GetOutputStacks())` + `IsDirty = true`). Wird aus `StorageSource.SetModified()` aufgerufen, wenn die geänderte Quelle ein `TileEntityWorkstation` ist — also genau nach einem Verbrauch aus dem Ausgabefach. |

Bewusst nicht geändert: die Scan-Regel (weiterhin **nur** `workstation.output`,
nie `input`), `StoragePriority`, Stash/Restock (scannen Arbeitsstationen nicht).

### Verifikation
- Regressionstests `python3 -m unittest -v tests/test_regressions.py`: **14/14 grün**
  (2 neue: `test_workstation_window_craft_consumption_is_patched`,
  `test_open_workstation_output_grid_is_resynced_after_removal`; Versionstest 1.1.7).
- `bash build.sh`: **BUILD SUCCESSFUL**, keine Warnungen.
- **Harmony-Binde-Probe** (Mono, gegen die installierte Assembly-CSharp geladen):
  `Patch_WorkstationInputGrid_DecItem -> XUiC_WorkstationInputGrid.DecItem (postfix=1)`
  → Zielmethode existiert mit exakter Signatur und der Postfix wird angewendet.
- Memberref-Abgleich der neuen DLL (`monodis --memberref`): aufgelöst werden
  `LocalPlayerUI.get_primaryUI`, `XUi.get_CurrentWorkstationOutputGrid`,
  `XUiC_WorkstationGrid.get_WorkstationData` / `.SetSlots`,
  `XUiM_Workstation.GetOutputStacks` / `.get_TileEntity` — alle gegen Assembly-CSharp.
- `monodis --userstrings`: `[ProxiStack] Initializing ProxiStack v1.1.7` (== ModInfo.xml).
- Sicherung vor der Änderung: `archives/ProxiStack-vor-Output-Verbrauch-Fix-20261011-0949.tar.gz`.

### Erwartetes Ergebnis im Livetest
- Fertige Ware aus dem Ausgabefach kann weiterhin als Zutat dienen — sie wird jetzt
  aber tatsächlich verbraucht (Ausgabefach leert sich sichtbar, ohne Fenster-Neustart).
- Craften im Schmiede-/Arbeitsstation-Fenster zieht fehlende Zutaten aus Kisten,
  Fahrzeugen, Drohnen und Ausgabefächern nach, statt kostenlos zu sein.
- Verhalten im Rucksack-Crafting-Fenster und bei Stash/Restock unverändert.

### Offen
- [ ] **Livetest durch Hewal:** Ausgabefach einer Station befüllen (z. B. Barren in
  der Schmiede), dann etwas herstellen, das diese Ware braucht → Ausgabefach muss sich
  verringern und verringert bleiben (auch nach Schließen/Öffnen des Fensters).
- [ ] Livetest: Craften im Schmiede-Fenster mit fehlender Zutat in Kisten → Kisteninhalt
  muss sinken (kein kostenloses Craften).
- [ ] Multiplayer-Pfad des neuen DecItem-Postfixes ist ungetestet (wie bisher alle
  Verbrauchspfade nur im Singleplayer verifiziert).

---

## Stand: 11.10.2026 — Nexus-Bugreport: mitgelieferte `0Harmony.dll` gehört nicht ins Paket

### Anlass (Nexus-Bugreport zu 1.1.6)
> „Es schmeißt beim Starten halt sofort das Logfenster auf und es gibt einige Errors …
> es ist nur in der gepackten File schon eine Harmony.dll in dem Ordner. Hab die gelöscht
> und keine Errors mehr."

### Ursache (belegt per `ikdasm` gegen Assembly-CSharp V 3.3.0)
- **Das Spiel bringt Harmony selbst mit:** `<Mods>/0_TFP_Harmony/` enthält
  `0Harmony.dll` (**Harmony 2.13.0.0**), `TfpHarmony.dll` sowie MonoMod/Cecil-Assemblies.
- `ModManager.LoadMods()` sucht den Mod mit dem Namen `TFP_Harmony`
  (`'<LoadMods>b__6_0'`: `_mod.Name == "TFP_Harmony"`) und verschiebt ihn per
  `FindIndex` → `RemoveAt` → `Insert(0, …)` **an den Anfang**, bevor `InitModCode()`
  für alle Mods läuft. Die Spiel-Harmony ist also garantiert zuerst geladen.
- `ModManager.loadModsFromFolder()` ruft je Unterordner `Mod.LoadDefinitionFromFolder()`
  + `Mod.LoadMod()` und lädt dabei **jede** `*.dll` (Literal `".dll"`).
- Unser ZIP enthielt `ProxiStack/0Harmony.dll` (Harmony **2.4.2.0**, 2,4 MB) → zweite
  Assembly mit gleichem Simple Name, anderer Version. Im **eigenen** Linux-Log bindet Mono
  die Kopie still auf die Spiel-Harmony (im ProxiStack-Ordner wird
  `Loaded assembly 0Harmony (in …/0_TFP_Harmony/0Harmony.dll)` gemeldet, kein einziger
  `ERR` außer der bekannten Discord-RTC-Zeile); auf anderen Setups (Windows) gibt es die
  gemeldeten Startfehler. Deshalb haben andere Mods (ohne Harmony im Paket) das Problem nicht.

### Korrektur
- `dist/ProxiStack/0Harmony.dll` entfernt → Release-ZIP künftig **7** Runtime-Dateien,
  ~34 KiB statt 846 KiB.
- `README.md` (Installation, Build-Voraussetzungen, Third-Party) und
  `NEXUS_DESCRIPTION_DE.md` / `NEXUS_DESCRIPTION_EN.md` korrigiert: keine Harmony im Paket,
  Hinweis an Bestandsnutzer, `Mods/ProxiStack/0Harmony.dll` zu löschen.
- `evz-nexus-mod-publishing/scripts/package_release.py`: neuer Wächter — DLLs mit Präfix
  `0Harmony`/`Harmony`/`HarmonyX`/`MonoMod`/`Mono.Cecil` im Runtime-Baum brechen die
  Paketierung mit klarer Meldung ab.

### Verifikation
- Wächter negativ/positiv getestet: mit `0Harmony.dll` → Abbruch mit Meldung; ohne →
  `Runtime files: 7`, ZIP 34,3 KiB (Testlauf in Scratch, **kein** Release erzeugt).
- Compile gegen die **Spiel-Harmony 2.13** in einer Scratch-Kopie: `EXIT=0`;
  `monodis --assemblyref` der Test-DLL → `Version=2.13.0.0` (statt 2.4.2.0 bei der bisherigen
  Dev-Referenz). Beide Varianten laufen zur Laufzeit, weil Unity/Mono über den Simple Name bindet.

### Offen
- [ ] **1.1.7-ZIP bauen** (enthält Fix A/B + 7 statt 8 Dateien). Optional vorher gegen
  `0_TFP_Harmony/0Harmony.dll` neu kompilieren — Achtung: ändert den DLL-Hash, danach
  erneut syncen und Hashvergleich.
- [ ] Nach Hewals Livetest die Kopie auch aus `/home/hewal/7 Days/0 einzelne mods/ProxiStack/`
  entfernen, damit das Testverzeichnis dem ausgelieferten Paket entspricht (laut Log ohnehin
  wirkungslos, weil auf die Spiel-Harmony gebunden wird).
- [ ] Prüfen, ob weitere EVZ-Mods (z. B. `Compare`, `WMM12SlotToolbelt`) eine Harmony-Kopie im
  Ordner oder im Paket haben.

---

## Stand: 08.10.2026 — Quellcode auf GitHub veröffentlicht

### Anlass
Wunsch von Hewal (nach einer Nexus-Anfrage nach einem Label-Filter): Der Quellcode soll
öffentlich sein, damit Nutzer die Mod selbst anpassen können. Die Nexus-Beschreibung
erlaubt das bereits ausdrücklich („may be modified and included in other projects …
as long as Eltern vs Zombies is credited").

### Ergebnis
- **Repo:** `https://github.com/hewal-dev/ProxiStack` (public, MIT).
- **Hinweis zum Account:** `gh auth status` meldet `hewalguenes-crypto`, dieser Account
  existiert aber nicht (API 404). Der hinterlegte Token gehört zu **`hewal-dev`**
  (Display-Name *DerDa81*). Repo liegt daher unter `hewal-dev/ProxiStack`.
- **Aufbereitung für Fremdnutzer:**
  - `LICENSE` (MIT, Copyright *Eltern vs Zombies*) neu – README nannte MIT, Datei fehlte.
  - `.gitignore`: keine DLLs, kein `dist/`, keine `releases/`, kein `media/`, kein `bin/obj`.
  - `build.sh` entkoppelt: Spielverzeichnis wird gesucht (`GAME_DIR` / Argument,
    Linux- und Windows-Pfade), Voraussetzungen werden geprüft, `/home/hewal`-Hardcoding raus.
  - `ProxiStack.csproj` parametrisiert über `GameDir`-Property.
  - `api-research.sh` / `sync.sh`: Pfade relativ bzw. per Env überschreibbar.
  - `README.md` für Fremdnutzer neu: Installation, Build für Linux/Windows, Config-Tabelle,
    Credits und Third-Party-Hinweis (Harmony MIT, Spieldateien nicht enthalten).
- **Verifikation:** `build.sh` in einer Scratch-Kopie ausgeführt →
  **BUILD SUCCESSFUL**, erzeugte DLL byte-identisch zur Dev-DLL
  (`7c3c0b23563995b65d6de2e4cf6b6c0a07d9c145b0b6a6e86bf1885476701090`).
  `python3 -m unittest -v tests/test_regressions.py` → **12/12 grün**.
  Der Live-Testordner wurde dabei nicht angefasst.

### Offen
- [ ] README im GitHub-Repo verlinkt die Release-ZIPs – sobald ein Release mit Asset
  angelegt ist, Link ergänzen (aktuell als „Releases page" umschrieben).
- [ ] `PROGRESS.md` enthält weiterhin maschinenlokale Pfade (bewusst, als Dev-Log).

---

## Stand: 07.10.2026 — Release-ZIP 1.1.6 erstellt

- Öffentliches Release-Archiv `releases/ProxiStack-Proximity-Crafting-and-Stashing-1.1.6.zip`
  liegt bereit (846,1 KiB, 8 Laufzeitdateien). Inhalt byte-identisch mit `dist/ProxiStack/`
  und mit der im Spiel laufenden Version.
- Paketierung über `package_release.py` des Nexus-Publishing-Skills; Validierung
  (`validate_release_zip.py`) **PASS**: genau ein Wurzelordner `ProxiStack/`,
  `ModInfo.xml` mit Version `1.1.6`, keine Quell-/Arbeitsdateien, CRC fehlerfrei.
- SHA-256: `385eb17affcee8c5797ad18071aeb1f9cf9dfd7f4aacb07ebe323ad7bbb57281`.

---

## Stand: 05.10.2026 — Version 1.1.6: Arbeitsstationen liefern nur noch das A...

### Anlass (Hewal, Livetest v3.3)
> „Sie nimmt aus der Schmiede z. B. Lehm aus dem Einschmelzfach, wenn ich etwas
> herstelle, was Lehm benötigt. Aus den Arbeitsstationen soll immer nur aus den
> Ausgabefächern etwas genommen werden.\"

### Ursache (belegt)
- `ContainerHelper.GetNearbySources()` legte für eine `TileEntityWorkstation`
  **zwei** Quellen an: eine für `workstation.output` (Ausgabefach) und eine für
  `workstation.input` (Arbeits-/Einschmelzfach).
- Beide bekommen `StorageType.Workstation` und stehen in der
  `StoragePriority` (`Drone, DewCollector, Workstation, Container, Vehicle`)
  **vor** `Container`. Damit wurde das Einschmelzfach vor allen Spielerkisten
  leergezogen — sichtbar als „Lehm aus dem Einschmelzfach verschwindet\",
  obwohl genug Lehm in Kisten lag.
- Betroffen sind **alle** Stationen, die `TileEntityWorkstation` verwenden
  (Forge/Schmiede, Campfire, Workbench, Chemistry Station, Cement Mixer).
- Kein v3.3-Regressionsbruch: Der Codepfad ist byte-identisch in v1.1.0–v1.1.5
  (Belege: `archives/ProxiStack-v1.1.0.zip` … `-v1.1.2.zip` und
  `backups/gportal-3.2-2026-08-23/saves/Mods/ProxiStack/ContainerHelper.cs`,
  jeweils mit identischem `else if (config.PullFromWorkstationOutputs && te is
  TileEntityWorkstation workstation)`-Block). Das Verhalten war also schon
  vorher da — es fiel jetzt nur erst auf.

### Korrektur (kleinste Änderung, rein clientseitig)
- In `ContainerHelper.cs` wird der Block `if (workstation.input != null)` samt
  `StorageSource` **ersatzlos entfernt**. Arbeitsstationen liefern ab 1.1.6 nur
  noch `workstation.output`.
- Vanilla-Beleg: `TileEntityWorkstation` hat getrennte öffentliche Arrays
  `input` (Arbeits-/Einschmelzfach), `output` (Ausgabefach), `fuel`, `tools`
  und `queue` — geprüft per
  `monodis --fields Assembly-CSharp.dll` gegen die installierte V 3.3.0.
- Kein neuer Config-Schalter: Es gibt nur noch einen sinnvollen Zustand, das
  Ausgabefach. `PullFromWorkstationOutputs` bleibt unverändert (`true`), nur der
  Kommentar wurde präzisiert: *„output only; the input/smelting slot is never
  used\"* (`ProxiStackConfig.xml` **und** Generator in `ProxiStackConfig.cs`,
  damit Handdatei und neu erzeugte Datei nicht auseinanderlaufen).

### Bewusst nicht geändert
- **Stash/Restock** scannen Arbeitsstationen gar nicht (nur
  `TEFeatureStorage`-Container, Fahrzeuge, Drohnen) — dort gab es das Problem
  nie. Bei Bedarf nachrüstbar, aber nicht Teil dieser Änderung.
- Alle übrigen Quellen (`Container`, `Vehicle`, `Drone`, `DewCollector`)
  bleiben unberührt.

### Verifikation
- Regressionstests `python3 -m unittest -v tests/test_regressions.py`:
  **12/12 grün** (2 neue: `test_workstation_scan_uses_output_slot_only`,
  `test_no_storage_source_ever_points_at_workstation_input`; Versionstest auf
  1.1.6 angehoben).
- `bash build.sh` erfolgreich, keine Warnungen.
- DLL-Feldkontrolle (`monodis --memberref ProxiStack.dll`): genau **eine**
  `TileEntityWorkstation`-Referenz → `output`; **keine** `input`-Referenz mehr.
- `monodis --userstrings`: `[ProxiStack] Initializing ProxiStack v1.1.6`
  (Startmeldung == `ModInfo.xml`).
- XML-Prüfung: `ModInfo.xml`, `ProxiStackConfig.xml` und
  `Config/XUi_InGame/windows.xml` fehlerfrei geparst.
- Release-ZIP `releases/ProxiStack-Proximity-Crafting-and-Stashing-1.1.6.zip`:
  8 Runtime-Dateien, Wurzel `ProxiStack/`, `testzip` OK, 866406 Bytes,
  SHA-256 `385eb17affcee8c5797ad18071aeb1f9cf9dfd7f4aacb07ebe323ad7bbb57281`.
- Sync nach `/home/hewal/7 Days/0 einzelne mods/ProxiStack/` (Dry-Run zuerst;
  `--delete-excluded` auf `tests/ media/ releases/` geprüft, keine
  Nutzerdatei betroffen). Dev- und Live-Hash identisch:
  DLL `7c3c0b23563995b65d6de2e4cf6b6c0a07d9c145b0b6a6e86bf1885476701090`,
  `ModInfo.xml` `c99e2f70a4911e644bbe5b5b343f9176882bb5883e10f5d84dd52250189656d6`,
  `ProxiStackConfig.xml` `73eac25e349ea7e5516107b2c9dda4059a4912e4ff68e0714c6937b14d1debf7`.
- Vorsorgliche Sicherung vor der Änderung:
  `archives/ProxiStack-vor-Workstation-Input-Fix-20261005-1430.tar.gz`.

### Erwartetes Ergebnis im Livetest
- Schmiede/Forge: Lehm, Kohle und Erz bleiben im Einschmelzfach, auch wenn
  anderswo etwas hergestellt wird, das diese Rohstoffe braucht.
- Fertige Ware im Ausgabefach (z. B. geschmolzene Barren) wird weiterhin als
  Crafting-Quelle benutzt — das ist gewollt.
- Beim Herstellen greift ProxiStack jetzt auf **Kisten** zurück statt auf das
  Einschmelzfach (Reihenfolge: Drone, DewCollector, Workstation-Ausgabe,
  Container, Vehicle).

### Offen
- [ ] **Livetest durch Hewal:** Schmiede befüllen (Lehm + Kohle ins
  Einschmelzfach), dann irgendwo nahe etwas herstellen, das Lehm braucht →
  Einschmelzfach muss unverändert bleiben; Lehm muss aus der/den Kiste(n)
  genommen werden.
- [ ] Livetest: Ausgabefach einer Station mit Fertigware → wird weiterhin als
  Quelle genutzt (Verhalten soll erhalten bleiben).
- [ ] Falls Hewal Stash/Restock auf Arbeitsstationen **will**: eigener Auftrag,
  aktuell bewusst nicht implementiert.
- [ ] Hinweis: 7DTD lief zum Zeitpunkt des Syncs (14:30). Die neue DLL greift
  erst nach **Spielneustart**.

---

## Stand: 03.10.2026 — Version 1.1.5: Debug-Logging entfernt, toter Code beseitigt

### Anlass
Der Live-Test mit V 3.3.0 (b18) lief fehlerfrei, erzeugte aber eine **26,4 MB** große
Client-Logdatei (`output_log_client__2026-10-02__17-43-47.txt`). Auswertung:

| | |
|---|---|
| ProxiStack-Zeilen | 216.833 |
| Anteil an der Logdatei | **26,0 MB = 98 %** |
| davon `Challenge boosted` | **209.286 Zeilen** |
| davon `GetItemCount boosted` | 2.487 |
| davon `HasItems check` | 2.238 |
| davon `CanReload:` | 22 |
| FPS (Durchschnitt) | 60,0 |

Kein messbarer Performance-Einbruch (FPS stabil bei 60), aber 98 % Log-Anteil ist in
einer veröffentlichten Mod nicht vertretbar — die Datei wird bei langen Sitzungen
unbrauchbar groß und verdeckt echte Fehler.

### Ursache
Debug-Ausgaben standen direkt in heißen Patch-Pfaden. Der Challenge-Patch
(`BaseChallengeObjective.HandleUpdatingCurrent`) und die `XUiM_PlayerInventory`-Zähler
werden pro Frame bzw. pro UI-Update aufgerufen.

### Korrektur
- **`Patches.cs`:** 37 `Log.Out`-Debugzeilen entfernt (`CanReload:` 13×,
  `HasItems`/`HasItem` 10×, `GetItemCount boosted`, `DecItem boosted`,
  `GetAmmoCount:` 8×, `Challenge boosted`, `decreaseAmmo`). Behalten wurden die
  einmaligen Init-Meldungen und die zwei Transpiler-Erfolgsmeldungen.
- **`ContainerHelper.cs`:** periodischer Scan-Log entfernt; dadurch wird das Feld
  `LastCraftingScanLogTime` unbenutzt und wurde mit entfernt.
- **Reflection-Optimierung:** `MaxCount` wurde vorher **bei jedem Aufruf** per
  Reflection gesucht (`typeof(...).GetField(...)`). Jetzt einmalig gecacht
  (`_maxCountField` / `_maxCountFieldResolved` + Helfer `MaxCountOf`).
- Alle `Log.Warning` (27 in `Patches.cs`) bleiben erhalten — Fehler sollen sichtbar sein.

### Toter Code entfernt
Drei Quelldateien wurden **nie kompiliert** (standen nicht in `build.sh`) und sind in
der DLL per `monodis --typedef` nicht vorhanden:

- `CraftingTranspiler.cs` (7,1 KB) — von keinem anderen File referenziert
- `ILSpy.cs` (2,2 KB) — Debug-Transpiler, 0 Vorkommen im Log
- `VirtualInventoryProvider.cs` (4,3 KB) — nur von `CraftingTranspiler.cs` referenziert

Nach `/tmp/proxistack-deadcode/` verschoben (nicht gelöscht). Keine Referenzen mehr im
Quellbaum.

### Verifikation
- `bash build.sh` ohne Warnungen (vorher `CS0169` für das entfernte Feld).
- 10/10 Regressionstests (`test_bugfix_release_is_1_1_5` angepasst).
- DLL-Stringkontrolle: `Challenge boosted`, `HasItems postfix`, `CanReload Postfix`,
  `IL-SPY`, `GetItemCount boosted` → **0 Treffer** in der neuen DLL.
- Release-ZIP `releases/ProxiStack-Proximity-Crafting-and-Stashing-1.1.5.zip`:
  8 Runtime-Dateien, `testzip` OK, 866377 Bytes,
  SHA-256 `483659496a71ec66abd4b5d5959a99473faa8261af6303675922568acd358ed4`.
- Sync nach `/home/hewal/7 Days/0 einzelne mods/ProxiStack/`; Dev- und Live-DLL
  hashgleich `892230d6ef1f59af0ced05aa02fdfa0bd61a5933fe9817a15315da0cb44ca2a7`.

### Erwartetes Ergebnis im Livetest
- Client-Log bleibt klein (statt 26 MB); keine `Challenge boosted`-Flut mehr.
- Alle Funktionen unverändert (Stash, Restock, Crafting, Reload, Repair, Painting, Quests).

### Offen
- [ ] Livetest: Logdatei-Größe nach längerer Sitzung prüfen (Erwartung: wenige MB
  statt 26 MB).
- [ ] Livetest: Funktionen stichprobenartig bestätigen (Crafting-Liste, Stash,
  Auffüllen, Nachladen, Quest-Fortschritt mit Kisten-Items).

## Stand: 02.10.2026 — Version 1.1.4: v3.3-Kompatibilität wiederhergestellt

### Sicherung
- Vollständiger Projektstand vor der Änderung:
  `/home/hewal/Hermes/7d2d-v3-mods/archives/ProxiStack-vor-v33-Fix-20261002-1643.tar.gz`.
- Ausgangs-DLL-Hash (Arbeits- und Live-Kopie identisch):
  `b9bc792b2f04a1d61164d86bb4971dc4110d96b73cfd1da2d7705c48df586033`.

### Symptom
- Livetest mit **V 3.3.0 (b18)**. Client-Log
  `output_log_client__2026-10-02__16-36-38.txt` meldete wiederholt:
  `Could not load type of field 'ProxiStack.ContainerResult:Lootable' (1) due to:
  Could not resolve type with token 01000006 ... type:ITileEntityLootable`
  gefolgt von `Error loading types from assembly ProxiStack ... ReflectionTypeLoadException`
  aus `SdtdConsole.RegisterCommands()`, `AuthorizationManager.Init()` und `XmlPatcher::.cctor()`.
- Tragweite: **Keine einzige Mod-Funktion lief** (Crafting, Stash, Restock, Reload,
  Repair, Painting, Quests). Die Logzeilen `Harmony patches registered` und
  `ProxiStack initialization complete` sind irreführend — `InitMod` läuft vor dem
  fehlschlagenden Typ-Scan.
- Zusätzlich lud der Client die Live-Version 1.1.3, während die Startmeldung noch
  `v1.1.2` ausgab.

### Ursache (belegt, nicht vermutet)
- `ITileEntityLootable` existiert in v3.3 nicht mehr. Nachweis über
  `monodis --typedef Assembly-CSharp.dll` (V 3.3.0 b18): nur noch `ITileEntity`,
  `ITileEntityChangedListener`, `ITileEntityFeature`, `ITileEntitySignable`.
- Ein einziger unauflösbarer Feldtyp in einem öffentlichen Struct macht
  `Assembly.GetTypes()` für die **gesamte** Mod-Assembly ungültig.
- Typeref-Abgleich der alten DLL gegen die neue Assembly: `ITileEntityLootable` war
  die **einzige** fehlende Referenz (alle anderen Verdachtsfälle — `Audio.Manager`,
  `Challenges.BaseChallengeObjective`, `Challenges.ChallengeBaseTrackedItemObjective`,
  `DictionaryList`2`, `FastTags`1`, `SingletonMonoBehaviour`1` — sind vorhanden).
- Der Build deckte danach zwei weitere v3.3-Brüche auf, die noch nicht im Log
  sichtbar waren, weil die Mod nie bis dahin kam.

### Korrektur
| v3.2-API | v3.3-Ersatz |
|---|---|
| `ITileEntityLootable` | `TEFeatureStorage` (erbt `TEFeatureAbs`, implementiert `IInventory`, `IItemStackGridHolder`) |
| `lootable.items` | `TEFeatureStorage.ItemGrid.items` |
| `lootable.bPlayerStorage` | `ItemStackGrid.playerOwned` |
| `lootable.bTouched` | `ItemStackGrid.Touched` |
| `Bag.items` | `Bag.ItemGrid.items` |
| `Bag.GetSlots()` | `Bag.ItemGrid.items` (`Bag.SetSlot` bleibt erhalten) |
| `Bag.Touched = true` | `ItemStackGrid.Touch()` (setzt `worldTimeTouched`) |
| `NetPackage.GetLength()` | Override ersatzlos entfernt (kein Vanilla-Paket überschreibt es mehr) |

- `Bag.Touched` ist in v3.3 ein reiner Getter; das Setzen ist ein Compilerfehler.
- `ItemStackGrid.Touch()` dereferenziert `GameManager.Instance.World` → in
  `StorageSource.SetModified()` mit Null-Guard abgesichert.
- `EligibleContainer`-Logik zentralisiert in `ContainerHelper.IsEligibleContainer()`
  (Bedingung entspricht Vanillas `TEFeatureStorage.UpdateTick`).
- Neue Helfer: `ContainerHelper.GetStorageItems()`, `GetBagGrid()`, `GetBagItems()`.

### Verifikation
- Build gegen die v3.3-Assembly: **erfolgreich** (vorher `CS0115` + 5× `CS1061`/`CS0200`).
- Regressionstests `python3 -m unittest tests/test_regressions.py`: **10/10 grün**
  (neu: `test_startup_banner_matches_modinfo_version`).
- Typeref-Nachkontrolle der neuen DLL: **keine** `ITileEntityLootable`-Referenz mehr,
  keine `lootable`-Referenz.
- Version auf **1.1.4** in `ModInfo.xml` **und** Startmeldung angeglichen.
- Release-ZIP `releases/ProxiStack-Proximity-Crafting-and-Stashing-1.1.4.zip`:
  8 Runtime-Dateien, Wurzel `ProxiStack/`, `testzip` OK, 867825 Bytes,
  SHA-256 `f10829d429697e02c1ee4f12d8690e1508c1ebee473845068f6554d421d52399`.
- Sync nach `/home/hewal/7 Days/0 einzelne mods/ProxiStack/` (Dry-Run zuerst geprüft);
  Dev- und Live-DLL hashgleich: `75d849ffcabdd9e1c32a747a5fb273ca6c62dec938eb24f93ac69777112ec782`.

### Livetest — BESTÄTIGT (02.10.2026)
- [x] Client-Log **ohne** `ReflectionTypeLoadException` und ohne `ITileEntityLootable`
  (im Lauf `output_log_client__2026-10-02__16-56-32.txt` nachgezählt: 0 Treffer).
- [x] `Loaded Mod: ProxiStack (1.1.4)`, `Stash UI initialized` im Log.
- [x] Von Hewal im Spiel bestätigt: ProxiStack funktioniert.
- Hinweis: Aktive Testumgebung ist `0 einzelne mods` (das frühere Testziel
  `2 Vanilla 30` in älteren Abschnitten ist historisch).

### Offene Punkte
- Multiplayer-Pfad (`PerformStashClient`, `NetPackageProxiStack`) wurde durch den
  `GetLength()`-Wegfall berührt, aber nur im Singleplayer-Fall geprüft.
- `PROGRESS.md`-Altbestand nennt weiterhin das Testziel `2 Vanilla 30` und die
  Preppocalypse-Syncs; das ist historisch und nicht mehr der aktive Pfad.

## Offene Aufgaben / Feature-Roadmap

Hier ist die Liste der noch fehlenden Features im Vergleich zum Vorbild ProxiCraft:

- [x] **Waffen nachladen (`EnableForReload`)**
  - [x] Harmony-Postfix für `ItemActionRanged.CanReload` hinzufügen (prüft Munitionsbestand in nahen Kisten, verhindert Nachladen bei vollem Magazin).
  - [x] Harmony-Postfix für `AnimatorRangedReloadState.GetAmmoCount` & `Animator3PRangedReloadState.GetAmmoCount` (Munitionszählung erhöhen und Munitionsabzug umleiten via Postfix statt fehleranfälligem Transpiler).
- [x] **Fahrzeuge betanken aus Containern entfernt**
  - [x] `EntityVehicle.hasGasCan`-Boost entfernt: Benzin in Kisten/Fahrzeuglagern schaltet Tanken nicht mehr frei.
  - [x] `EntityVehicle.takeFuel` bleibt nur noch als Guard gepatcht, damit der generische `DecItem`-Fallback beim Tanken keine Container leert.
  - [x] `EnableForRefuel` aus Code und Config entfernt.
- [x] **Fahrzeuge reparieren**
  - [x] Harmony-Patch für `XUiM_Vehicle.RepairVehicle` implementieren.
- [x] **Farbpistole / Block-Texturierung (Paint Tool Ammo)**
  - [x] Harmony-Patches für `ItemActionTextureBlock.checkAmmo` und `decreaseAmmo` implementieren.
- [x] **Stromquellen/Generatoren aus Containern betanken entfernt**
  - [x] Kein Container-Fallback fürs Betanken; Tanken bleibt Vanilla-only.
- [x] **HUD Munitionsanzeige**
  - [x] Harmony-Patch für `XUiC_HUDStatBar.updateActiveItemAmmo` implementieren (bereits im Zuge des Nachladens erledigt).
- [x] **Challenge-Tracker (Fortschritt für v3.0 Quests)**
  - [x] Harmony-Patch für `BaseChallengeObjective.HandleUpdatingCurrent` implementiert (boosted `Current` mit Container-Items).
- [ ] **Multiplayer Kistensperre (TELock / TEUnlock Server)**
  - [ ] ⏸️ Zurückgestellt — erst Multiplayer-Test der bestehenden Features abwarten, dann Lock-Sync implementieren.

## Nexus-Veröffentlichung

### Stand: 17.08.2026 — Vorbereitung Version 1.1.2

- [x] Projektdateien, `ModInfo.xml`, Runtime-Bestand und öffentliche Funktionsaussagen geprüft.
- [x] Vorhandene Regressionstests ausgeführt: 9/9 erfolgreich.
- [x] Release-Build mit `bash build.sh` erfolgreich erstellt.
- [x] `ModInfo.xml`, `ProxiStackConfig.xml` und XUi-XML erfolgreich geparst.
- [x] Deutscher Prüfentwurf erstellt: `NEXUS_DESCRIPTION_DE.md`.
- [x] Öffentlichen Titel, Versionsangabe und Multiplayer-Formulierung mit Hewal abgestimmt.
- [x] Englische, kopierfertige Nexus-BBCode-Fassung erstellt und validiert: `NEXUS_DESCRIPTION_EN.md`.
- [x] Bereinigten Runtime-Baum mit acht Dateien unter `dist/ProxiStack/` erstellt.
- [x] Release-ZIP für Version 1.1.2 erstellt und Inhalt, Version und CRC geprüft:
  `releases/ProxiStack-Proximity-Crafting-and-Stashing-1.1.2.zip`.
  - Größe: 865530 Bytes
  - SHA-256: `f8fea1b056d6aedfa331a53522efa7e81f96b0e15f9101b903535928f9d604b2`
- [x] Release-ZIP für die Fehlerkorrektur in Version 1.1.3 neu erstellt:
  `releases/ProxiStack-Proximity-Crafting-and-Stashing-1.1.3.zip`.
  - Aktueller Build, XML-Prüfung und 9/9 Regressionstests erfolgreich.
  - Runtime-Baum erneut aus aktuellen Dateien erstellt; enthält acht Runtime-Dateien.
  - ZIP-Struktur, Version 1.1.3 und CRC geprüft.
  - Größe: 865480 Bytes
  - SHA-256: `78ee7b22f544ccb3236de4dbfb3b6b4c79fc417beb24a63895d47c5de36ade66`
- [x] Drei Screenshots von Hewal erhalten und daraus ein geprüftes Nexus-Medienset erstellt:
  - `media/nexus/proxistack-thumbnail-1920x1080.jpg`
  - `media/nexus/proxistack-header-1300x372.jpg`
  - `media/nexus/proxistack-gallery-01-1920x1080.jpg`
  - `media/nexus/proxistack-gallery-02-1920x1080.jpg`
  - `media/nexus/proxistack-nexus-media-preview.jpg`
- [x] Alle Medien auf korrekte Abmessungen, Dateigröße unter 8 MiB, Lesbarkeit, Verzerrung und Überlappungen geprüft.
- [x] Deutsche und englische Beschreibung nach Hewals Rückmeldung angepasst:
  - keine Erklärung zum Sperren per `Alt + Linksklick`;
  - stattdessen Hinweis, dass gesperrte Inventarplätze beim Einlagern übersprungen werden;
  - keine öffentliche Beschreibung der Hotkey-Konfiguration;
  - Multiplayer als funktionsfähig für gehostete Spiele und dedizierte Server, aber noch nicht ausgiebig getestet beschrieben.
- [x] Englische Endfassung nach der Anpassung erneut validiert; Kurzbeschreibung 204 Zeichen.

### Erkannte Punkte vor dem öffentlichen Paket

- [x] Irreführende Config-Kommentare korrigiert: Nachladen und unterstützte Challenges sind als aktiv beschrieben; Händler und Lockpicking als reserviert/nicht unterstützt gekennzeichnet.
- [x] Veraltete Startmeldung von `v1.1.0` auf `v1.1.2` korrigiert.
- [x] Nach den Textkorrekturen erneut gebaut, 9/9 Regressionstests bestanden und XML geprüft.
- [x] Livetest-Sync zuerst per Dry-Run geprüft und anschließend nach `/home/hewal/7 Days/2 Vanilla 30/ProxiStack/` ausgeführt; DLL und Config sind hashgleich.
- Die separate Server-Synchronisierung gesperrter Inventarplätze ist weiterhin offen.
- Öffentlicher Titel: `ProxiStack - Proximity Crafting and Stashing`.

---

## Stand: 17.08.2026 — Version 1.1.3: Buttons auch im Fahrzeugspeicher ausblenden

### Sicherung
- Version **1.1.2** wurde vor der Änderung als
  `/home/hewal/Hermes/7d2d-v3-mods/archives/ProxiStack-v1.1.2.zip`
  archiviert.
- ZIP-Test erfolgreich; SHA-256:
  `c4a9a4a953351984c17cbd307a67e9dd4aa0aad1411c6d3e608b0296f0828f06`.

### Ursache und Korrektur
- Version 1.1.2 erkannte nur das Fenster `looting`. Beim geöffneten
  Fahrzeugspeicher war dieses Fenster nicht aktiv, obwohl Vanillas
  Transferbuttons sichtbar waren. Dadurch überlagerten sich wieder beide
  Buttonpaare auf X=100/150.
- V3.1 berechnet das native Binding `lootingorvehiclestorage` nicht über einen
  einzelnen Fensternamen, sondern über
  `XUiC_BackpackWindow.TryGetMoveDestinationInventory(...)`.
- ProxiStack verwendet ab 1.1.3 dieselbe Vanilla-Methode zentral über
  `HasMoveDestinationOpen(...)` für Sichtbarkeit und Aktions-Guards.
- Ergebnis: ProxiStack-Buttons werden sowohl bei Kisten/Looting als auch beim
  geöffneten Fahrzeugspeicher ausgeblendet. In diesen Kontexten bleiben nur
  die passenden Vanilla-Transferbuttons sichtbar.

### Verifikation und Deployment
- Regressionstests zuerst gegen 1.1.2 fehlgeschlagen, nach der Korrektur
  **9/9 erfolgreich**.
- XML-Prüfung und `bash build.sh` erfolgreich.
- DLL enthält `HasMoveDestinationOpen(XUiC_BackpackWindow)` sowie weiterhin
  `FillExistingBackpackStacks(...)`.
- Version auf **1.1.3** erhöht und nach
  `/home/hewal/7 Days/v3 Preppocalypse/ProxiStack/` übertragen.
- Arbeits-/Test-DLL SHA-256:
  `b9bc792b2f04a1d61164d86bb4971dc4110d96b73cfd1da2d7705c48df586033`.
- Preppocalypse-`ProxiStackConfig.xml` wurde nicht überschrieben.

### Offener Livetest
- [ ] Kiste öffnen: ProxiStack-Buttons bleiben ausgeblendet.
- [ ] Fahrzeugspeicher öffnen: ProxiStack-Buttons müssen ausgeblendet sein; nur
  Vanilla-Transferbuttons dürfen sichtbar sein.
- [ ] Inventar ohne offene Transferquelle: ProxiStack-Buttons müssen weiterhin
  sichtbar und funktionsfähig sein.

---

## Stand: 17.08.2026 — Version 1.1.2: Buttons in Kisten ausblenden, auf Fahrzeugen erlauben

### Sicherung
- Version **1.1.1** wurde vor der Änderung als
  `/home/hewal/Hermes/7d2d-v3-mods/archives/ProxiStack-v1.1.1.zip`
  archiviert.
- ZIP-Test erfolgreich; SHA-256:
  `c3a97edf1d950858ec4a0e6e7e758000745e6be036cd577477cde70e280ccf10`.

### Ursache und Korrektur
- Das Binding `notlootingorvehiclestorage` prüfte bisher entgegen seinem Namen
  nur `player.AttachedToEntity`. Dadurch waren die ProxiStack-Buttons auf einem
  Fahrzeug ausgeblendet, beim Öffnen einer Kiste aber weiterhin sichtbar und
  überlagerten Vanillas Transferbuttons.
- Zusätzlich blockierte `IsPlayerInventoryOnly()` die Stash-/Restock-Aktionen
  selbst beim Fahren. Ein reines Einblenden hätte daher nicht genügt.
- Neue zentrale Prüfung `IsLootWindowOpen()` fragt über
  `LocalPlayerUI.windowManager.IsWindowOpen("looting")` ausschließlich das
  geöffnete Loot-/Kistenfenster ab.
- Verhalten ab 1.1.2:
  - Kiste/Lootfenster offen: ProxiStack-Buttons ausgeblendet; auch Hotkeys lösen
    keine Stash-/Restock-Aktion aus.
  - Fahrrad, Motorrad oder anderes Fahrzeug ohne geöffnetes Lootfenster:
    Buttons sichtbar und Aktionen erlaubt.
  - Die Reichweitensuche verwendet weiterhin die aktuelle Spielerposition und
    die konfigurierte `StashDistance`.

### Verifikation und Deployment
- 9 Regressionstests zunächst mit dem alten Verhalten fehlgeschlagen bzw. die
  vorhandenen Tests bestanden; nach der Korrektur **9/9 erfolgreich**.
- XML-Prüfung und `bash build.sh` erfolgreich.
- DLL enthält `IsLootWindowOpen()` und die vorhandene sichere
  `FillExistingBackpackStacks(...)`-Methode.
- Version auf **1.1.2** erhöht und nach
  `/home/hewal/7 Days/v3 Preppocalypse/ProxiStack/` übertragen.
- Arbeits-/Test-DLL SHA-256:
  `26a9bc52bac76ae5129dbb25b232fd367a95e61b14abd1c22103fc5ce0141eed`.
- Preppocalypse-`ProxiStackConfig.xml` wurde nicht überschrieben;
  `StashDistance` bleibt `25 25 25`.

### Livetest
- [x] Kiste öffnen: Beide ProxiStack-Buttons verschwinden; nur Vanilla-
  Transferbuttons dürfen sichtbar sein.
- [x] Fahrrad/Motorrad besteigen und Inventar öffnen: ProxiStack-Buttons sind
  sichtbar sein.
- [x] Vom Fahrzeug aus nahe Kisten: Auffüllen und Einlagern funktionieren.
- [x] Auffüllen vom Fahrzeug ergänzt weiterhin nur vorhandene Teilstacks.
- [x] Gesamtfunktion von Version 1.1.2 am 17.08.2026 durch Hewal bestätigt.
- [x] Preppocalypse-Client-Log `output_log_client__2026-08-17__15-29-33.txt`
  geprüft: Der einzige rote `ERR` stammt von Discord-RTC
  (`BuiltInAECIsAvailable: Not supported on this platform`) und erscheint auch
  im anschließenden Vanilla-Log. Kein ProxiStack-Laufzeitfehler und keine
  Exception. Einzige ProxiStack-Meldung ist eine harmlose gelbe XUi-Warnung,
  weil der optionale `windowBackpackAGF` in Preppocalypse nicht existiert; der
  normale `windowBackpack`-Patch und alle Funktionen wurden geladen.

---

## Stand: 17.08.2026 — Version 1.1.1: universelles Button-Layout und sicheres Auffüllen

### Sicherung
- Der unveränderte Stand **1.1.0** wurde vor der Bearbeitung als
  `/home/hewal/Hermes/7d2d-v3-mods/archives/ProxiStack-v1.1.0.zip`
  archiviert.
- ZIP-Test erfolgreich; SHA-256:
  `9ed041399b4608b716d36b8947fe6af3cf92caf1f3e9d2f84c5eb22d0b5ba9f1`.

### Button-Layout
- **Ursache:** Die bisherigen lokalen Buttonpositionen X=200/250 lagen durch
  den X=320-Offset von `ContainerStandardControls` global bei X=520/570 und
  damit direkt im Vanilla-Geldbereich X=492–577.
- **Korrektur:** Auffüllen und Einlagern verwenden jetzt die kontextabhängig
  freien Vanilla-Transferplätze X=100/150. ProxiStack zeigt sie nur bei
  `{notlootingorvehiclestorage}`; Vanillas eigene Transferbuttons an denselben
  Positionen erscheinen nur im Gegenkontext.
- Preppocalypse 60/96 BBM verändert Backpack-Größe und Grid, lässt den
  verwendeten Vanilla-Header aber bestehen. Deshalb ist keine separate
  Preppocalypse-Kompatibilitätsmod erforderlich.

### Auffüllen
- **Ursache:** Local- und Client-Pfad haben jeden Kistenstack ungefiltert an
  `player.bag.AddItem(item)` übergeben. Diese API darf leere Rucksackslots
  belegen und erzeugte dadurch neue Stacks für beliebige Kistenitems.
- **Korrektur:** Beide Pfade verwenden jetzt
  `FillExistingBackpackStacks(...)`.
  - Leere Rucksackslots werden immer übersprungen.
  - Nur über `EqualsForMerging` kompatible vorhandene Stacks werden gefüllt.
  - `StackTransferCount` begrenzt jede Übertragung auf die freie Kapazität des
    konkreten Zielstacks.
  - `Bag.AddItem` wird beim Auffüllen nicht mehr verwendet.
  - Ein Container wird nur bei einer tatsächlichen Entnahme als geändert
    markiert.

### Verifikation und Deployment
- 6 Regressionstests zunächst mit dem alten Verhalten fehlgeschlagen und nach
  der Korrektur erfolgreich (`python -m unittest -v tests/test_regressions.py`).
- XUi- und ModInfo-XML erfolgreich geparst.
- `bash build.sh` erfolgreich; neue DLL enthält
  `FillExistingBackpackStacks(Bag, ItemStack[])`.
- Version auf **1.1.1** erhöht.
- Ausschließlich Runtime-Dateien nach
  `/home/hewal/7 Days/v3 Preppocalypse/ProxiStack/` übertragen.
- Arbeits- und Test-DLL sind hashgleich:
  `585f2152f764c7adb53f5023767105946c1632c3ad25a3813cd066baac704e39`.
- Die angepasste Preppocalypse-Konfiguration wurde bewusst bewahrt;
  `StashDistance` bleibt dort `25 25 25`.

### Livetest
- [x] Preppocalypse: Beide Buttons liegen links vom Geldbetrag und sind
  anklickbar.
- [x] Leerer Rucksack + gefüllte Kisten: Auffüllen übernimmt nichts.
- [x] Vorhandener Teilstack + großer Kistenstack: nur vorhandenen Stack bis zum
  Maximum füllen, keinen neuen Stack erzeugen.
- [x] Andere Kistenitems bleiben unverändert.
- [x] Preppocalypse-Log geprüft: kein roter ProxiStack-Fehler; Details im
  Livetest-Abschnitt von Version 1.1.2.
- [ ] Multiplayer-Client-Pfad bei Gelegenheit separat verifizieren.

---

## Stand: 04.07.2026 — Tanken aus Containern entfernt
- **Entscheidung**: Das Betanken aus Kisten/Fahrzeuglagern wird aus ProxiStack entfernt, weil der Multiplayer-Pfad zu fehleranfällig war und kostenloses Tanken ermöglichen konnte.
- **Code-Änderung**:
  - `Patch_EntityVehicle_hasGasCan` wurde entfernt. Benzin in nahen Containern schaltet den Tank-Button nicht mehr frei.
  - `Patch_EntityVehicle_takeFuel` bleibt nur als Guard bestehen: Während Vanilla tankt, wird `ActionState.IsVehicleRefuelActive` gesetzt, damit die generischen `Bag.DecItem`-/`Inventory.DecItem`-Postfixes keine Container als Fallback leeren.
  - `EnableForRefuel` wurde aus `ProxiStackConfig.cs` und `ProxiStackConfig.xml` entfernt.
- **Erwartetes Verhalten**: Tanken funktioniert wieder nur mit Benzin im normalen Spieler-Inventar/Toolbelt nach Vanilla-Regeln. Container, Fahrzeuglager und Kisten werden fürs Tanken ignoriert.
- **Offen**: Im Spiel kurz prüfen: Client ohne Benzin im Inventar, aber Benzin im Motorrad/Kiste → Tank-Option darf nicht verfügbar sein bzw. kein Container-Benzin verbrauchen.
- **Ad-hoc-Verifikation**: `/tmp/hermes-verify-dazd2dvq.py` geprüft (anschließend gelöscht): kein `hasGasCan`-Patch mehr, kein `EnableForRefuel` mehr in Code/XML, Guard aktiv, Build+Sync erfolgreich, Arbeits- und Livetest-DLL identisch (`35f881624ae3380cf537c31b99c3fa704564fcf5521915394a326c399920e74f`).

---

## Stand: 2026-06-20 — IL-Spy Findings & Rewrite

### Problem
Initial attempts to patch crafting methods (such as `HasIngredientsForRecipe`, `CanCraft` prefix/postfix) succeeded in displaying chest counts in the UI but caused the "Craft" button to fail with *"you do not have the required items"*.

### IL-Spy Debugging
A runtime IL-Spy hook revealed that none of the following standard XUI methods call `Bag.GetItemCount` or `Bag.DecItem` directly:
- `SetRecipe`
- `updateRecipeData`
- `AddRecipeToCraftAtIndex`
- `CraftingQueue.Update`
- `RecipeStack.Update`

### Solution for v3.0 Proximity Crafting
In 7DTD v3.0, item checks and consumption are handled through:
- `XUiM_PlayerInventory.GetItemCount`
- `XUiM_PlayerInventory.DecItem`
- `XUiM_PlayerInventory.HasItems` / `HasItem`
- `Recipe.CanCraft` / `CanCraftAny`
- Transpiling `XUiC_RecipeCraftCount.calcMaxCraftable` and `ItemActionEntryCraft.hasItems` (which call `GetAllItemStacks` directly) to merge nearby container items dynamically.

### Fixes Applied:
1. **Ambiguous Matches**: Resolved by target patching specific signatures of `GetItemCount`.
2. **Deep Cloning**: Avoided duplicate items and inventory duplication by performing deep clones of `ItemStack` objects before merging.
3. **Redundant Postfixes**: Removed duplicate count additions in UI binding lookups.

---

## Stand: 2026-06-25 — UI Placement & Refinements

### UI Position & Vehicle Behavior
- **Positioning**: Moved the Stash and Restock buttons to positions `250` and `200` to prevent overlapping vanilla control buttons.
- **Riding / Vehicles**: Hidden the buttons when riding a vehicle (`player.AttachedToEntity != null`) to avoid accidental inputs.
- **Multi-container Support**: Allowed stashing/restocking even when viewing a container inventory locally.

### Performance Optimization
- **Chunk-Lookup**: Replaced grid-scanning of individual coordinates with an optimized chunk-based scanner. It scans the 9 chunks around the player and filters their cached tile-entities. This reduced operations from thousands to under 50, completely eliminating UI stuttering.

### 3D Configuration
- **Dimensional Crafting / Stashing**: Converted distance ranges to 3-dimensional Vectors (e.g. `25 25 25` and `15 15 15`), enabling distinct vertical vs. horizontal ranges.
- **Separate Config Options**: Separated repair and upgrade settings, and commented the XML configuration with descriptive English guides.

---

## Stand: 25.06.2026 — Lokalisierungs-Migration & neue UI-Symbole
- **Format-Migration**: Die Mod wurde vollständig von der veralteten `Localization.txt` auf `Localization.csv` im v3.0-Format migriert. Das neue CSV-Dateiformat und dessen Header wurden verifiziert.
- **Deutsche Bezeichnungen**:
  - `lblStashStack` (Stash/Grüner Button) wurde in "Einlagern" geändert.
  - `lblStashRestock` (Restock/Grauer Button) wurde in "Auffüllen" geändert.
- **Neue UI-Symbole**: Die Icons für `ui_game_symbol_stash.png` (Auslagern / Einlagern) und `ui_game_symbol_restock.png` (Auffüllen) wurden durch die neuen Grafiken des Users ersetzt und auf die originalen 120x120 Pixel herunterskaliert. Die grüne Einfärbung im XML-Layout für das Auslagern-Symbol bleibt bestehen.
- **Dokumentation**: Die Dateiregeln wurden in `AGENTS.md` und der Mod-`README.md` hinterlegt, um zukünftige Fehlimplementierungen mit `.txt` auszuschließen.

---

## Stand: 25.06.2026 — Multiplayer-Fix (Verbindungsabbruch behoben)
- **Problem**: Bei Aktionen im Multiplayer kam es zu einem Disconnect der verbundenen Clients (z. B. beim Sortieren/Stashen).
- **Ursache**: Ein Fehler in `NetPackageProxiStack.GetLength()` lieferte eine falsche Bytegröße zurück (`10 + count * 12` anstelle von tatsächlich geschriebenen `7 + count * 6` Bytes). Dadurch geriet der Netzwerkstream aus dem Takt (Desynchronisation). Folgepakete (wie `NetPackageWaypoint`) lasen daraufhin korrupte Streamdaten und warfen eine `Attempted to read past the end of the stream`-Exception, was zum Verbindungsabbruch führte.
- **Lösung**: 
  - Die Methode `GetLength()` in [NetPackageProxiStack.cs](file:///home/hewal/Hermes/7d2d-v3-mods/ProxiStack/NetPackageProxiStack.cs) wurde so korrigiert, dass sie exakt der Bytegröße der geschriebenen Daten entspricht (`7 + (offsets * 6)`).
  - Fehlende Overrides für `PackageDirection` (auf `Both`) und `AllowedBeforeAuth` (auf `false`) wurden zur Sicherheit ergänzt.
- **Kompilierung & Deployment**: DLL erfolgreich neu gebaut und in die Testumgebung kopiert.

---

## Stand: 26.06.2026 — Multiplayer-Paketregistrierung (Fix)
- **Problem**: Im Multiplayer trennte der Host die Verbindung der Mietspielerin mit einem "Unknown NetPackage ID: 193" Fehler, sobald sie den "Einlagern"-Button drückte.
- **Ursache**: `NetPackageProxiStack` wurde in der `NetPackageManager.knownPackageTypes` Liste auf dem Server nicht registriert. Das lag daran, dass der statische Konstruktor `.cctor` von `NetPackageManager` alle Assemblies nach NetPackages scannt, *bevor* die C#-Mods geladen werden. Der Client hatte es durch eine andere Lade-Reihenfolge lokal unter ID 193 registriert. Da der Server es nicht gesendet hat, wurde es beim Handshake nicht synchronisiert. Der Client sendete ID 193, die dem Host unbekannt war.
- **Lösung**: 
  - Manuelle Registrierung des NetPackages in `NetPackageManager.knownPackageTypes` via Reflection innerhalb von `InitMod` (in [ProxiStack.cs](file:///home/hewal/Hermes/7d2d-v3-mods/ProxiStack/ProxiStack.cs)) implementiert.
  - Dadurch ist das Paket beim Start des Servers/Clients registriert und wird beim Handshake synchronisiert.
- **Kompilierung & Deployment**: DLL erfolgreich neu gebaut und in `/home/hewal/7 Days/2 Vanilla 30/ProxiStack/` sowie das Steam-Mods-Verzeichnis kopiert.

---

## Stand: 26.06.2026 — Plattform-übergreifender ID-Handshake (Fix für "Unknown NetPackage ID: 193")
- **Problem**: Trotz erfolgreicher Handshake-Synchronisierung sendete der Windows-Client an den Linux-Host weiterhin das Paket `NetPackageProxiStack` mit der ID `193` (Client-ID). Der Linux-Host besaß jedoch nur 190 Pakete (0..189) und trennte die Verbindung wegen des ungültigen IDs.
- **Ursache**: Durch unterschiedliche Betriebssysteme (Linux Host vs. Windows Client) unterscheidet sich die Anzahl der standardmäßigen NetPackages (z.B. Xbox/Steam Core-Pakete). Da `GetPackageId` standardmäßig aus `packageClassToPackageId` ausliest, war der Wert dort fälschlicherweise auf dem Client-Standardwert (193) geblieben oder desynchronisiert.
- **Lösung**:
  - Implementierung eines Harmony-Prefix-Patches `Patch_NetPackageManager_GetPackageId` in [Patches.cs](file:///home/hewal/Hermes/7d2d-v3-mods/ProxiStack/Patches.cs).
  - Wenn `GetPackageId(typeof(NetPackageProxiStack))` aufgerufen wird, durchsucht der Patch das von dem Host übermittelte `packageIdToClass`-Array nach dem Typ `NetPackageProxiStack` und gibt dessen synchronisierten Index (z.B. `189`) zurück.
  - Dadurch wird unabhängig von der Plattform und lokalen Paketanzahl stets die korrekte, mit dem Host vereinbarte ID gesendet.
  - *Korrektur*: Parametername im Harmony-Prefix von `_packageType` auf `_type` korrigiert, da Harmony Argumente namentlich bindet und `NetPackageManager.GetPackageId` den Parameternamen `_type` verwendet. Ohne diese Übereinstimmung schlug das Laden des Patches im Client und Server fehl.
- **Kompilierung & Deployment**: DLL erfolgreich kompiliert und in `/home/hewal/7 Days/2 Vanilla 30/ProxiStack/` deployt.

---

## Stand: 26.06.2026 — Fehlende ID-Serialisierung (Endgültiger Fix für "Unknown NetPackage ID: 193")
- **Problem**: Der Windows-Client stürzte auf dem Linux-Host weiterhin mit `Unknown NetPackage ID: 193` ab, obwohl der Harmony-Patch auf dem Client aktiv war und die korrekte, synchronisierte ID `189` im Log ausgab.
- **Ursache**: In `NetPackageProxiStack.cs` fehlte der Aufruf von `base.write(_writer)` in der überschriebenen Methode `write()`. In 7 Days to Die schreibt die Basisklasse `NetPackage.write` die Paket-ID (2 Bytes, `uint16`) in den Datenstrom. Ohne diesen Aufruf wurde die ID übersprungen und stattdessen die Spieler-ID (`playerEntityId` = `193`) als erstes in den Stream geschrieben. Der Host las diese ersten 2 Bytes als Paket-ID (`193`) und stürzte ab, da ID 193 auf dem Server unbekannt war.
- **Lösung**:
  - `base.write(_writer);` am Anfang von `NetPackageProxiStack.write(PooledBinaryWriter _writer)` in [NetPackageProxiStack.cs](file:///home/hewal/Hermes/7d2d-v3-mods/ProxiStack/NetPackageProxiStack.cs) hinzugefügt.
  - Die Mod wurde erfolgreich neu kompiliert und auf dem Host deployt.

---

## Stand: 27.06.2026 — Crafting-Bugs behoben (UI-Highlighting & Multiplayer-Client)
- **Problem 1 (UI-Highlighting)**: Rezepte wurden in der Craftingliste fälschlicherweise weiß hervorgehoben (als craftbar angezeigt), obwohl benötigte Zutaten fehlten.
- **Ursache**: Im Patch für `Recipe.CanCraftAny` wurde das Ergebnis auf `true` gesetzt, sobald *mindestens eine* Zutat (entweder im Inventar oder im Container) gefunden wurde. Da `CanCraftAny` im Hauptspiel jedoch prüft, ob das Rezept auf *irgendeinem Tier* komplett herstellbar ist (also *alle* Zutaten für mindestens ein Tier vorhanden sind), führte dies zu falschen UI-Hervorhebungen.
- **Lösung**: `Patch_Recipe_CanCraftAny` in [Patches.cs](file:///home/hewal/Hermes/7d2d-v3-mods/ProxiStack/Patches.cs) so umgeschrieben, dass es wie `CanCraft` prüft, ob *alle* Zutaten vorhanden sind.
- **Problem 2 (Client-Multiplayer-Crafting)**: Die Mitspielerin (Client) konnte im Multiplayer gar nicht aus Containern craften.
- **Ursache**: Die Hilfsmethode `IsModSafeForMultiplayer()` gab für Clients im Multiplayer immer `false` zurück, wodurch alle containerbezogenen Patches bei ihr inaktiv blieben.
- **Lösung**: `IsModSafeForMultiplayer()` in [Patches.cs](file:///home/hewal/Hermes/7d2d-v3-mods/ProxiStack/Patches.cs) so angepasst, dass sie immer `true` zurückgibt, da die Bestandsveränderungen via `SetModified()` und das Standard-Netzwerk-Protokoll von 7DTD sicher synchronisiert werden.

---

## Stand: 29.06.2026 — Ausschluss von ungelooteten POI-Containern
- **Problem**: Rezepte (z.B. der Motorrad-Lenker) wurden fälschlicherweise als craftbar (weiß hervorgehoben) angezeigt, obwohl das dafür benötigte Klebeband im Spieler-Rucksack und in den eigenen Kisten fehlte.
- **Ursache**: Der Reichweitenscan durchsuchte unterschiedslos alle Container im 3D-Radius, einschließlich unberührter (nicht geöffneter) Beutebehälter der Spielwelt (POIs, Autos, etc.). Da in der Nähe der Spielerbasis noch ungelootete Behälter mit Klebeband existierten, zählte die Mod diese fälschlicherweise als verfügbare Ressourcen.
- **Lösung**: [ContainerHelper.cs](file:///home/hewal/Hermes/7d2d-v3-mods/ProxiStack/ContainerHelper.cs) wurde so angepasst, dass Behälter ignoriert werden, wenn sie weder vom Spieler platziert wurden (`composite.PlayerPlaced`), noch als typische Spieler-Lagerkisten klassifiziert sind (`lootable.bPlayerStorage`), noch jemals geöffnet/durchsucht wurden (`lootable.bTouched`). Ungelootete POI-Behälter fallen damit sicher aus dem Raster.

- **Problem (Double-Counting beim Crafting-Check)**: Trotz des Filter-Fixes wurden Rezepte in der Craftingliste fälschlicherweise als herstellbar angezeigt, wenn der Spieler nur einen Teil der benötigten Ressourcen hatte (z.B. 23 von 400 benötigten Schwarzpulver, aber 23 wurde fälschlicherweise als ausreichend gewertet).
- **Ursache**: Double-Counting! In `Patch_Recipe_CanCraft` und `Patch_Recipe_CanCraftAny` wurde die Item-Anzahl aus dem übergebenen `_itemStack` gelesen. Da unsere Prefix-Patches für die Rezeptliste und die Herstellmenge den Container-Inhalt bereits vorab in diese Item-Liste gemischt haben, enthielt `_itemStack` bereits alle Container-Ressourcen. Im Postfix-Patch wurde dann jedoch bei fehlender Differenz *erneut* `ContainerHelper.GetNearbyItemCount` aufgerufen und fälschlicherweise addiert, wodurch Kisten-Ressourcen doppelt gezählt wurden.
- **Lösung**: Da unsere Prefix-Patches (die das Inventar mit den Kisten mergen) bereits alle relevanten Crafting-Prüfstellen abdecken und die Vanilla-Evaluationsmethoden (`CanCraft` / `CanCraftAny`) dadurch automatisch das korrekte Gesamtergebnis berechnen, waren die Postfix-Patches redundant. Ich habe `Patch_Recipe_CanCraft` und `Patch_Recipe_CanCraftAny` in [Patches.cs](file:///home/hewal/Hermes/7d2d-v3-mods/ProxiStack/Patches.cs) vollständig entfernt. Die Item-Bestände werden nun wieder exakt einmal gezählt.

---

## Stand: 04.07.2026 — Fahrzeuge betanken (EnableForRefuel) implementiert — später entfernt
- **Historie**:
  - Harmony-Postfix-Patch `Patch_EntityVehicle_hasGasCan` für `EntityVehicle.hasGasCan` wurde zunächst hinzugefügt, um Tanken aus nahen Kisten/Fahrzeuglagern freizuschalten.
  - Harmony-Prefix/Postfix-Patch `Patch_EntityVehicle_takeFuel` wurde zunächst hinzugefügt, um `ActionState.IsRefuelActive` während des Betankens zu setzen.
- **Status aktuell**: Dieses Feature wurde wegen Multiplayer-Problemen wieder entfernt. Siehe Abschnitt „Tanken aus Containern entfernt“.

---

## Stand: 04.07.2026 — Fahrzeuge reparieren (EnableForRepair) implementiert & Double-Counting behoben
- **Implementierung**:
  - Harmony-Prefix/Postfix-Patch `Patch_XUiM_Vehicle_RepairVehicle` für `XUiM_Vehicle.RepairVehicle` hinzugefügt.
  - Dieser aktiviert während der Ausführung `ActionState.IsRepairActive = true`, wodurch unsere bestehenden, optimierten `GetItemCount`- und `DecItem`-Patches für den Rucksack automatisch die Reparaturkits (`resourceRepairKit`) aus nahen Kisten/Behältern zählen und abziehen.
- **Fehlerbehebung (Double-Counting & Double-Consumption Popup)**:
  - **Problem**: Bei Fahrzeug-Reparatur wurde die doppelte Anzahl an verfügbaren Reparaturkits angezeigt (z.B. 10000 statt 5000) und beim Reparieren wurden laut UI-Popup 2 Kits abgezogen (obwohl real nur 1 Kit aus der Kiste entfernt wurde).
  - **Ursache**: Da `RepairVehicle` intern `Bag.GetItemCount` und `Inventory.GetItemCount` aufruft und die Summe bildet, haben beide Patches die Kistenmenge addiert (Double-Counting). Das UI berechnete die Mengenänderung über die Gesamtsumme, wodurch nach dem echten Abzug von 1 Kit die Summe um 2 abfiel (Double-Consumption UI Fehler).
  - **Lösung**: Der Patch `Patch_Inventory_GetItemCount` wurde vollständig entfernt. Kisteninhalte werden nun bei aktivem Upgrade/Repair nur noch einmal über `Bag.GetItemCount` hinzugerechnet. Dadurch stimmt die Summenbildung, die Anzeige zeigt korrekte Bestände und das UI-Popup zählt exakt 1 abgezogenes Item.
- **Kompilierung & Deployment**: DLL erfolgreich gebaut und via `sync.sh` ins Livetest-Verzeichnis übertragen.

---

## Stand: 04.07.2026 — Farbpistole / Block-Texturierung (EnableForPainting) implementiert
- **Implementierung**:
  - Neue Konfigurationsoption `EnableForPainting` (Standard: `true`) in `ProxiStackConfig.cs` und `ProxiStackConfig.xml` hinzugefügt.
  - Harmony-Prefix-Patches für `ItemActionTextureBlock.checkAmmo` und `decreaseAmmo` implementiert. 
  - `checkAmmo` prüft bei Fehlen von Farbe im Inventar, ob Farbe in nahen Kisten/Behältern existiert.
  - `decreaseAmmo` ermittelt die Farbkosten aus `BlockTextureData.list[idx].PaintCost`, verbraucht die Farbe prioritär aus dem Inventar des Spielers und zieht den verbleibenden Bedarf automatisch aus Kisten/Behältern ab.
- **Kompilierung & Deployment**: DLL erfolgreich gebaut und via `sync.sh` ins Livetest-Verzeichnis übertragen.

---

## Stand: 04.07.2026 — Challenge-Tracker (EnableForQuests) implementiert
- **Implementierung**:
  - Harmony-Postfix-Patch `Patch_BaseChallengeObjective_HandleUpdatingCurrent` für `BaseChallengeObjective.HandleUpdatingCurrent` hinzugefügt.
  - Der Patch prüft, ob die Objective-Instanz ein `ChallengeBaseTrackedItemObjective` ist (Gather, GatherByTag, GatherIngredient, Harvest).
  - Über Reflection wird das `expectedItem`-Feld ausgelesen und der Container-Scan durchgeführt.
  - Der `Current`-Wert der Objective wird um die in nahen Kisten gefundene Item-Anzahl erhöht (gecapped auf `MaxCount`).
  - Nutzt die existierende Konfigurationsoption `EnableForQuests` (Standard: `true`).
- **v3.0 Besonderheit**: Anders als in v2.6 hat `ChallengeObjectiveGather` in v3.0 kein eigenes `HandleUpdatingCurrent`. Die Methode wird von `BaseChallengeObjective` geerbt. Unser Postfix-Patch greift auf der Basisklasse und prüft per `as ChallengeBaseTrackedItemObjective`, ob es sich um ein item-basiertes Objective handelt.
- **Kompilierung & Deployment**: DLL erfolgreich gebaut und via `sync.sh` ins Livetest-Verzeichnis übertragen.

---

## Stand: 28.10.2026 — v1.1.8: XUi-Fix für 7DTD v3.4 (Stash-/Restock-Buttons fehlten)

**Symptom (Nutzer):** Nach dem Update auf 7 Days to Die **V 3.4.0 (b6)** waren die beiden Buttons im Inventar (Restock/Einlagern) verschwunden.

**Befund (belegt, neuestes Client-Log 28.10. 13:33):**
```
WRN XML patch for "XUi_InGame/windows.xml" from mod "ProxiStack" did not apply:
  <append xpath="/windows/window[@name='windowBackpack']/rect[@name='header']/rect[@controller='ContainerStandardControls']"
```
- **Ursache:** Vanilla hat `ContainerStandardControls` in `windowBackpack` verschoben. Bis v3.3 lag der Controller in `rect[@name='header']`; ab v3.4 steht er als eigenes `rect[@name='actions']` (pos 0,-46, height 40) unterhalb der Kopfzeile. Der XPath zielte ins Leere → kein einziger Button im Fenster → auch die `OnPress`-Verdrahtung in `Patches.cs` (Patch 1, `GetChildById`) findet nichts.
- **Regression belegt, nicht angenommen:** `did not apply`-Zeilen pro Log: v3.3 = 1 (nur der optionale AGF-Zweig), v3.4 = 2 (AGF **und** Standard-Backpack). Neue Signatur tritt ausschließlich unter 3.4.0 (b6) auf.
- **Kein C#-Bruch:** Mono.Cecil-Member-Audit der 1.1.7-DLL gegen die 3.4-Assemblies → 0 fehlende Symbole. Es war reines XML.

**Neue Anker-Struktur (aus `Data/Config/XUi_InGame/windows.xml`, v3.4.0 b6):**
```
window[@name='windowBackpack']
  rect[@name='header']                       height 43 – Icon, Name, Geldbetrag, Trash
  rect[@name='actions']  controller=ContainerStandardControls  pos 0,-46 height 40
    rect[@name='filters']                    Kategorie-/Textfilter
    rect[@move_start_bottom_left='true']     rechtsbündig, pos ${parentinnerwidth-162}, width 160
      iconbutton btnToggleLockMode   pos 20,-20
      button     btnSort             pos 60,-20
      rect pos="0, 0"
        button   btnMoveFillAndSmart pos 100,-20   (nur beim Looten aktiv)
        button   btnMoveAll          pos 140,-20   (nur beim Looten aktiv)
```

**Fix (v1.1.8):** `Config/XUi_InGame/windows.xml` hängt die beiden Buttons jetzt in den rechtsbündigen Block der `actions`-Leiste:
`/windows/window[@name='windowBackpack']/rect[@name='actions']/rect[@move_start_bottom_left='true']`
mit `pos="100, -20"` (Restock) und `pos="140, -20"` (Stash) – also **genau auf den Vanilla-Transferbutton-Slots**, mit der bestehenden komplementären Sichtbarkeit `{notlootingorvehiclestorage}`. Beim Looten/Fahrzeugzugriff verschwinden unsere Buttons, Vanillas Fill/All werden aktiv; ohne geöffneten Behälter liegen unsere Buttons über den (ausgegrauten) Vanilla-Slots.
- Der AGF-Zweig (`windowBackpackAGF`) bleibt unverändert: Er scheitert ohne installiertes AGF-BackpackPlus erwartbar und harmlos (in v3.3 genauso).
- Version gemeinsam in `ModInfo.xml` und Startmeldung (`ProxiStack.cs`) auf 1.1.8 gehoben, DLL neu gebaut.

**Verifikation (tatsächlich ausgeführt):**
- XPath-Ziel gegen die installierte Vanilla-Datei mit einem .NET-`XPathNavigator`-Simulator geprüft: **genau 1 Treffer**, Zielknoten `rect pos=${parentinnerwidth-162},0 width=160`; Kinder vorher = Lock(20)/Sort(60)/rect(0,0), eingefügte Buttons 100/140. Der AGF-Zweig hat 0 Treffer (erwartet).
- Semantik der Auffindung im Spiel-IL geprüft: `XUiController.GetChildByType<T>()` und `GetChildById()` sind **rekursiv** → `ProxiStackManager.ContainerControls` findet den Controller weiterhin (jetzt direktes Kind des Fensters), `GetChildById("btnMoveStash"/"btnMoveRestock")` findet die Buttons eine Ebene tiefer.
- `bash build.sh` grün; `pytest tests/test_regressions.py` → **17 passed** (zwei Tests an die neue Struktur angepasst, ein neuer Regressionswächter `test_vanilla_patch_targets_v34_actions_control_block`).
- Member-Audit der neu gebauten DLL gegen 3.4 → 0 fehlende Symbole.
- `sync.sh` nach `/home/hewal/7 Days/0 einzelne mods/ProxiStack/`: DLL-Hash identisch (`78b5c819…`), `windows.xml` identisch, `ModInfo` = 1.1.8.
- `sync.sh` um `--exclude ".pytest_cache/"` ergänzt (Dry-Run wollte den Pytest-Cache in den Lade-Ordner kopieren).

**Offen / noch nicht bestätigt:**
- **Optik im Spiel nicht geprüft** (kein Client-Start aus der Sitzung heraus). Beim nächsten Start prüfen: Buttons sitzen rechts neben dem Sortier-Icon; beim Öffnen einer Kiste/eines Fahrzeugspinds verschwinden sie und Vanillas Fill/All sind bedienbar; Tooltips `lblStashRestock`/`lblStashStack` erscheinen; Geldbetrag/Münze in der Kopfzeile bleiben frei.
- Falls die actions-Leiste in einem späteren Build erneut umgebaut wird, fällt der Patch wieder sichtbar mit `did not apply` aus (kein stiller Fehlschlag).

---

## Stand: 28.10.2026 — v1.1.9: Buttons in die Kopfzeile (keine Überlagerung der Vanilla-Buttons)

**Anlass (Nutzer-Rückmeldung nach v1.1.8):** Die Buttons lagen exakt auf den Vanilla-Transferbuttons (`btnMoveFillAndSmart` 100, `btnMoveAll` 140) – sichtbar, aber unschön.

**Platzanalyse (v3.4.0 b6, `windowBackpack`, 603 breit):** Die `actions`-Leiste ist vollständig belegt – Filter-Icon 5–35, Such-Icon 45–75, Kategorie-Grid 100–440, Textsuchfeld 80–330, rechtsbündiger Block 441–601 (Lock 461 / Sort 501 / Fill 541 / All 581). In der Leiste ist kein Platz für zwei weitere Icons, ohne Vanilla-Elemente zu verschieben.

**Kopfzeile (`rect[@name='header']`, height 43) hat dagegen Freiraum:** Icon 21, Titel 42–222, `btnClearInventory` 364–396 (nur Kreativmodus), Geldbetrag-Label 492–572, Münze 565–589. Frei: 400–476.

**Umsetzung (v1.1.9):** Buttons jetzt in `rect[@name='header']` auf `pos="420, -22"` (Restock) und `pos="460, -22"` (Stash) – 24 px Abstand zum Trash-Button und 16 px zum Geldbetrag. Sichtbarkeit bleibt komplementär (`{notlootingorvehiclestorage}`).

**Dabei behobene Falle (sonst tote Buttons):** `XUiController.GetChildById` durchsucht nur den **eigenen Teilbaum**. Die Verdrahtung in `Patches.cs` holte die Buttons über `ProxiStackManager.ContainerControls` (= `rect[@name='actions']`); in der Kopfzeile wäre sie `null` geblieben → Buttons sichtbar, aber ohne `OnPress`. Neu: `ProxiStackManager.FindButton(string id)` sucht rekursiv ab dem Backpack-Fenster und nutzt `ContainerControls` nur noch als Fallback. `ContainerControls`/`GetChildByType` bleiben für `HasMoveDestinationOpen` unverändert nötig.

**Verifikation (ausgeführt):**
- XPath-Simulator gegen die installierte Vanilla-Config: Ziel `windowBackpack/rect[@name='header']` = **1 Treffer**; Kinder bestätigt (Icon 21, Titel 42, Trash 380, Geldbetrag 492, Münze 577) → 420/460 kollidiert mit nichts.
- `pytest tests/test_regressions.py` → **18 passed**; zwei neue Wächter: `test_vanilla_patch_targets_header_free_space` (XPath muss `header` sein) und `test_button_lookup_is_window_wide_not_container_scoped` (Verdrahtung darf nicht ausschließlich ab `ContainerControls` suchen); Positions-Test prüft jetzt die Kollisionsfreiheit gegen Trash (396) und Geldbetrag (492).
- `build.sh` grün, Member-Audit der DLL gegen 3.4 → 0 fehlende Symbole, Startmeldung im Binary `Initializing ProxiStack v1.1.9`.
- `sync.sh` → DLL-Hash identisch (`3eff1ff0…`), `windows.xml` und `ModInfo.xml` identisch im Live-Ordner.

**Offen:**
- Optik im Spiel weiterhin unbestätigt (kein Client-Start aus der Sitzung). Beim nächsten Start prüfen: die zwei Icons sitzen in der Kopfzeile zwischen Mülltonne und Geldbetrag, ohne Überlappung; Klick auf Restock/Stash wirkt (Verdrahtung), Tooltips erscheinen; beim Looten verschwinden sie.
- Falls die Icons zu dicht am Geldbetrag wirken: `pos` auf 400/440 verschieben (eine Zeile, Testgrenzen 396/492 bleiben gültig).

---

## Stand: 28.10.2026 — v1.1.9 auf Nexus Mods veröffentlicht

**Aktion:** Datei-Update auf der bestehenden Mod-Seite `nexusmods.com/7daystodie/mods/12082` (kein neuer Entwurf, kein Publish-Schritt nötig).

- **Archiv:** `releases/ProxiStack-Proximity-Crafting-and-Stashing-1.1.9.zip` (35,8 KB, SHA-256 `f6ffd9892c103bcccb91290c29c7d9b0f75f4d84e28f8990578a5afabdaade49`), `validate_release_zip.py` = PASS (7 Runtime-Dateien, Wurzel `ProxiStack`, Version 1.1.9). DLL im ZIP = DLL im Live-Ordner (`3eff1ff0…`).
- **Upload:** Datei-Zeile per „Update existing file“ ersetzt; **Display name** `ProxiStack for 7D2D 3.4`, **File version** `1.1.9`, „Update mod version to match this file's version“ gesetzt, „Archive existing file“ bewusst nicht. 1.1.7 bleibt unter Old files downloadbar.
- **Changelog (öffentlich, Logs-Tab, anonym verifiziert):**
  `Fixed the stash and restock buttons missing on 7 Days to Die 3.4 (new inventory layout).`
  `Buttons moved to the inventory header so they no longer cover the vanilla buttons.`
  (Erst mit vorangestellten `- ` eingetragen → Nexus rendert selbst Bullets, daher per „More actions → Edit changelog“ auf reinen Text umgestellt.)
- **Verifikation anonym (Firecrawl, ohne Session):** Mod-Version 1.1.9, `Last updated 28 Oct 2026, 9:03AM`, Main-File „ProxiStack for 7D2D 3.4“ 35 KB, Changelog-Eintrag unter Version 1.1.9 vorhanden. Datei stand kurzzeitig auf „Virus scanning is in progress“ (normal).
- **Upload-Session:** kopierte Nexus-Cookies waren abgelaufen (Token-Rotation) → Brave-Kopie des Profils sichtbar auf dem Desktop gestartet, Login durch Hewal, danach per CDP gesteuert. Profilkopie nach dem Upload gelöscht.

**Offen:**
- **GitHub-Pendant fehlt:** EVZ-Mods erscheinen normalerweise doppelt. Für v1.1.9 gibt es noch kein Commit/Release im Quell-Repo `hewal-dev/ProxiStack` (letztes Release 1.1.6). Auf Wunsch nachziehen, inkl. Quellen für den Kopfzeilen-Fix und `FindButton`.
- Die Datei-Zeile trägt weiterhin die alte Kurzbeschreibung „Fix for Duplicate Bug, removed shipped harmony.dll“ (Feld ist nur über den Update-Formularweg erreichbar); bei der nächsten Version mitpflegen.

### Nachtrag: Quell-Repo nachgezogen (GitHub)

- **Commit:** `d3ea267` – „ProxiStack 1.1.9 – Inventar-Buttons für 7 Days to Die 3.4 repariert“ (windows.xml, Patches.cs, ProxiStackManager.cs, ProxiStack.cs, ModInfo.xml, tests, sync.sh, PROGRESS).
- **Release:** https://github.com/hewal-dev/ProxiStack/releases/tag/v1.1.9 (Latest), Asset `ProxiStack-Proximity-Crafting-and-Stashing-1.1.9.zip`, 36 696 Bytes = lokale Datei, State `uploaded`. Tag zeigt auf den Release-Commit `d3ea267`.
- **Gleiches Archiv wie Nexus** (SHA-256 `f6ffd989…`), Version identisch 1.1.9 – beide Downloads sind byte-gleich.
- **Portabler Build bewiesen:** `git archive HEAD` in ein leeres Verzeichnis entpackt, dort `build.sh` mit explizitem Spielverzeichnis → `BUILD SUCCESSFUL`, erzeugte DLL `3eff1ff0…` **identisch** zur ausgelieferten DLL. Der Repo-Baum enthält keine Binaries/ZIPs (Grep über den Remote-Tree: leer), Lizenz MIT.

### Nachtrag 2: Zeile unter der Version („File description") — Versuch dokumentiert

**Ziel (Nutzer-Konvention, gilt für alle Mods):** Die Zeile unter der Versionsnummer im Files-Tab nennt nur die unterstützte Spielversion, z. B. `for 7DTD V3.4`. Festgehalten in `../NexusMods-Beschreibungsstandard.md` und in der Skill `evz-nexus-mod-publishing`.

**Befund: Das Feld lässt sich für eine bestehende Datei nicht überschreiben (verifiziert 28.10.2026).**
- Es liegt im Update-Formular unter **File options** als `Description (n/255)`, Platzhalter „Provide additional information for this specific file if relevant". Alter Wert: „Fix for Duplicate Bug, removed shipped harmony.dll" (aus 1.1.7 geerbt).
- Fünf Wege probiert: nativer Value-Setter + input/change, `execCommand('insertText')`, zeichenweise CDP-Tastatureingaben, das Zeilenmenü **Edit → File options**, und der komplette Ersatz-Upload („Update existing file"). In allen Fällen stand der neue Text unmittelbar vor `Save file` im Feld — nach dem Speichern zeigte sowohl das Formular als auch die öffentliche Seite wieder den alten Text. Bewertung: **serverseitig nicht beschreibbar**, nicht unser Fehler.
- **Nebenwirkung aufgeräumt:** Jeder `Save file` auf einer bereits veröffentlichten Version erzeugt einen **zusätzlichen Datei-Datensatz** gleichen Namens (Status „Old", die Versionsnummer im Formular wurde dabei automatisch auf 1.1.10 hochgezählt und einmal so gespeichert). Diese vier Duplikate wurden über das Zeilenmenü **More actions → Archive** entfernt. Öffentlich sichtbar sind jetzt: Main 1.1.9 + Old 1.1.7 / 1.1.6 / 1.1.3.
- Die Mod-Version steht wieder korrekt auf **1.1.9** (war zwischenzeitlich durch den fehlgeschlagenen 1.1.10-Save auf 1.1.10 gesprungen).

**Offen:** Die alte Fix-Zeile bleibt bis auf Weiteres stehen. Beim nächsten echten Upload (neue Version) einmal gegenprüfen, ob Nexus das Feld bei einer *neuen* Datei übernimmt — falls ja, dort `for 7DTD <Version>` setzen. Falls nein: Feld als nicht pflegbar dokumentieren (Support-Ticket wäre der einzige Weg).

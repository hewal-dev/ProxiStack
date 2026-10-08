# ProxiStack - Proximity Crafting and Stashing – deutscher Nexus-Prüfentwurf

## Mod-Titel

`ProxiStack - Proximity Crafting and Stashing`

## Kurzbeschreibung

ProxiStack verwendet Gegenstände aus nahe gelegenen Lagern automatisch zum Herstellen, Nachladen, Reparieren und für weitere Basisaktionen. Zusätzlich kannst Du passende Gegenstände mit einem Klick einlagern, vorhandene Rucksackstapel auffüllen und gesperrte Inventarplätze beim Einlagern überspringen.

## Was macht die Mod?

In einer gut ausgebauten Basis verteilen sich Materialien, Munition und Werkzeuge schnell auf viele Kisten. Für jede Herstellung oder Reparatur alles erst in den Rucksack zu holen, unterbricht den Spielfluss und erzeugt unnötige Inventararbeit.

ProxiStack bezieht geeignete Gegenstände direkt aus erreichbaren Lagern ein. Dadurch kannst Du unter anderem herstellen, Waffen nachladen, Blöcke und Fahrzeuge reparieren sowie mit der Farbpistole arbeiten, ohne die benötigten Gegenstände vorher manuell herauszunehmen. Zwei zusätzliche Inventar-Schaltflächen helfen beim Einlagern und Auffüllen.

Das Betanken von Fahrzeugen und Generatoren aus Lagern wird bewusst nicht verändert. Dafür gelten weiterhin die Vanilla-Regeln.

## Funktionen

- Herstellung mit Materialien aus nahe gelegenen Lagern;
- Nachladen von Waffen und Werkzeugen mit passender Munition aus Lagern;
- Reparieren von Blöcken und Fahrzeugen mit Materialien beziehungsweise Reparaturkits aus Lagern;
- optionales Verbessern von Blöcken mit gelagerten Baumaterialien;
- Nutzung gelagerter Farbe mit der Farbpistole;
- Berücksichtigung gelagerter Gegenstände bei passenden Sammel-Herausforderungen;
- „Einlagern“ verschiebt passende Rucksackgegenstände in nahe Lager;
- einmaliges Einlagern füllt vorhandene Lagerstapel, ein Doppelklick innerhalb von zwei Sekunden darf zusätzlich neue Stapel in freien Lagerplätzen anlegen;
- „Auffüllen“ ergänzt ausschließlich bereits vorhandene Rucksackstapel und belegt keine leeren Rucksackplätze;
- gesperrte Inventarplätze werden beim Einlagern übersprungen;
- getrennt einstellbare 3D-Reichweiten für Herstellungs- und Lageraktionen;
- konfigurierbare Farben, Lagerpriorität, Lagerquellen und Teilfunktionen;
- Unterstützung für Spieler-Lager, Fahrzeuge, aktive Drohnen und Taukollektoren entsprechend der Konfiguration; bei Arbeitsstationen wird ausschließlich das Ausgabefach als Quelle genutzt (das Einschmelz-/Arbeitsfach nie);
- ungelootete POI-Behälter werden nicht als frei verfügbare Materiallager verwendet.

## Bedienung

1. Stelle Deine Lager innerhalb der konfigurierten Reichweite auf. Standardmäßig beträgt die Herstellungsreichweite `25 25 25` Blöcke und die Reichweite für Einlagern/Auffüllen `15 15 15` Blöcke.
2. Öffne das Herstellungsmenü oder führe eine unterstützte Aktion aus. ProxiStack zählt und verbraucht passende Gegenstände automatisch aus erreichbaren Lagern.
3. Nutze im Rucksack die Schaltflächen „Einlagern“ und „Auffüllen“. Gesperrte Inventarplätze werden beim Einlagern übersprungen.
4. Bei geöffnetem Loot- oder Kistenfenster werden die ProxiStack-Schaltflächen ausgeblendet, damit sie nicht mit den Vanilla-Transferbuttons überlappen. Auf einem Fahrzeug bleiben sie ohne geöffnetes Lootfenster nutzbar.

## Konfiguration

Beim ersten Start wird im Mod-Ordner die Datei `ProxiStackConfig.xml` erstellt. Dort lassen sich Reichweiten, Farben, Lagerquellen, Verbrauchspriorität und einzelne Funktionen anpassen.

## Installation

1. Lade das Archiv herunter und entpacke es.
2. Kopiere den vollständigen Ordner `ProxiStack` nach `7 Days To Die/Mods/`.
3. Deaktiviere EAC.
4. Starte das Spiel vollständig neu.

Erwarteter Pfad:

`7 Days To Die/Mods/ProxiStack/ModInfo.xml`

## Voraussetzungen

- 7 Days to Die v3.0;
- EAC muss deaktiviert sein;
- keine separat herunterzuladende Abhängigkeit – die benötigte Harmony-Bibliothek ist im Paket enthalten;
- die Mod funktioniert im Mehrspielerbetrieb mit gehosteten Spielen und dedizierten Servern, wurde dort aber noch nicht ausgiebig getestet. Sie muss auf dem Host beziehungsweise Server und auf allen teilnehmenden Clients installiert sein.

## Kompatibilität

ProxiStack erweitert Inventar-, Herstellungs-, Munitions-, Reparatur- und Lagerabläufe. Konflikte sind deshalb vor allem mit Mods möglich, die dieselben Spielmethoden oder den Kopfbereich des Rucksackfensters verändern.

Für `windowBackpackAGF` ist ein zusätzlicher UI-Eintrag vorhanden. Andere stark veränderte Rucksackfenster können eine kleine XUi-Kompatibilitätsanpassung benötigen.

Eine universelle Kompatibilität mit allen Overhauls oder UI-Mods wird nicht zugesichert.

## Bekannte Einschränkungen

- Tanken aus Kisten, Fahrzeuglagern oder anderen entfernten Lagern wird nicht unterstützt.
- Händlerkäufe und Lockpicking verwenden derzeit weiterhin die Vanilla-Inventarregeln.
- Die Lagerreichweite ist dreidimensional und auf maximal 127 Blöcke je Achse begrenzt.
- Die Schaltflächen „Einlagern“ und „Auffüllen“ sind bei geöffnetem Lootfenster absichtlich ausgeblendet.
- Stark angepasste Rucksackfenster können einen XUi-Kompatibilitätspatch benötigen.

## Deinstallation

Beende das Spiel und entferne den Ordner `7 Days To Die/Mods/ProxiStack/`. ProxiStack fügt keine eigenen Weltblöcke oder Gegenstände hinzu; eine besondere Savegame-Bereinigung ist nicht vorgesehen. Erstelle wie üblich vorher eine Sicherung Deines Spielstands.

## Fehler melden

Bitte nutze den Reiter `Bugs` auf der Nexus-Modseite und gib möglichst Folgendes an:

- genaue Spielversion und Buildnummer;
- verwendete ProxiStack-Version;
- Einzelspieler oder Mehrspieler sowie Host/Server/Client;
- genaue Schritte zum Reproduzieren;
- relevante weitere Mods, besonders Inventar-, UI-, Crafting- oder Overhaul-Mods;
- Screenshot, falls das Problem sichtbar ist;
- relevanter Ausschnitt aus dem Client-Log.

## Credits

Entwicklung: **Eltern vs Zombies**

YouTube: https://www.youtube.com/@ElternVSZombies

Enthaltene Bibliothek: Harmony / HarmonyX-kompatible Harmony-Bibliothek (`0Harmony.dll`).

## Berechtigungen

Die Mod darf verändert und in andere Projekte wie Overhauls eingebunden werden, sofern **Eltern vs Zombies** als ursprünglicher Autor genannt wird.

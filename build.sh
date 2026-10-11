#!/usr/bin/env bash
# ProxiStack Build Script (7 Days to Die v3.x, Linux / Mono)
#
# Verwendung:
#   bash build.sh                 # Spielverzeichnis automatisch suchen
#   bash build.sh /pfad/zum/spiel # Spielverzeichnis explizit angeben
#   GAME_DIR=/pfad/zum/spiel bash build.sh
#
# Voraussetzung: Mono (`mcs`). Unter Debian/Ubuntu: sudo apt install mono-devel

set -u

MOD_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
OUT_DLL="$MOD_ROOT/ProxiStack.dll"

# ---------------------------------------------------------------------------
# 1) Spielverzeichnis bestimmen
# ---------------------------------------------------------------------------
find_game_dir() {
    local candidates=(
        "${GAME_DIR:-}"
        "$HOME/.steam/steam/steamapps/common/7 Days To Die"
        "$HOME/.local/share/Steam/steamapps/common/7 Days To Die"
        "$HOME/.steam/steam/steamapps/common/7 Days to Die"
        "/mnt/c/Program Files (x86)/Steam/steamapps/common/7 Days To Die"
    )
    local c
    for c in "${candidates[@]}"; do
        [ -n "$c" ] || continue
        if [ -d "$c/7DaysToDie_Data/Managed" ]; then
            printf '%s\n' "$c"
            return 0
        fi
    done
    return 1
}

GAME_DIR="${1:-$(find_game_dir || true)}"

if [ -z "$GAME_DIR" ] || [ ! -d "$GAME_DIR/7DaysToDie_Data/Managed" ]; then
    echo "FEHLER: Spielverzeichnis nicht gefunden." >&2
    echo "Bitte explizit angeben:  bash build.sh \"/pfad/zu/7 Days To Die\"" >&2
    echo "oder:                    GAME_DIR=\"/pfad/zu/7 Days To Die\" bash build.sh" >&2
    exit 1
fi

MANAGED="$GAME_DIR/7DaysToDie_Data/Managed"
# Harmony NUR als Compile-Referenz aus der Spielinstallation (nie aus dem Mod-Ordner):
# 7 Days to Die liefert <Mods>/0_TFP_Harmony/0Harmony.dll (Harmony 2.13) selbst mit.
HARMONY="${HARMONY:-$GAME_DIR/Mods/0_TFP_Harmony/0Harmony.dll}"

echo "=== ProxiStack Build ==="
echo "Spielverzeichnis: $GAME_DIR"
echo "Ausgabe:          $OUT_DLL"
echo

# ---------------------------------------------------------------------------
# 2) Voraussetzungen prüfen
# ---------------------------------------------------------------------------
if ! command -v mcs >/dev/null 2>&1; then
    echo "FEHLER: 'mcs' (Mono C# Compiler) nicht gefunden." >&2
    echo "Installieren mit: sudo apt install mono-devel   (bzw. das Paket deiner Distribution)" >&2
    exit 1
fi

if [ ! -f "$HARMONY" ]; then
    FOUND="$(find "$GAME_DIR" -name '0Harmony.dll' 2>/dev/null | head -1)"
    if [ -n "$FOUND" ]; then
        HARMONY="$FOUND"
    else
        echo "FEHLER: 0Harmony.dll nicht gefunden." >&2
        echo "Erwartet: $GAME_DIR/Mods/0_TFP_Harmony/0Harmony.dll" >&2
        echo "Sie gehört zur Spielinstallation und darf NICHT in den Mod-Ordner kopiert werden." >&2
        echo "Alternativ per HARMONY=/pfad/0Harmony.dll übergeben." >&2
        exit 1
    fi
fi

MISSING=0
for req in Assembly-CSharp.dll LogLibrary.dll UnityEngine.dll; do
    if [ ! -f "$MANAGED/$req" ]; then
        echo "FEHLER: Referenz fehlt: $MANAGED/$req" >&2
        MISSING=1
    fi
done
[ "$MISSING" -eq 0 ] || exit 1

# netstandard.dll (kommt mit Mono)
NETSTANDARD="$(find /usr/lib/mono -name "netstandard.dll" -path "*/4.8-api/*" 2>/dev/null | head -1)"
if [ -z "$NETSTANDARD" ]; then
    NETSTANDARD="$(find /usr/lib/mono -name "netstandard.dll" 2>/dev/null | head -1)"
fi
if [ -z "$NETSTANDARD" ]; then
    echo "FEHLER: netstandard.dll nicht gefunden (Mono-Installation prüfen)." >&2
    exit 1
fi

# ---------------------------------------------------------------------------
# 3) Kompilieren
# ---------------------------------------------------------------------------
cd "$MOD_ROOT"

ARGS=(
    -target:library
    -out:"$OUT_DLL"
    -lib:"$MANAGED"
    -r:Assembly-CSharp.dll
    -r:LogLibrary.dll
    -r:UnityEngine.dll
    -r:UnityEngine.CoreModule.dll
    -r:UnityEngine.IMGUIModule.dll
    -r:UnityEngine.UI.dll
    -r:UnityEngine.UIModule.dll
    -r:UnityEngine.InputModule.dll
    -r:UnityEngine.InputLegacyModule.dll
    -r:UnityEngine.TextRenderingModule.dll
    -r:UnityEngine.AnimationModule.dll
    -r:"$HARMONY"
    -r:"$NETSTANDARD"
    ProxiStack.cs
    ProxiStackConfig.cs
    ContainerHelper.cs
    ProxiStackManager.cs
    Patches.cs
    NetPackageProxiStack.cs
)

mcs "${ARGS[@]}"

if [ $? -eq 0 ]; then
    echo
    echo "BUILD SUCCESSFUL"
    ls -lh "$OUT_DLL"
    echo
    echo "Installation: ProxiStack.dll zusammen mit ModInfo.xml, Config/ und UIAtlases/"
    echo "in den Ordner .../7 Days To Die/Mods/ProxiStack/ kopieren."
    exit 0
else
    echo
    echo "BUILD FAILED" >&2
    exit 1
fi

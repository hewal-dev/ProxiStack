#!/bin/bash
# v3.x API Research Tool
# Sucht Methoden/Signaturen in der Assembly-CSharp.dll des installierten Spiels.
#
# Spielverzeichnis automatisch suchen oder per GAME_DIR übergeben:
#   GAME_DIR="/pfad/zu/7 Days To Die" bash api-research.sh method 'GetTileEntity'

if [ -n "${GAME_DIR:-}" ] && [ -d "$GAME_DIR/7DaysToDie_Data/Managed" ]; then
    MANAGED="$GAME_DIR/7DaysToDie_Data/Managed"
else
    MANAGED=""
    for c in "$HOME/.steam/steam/steamapps/common/7 Days To Die" \
             "$HOME/.local/share/Steam/steamapps/common/7 Days To Die"; do
        if [ -d "$c/7DaysToDie_Data/Managed" ]; then MANAGED="$c/7DaysToDie_Data/Managed"; break; fi
    done
fi

if [ -z "$MANAGED" ]; then
    echo "FEHLER: Spielverzeichnis nicht gefunden. GAME_DIR setzen." >&2
    exit 1
fi

DLL="$MANAGED/Assembly-CSharp.dll"

case "$1" in
    "method")
        # Suche nach Methode in einer Klasse
        monodis --method "$DLL" 2>/dev/null | grep "$2" | head -20
        ;;
    "class")
        # Suche nach Klassendefinition
        monodis --typedef "$DLL" 2>/dev/null | grep "$2" | head -20
        ;;
    "find")
        # Suche in allen Strings
        strings "$DLL" | grep -i "$2" | sort -u | head -20
        ;;
    *)
        echo "Usage: $0 {method|class|find} <pattern>"
        echo ""
        echo "Examples:"
        echo "  $0 method 'GetTileEntity'"
        echo "  $0 method 'lockedTile\|LockedTile'"
        echo "  $0 method 'StashItems'"
        echo "  $0 method 'UICamera\|Input.GetKey'"
        echo "  $0 class 'XUiC_'"
        echo "  $0 find 'StashItems'"
        ;;
esac
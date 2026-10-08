#!/bin/bash
# ProxiStack Sync Script (Entwickler-Werkzeug)
# Kopiert nur spielrelevante Dateien in das Livetest-Verzeichnis und schließt
# Quellcode/Notizen/Build-Artefakte aus.
#
# Ziel überschreibbar: LIVETEST="/pfad/zu/Mods/ProxiStack" bash sync.sh

LIVETEST="${LIVETEST:-/home/hewal/7 Days/2 Vanilla 30/ProxiStack/}"

echo "=== Syncing ProxiStack to Live Test ==="
rsync -av --delete --delete-excluded \
    --exclude "*.cs" \
    --exclude "*.md" \
    --exclude "*.sh" \
    --exclude "*.csproj" \
    --exclude ".git/" \
    "/home/hewal/Hermes/7d2d-v3-mods/ProxiStack/" "$LIVETEST"

echo "✅ Sync completed!"

#!/bin/bash
# ProxiStack Sync Script (Entwickler-Werkzeug)
# Kopiert nur spielrelevante Dateien in das Livetest-Verzeichnis und schließt
# Quellcode/Notizen/Build-Artefakte aus.
#
# Ziel überschreibbar: LIVETEST="/pfad/zu/Mods/ProxiStack" bash sync.sh

LIVETEST="${LIVETEST:-/home/hewal/7 Days/0 einzelne mods/ProxiStack/}"

echo "=== Syncing ProxiStack to Live Test ==="
rsync -av --delete --delete-excluded \
    --exclude "*.cs" \
    --exclude "*.md" \
    --exclude "*.sh" \
    --exclude "*.csproj" \
    --exclude ".git/" \
    --exclude ".gitignore" \
    --exclude "0Harmony.dll" \
    --exclude "Harmony*.dll" \
    --exclude "MonoMod*.dll" \
    --exclude "Mono.Cecil*.dll" \
    --exclude "dist/" \
    --exclude "releases/" \
    --exclude "media/" \
    --exclude "tests/" \
    --exclude ".pytest_cache/" \
    --exclude "__pycache__/" \
    "/home/hewal/Hermes/7d2d-v3-mods/ProxiStack/" "$LIVETEST"

echo "✅ Sync completed!"

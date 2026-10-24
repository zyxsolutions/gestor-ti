#!/usr/bin/env bash
# Instala/atualiza o ZYX Core no Linux (AppImage) e cria o atalho no menu de aplicativos.
# Uso:  curl -fsSL https://raw.githubusercontent.com/zyxsolutions/gestor-ti/main/install-linux.sh | bash
set -euo pipefail

REPO="zyxsolutions/gestor-ti"
DEST="$HOME/.local/share/zyx-core"
APPIMAGE="$DEST/ZYXCore.AppImage"

echo "Procurando a última versão..."
URL=$(curl -fsSL "https://api.github.com/repos/$REPO/releases/latest" \
  | grep -o '"browser_download_url": *"[^"]*\.AppImage"' | head -n1 | sed 's/.*"\(http[^"]*\)"/\1/')
if [ -z "${URL:-}" ]; then
  echo "Nenhum AppImage encontrado na última versão. Veja https://github.com/$REPO/releases" >&2
  exit 1
fi

mkdir -p "$DEST" "$HOME/.local/share/applications"
echo "Baixando $URL"
curl -fL --progress-bar -o "$APPIMAGE.tmp" "$URL"
chmod +x "$APPIMAGE.tmp"
mv "$APPIMAGE.tmp" "$APPIMAGE"
curl -fsSL "https://raw.githubusercontent.com/$REPO/main/brand/logo-mark-512.png" -o "$DEST/icone.png" || true

cat > "$HOME/.local/share/applications/zyx-core.desktop" <<EOF
[Desktop Entry]
Type=Application
Name=ZYX Core
Comment=Gestão de operações de TI
Exec=$APPIMAGE
Icon=$DEST/icone.png
Categories=Office;Utility;
Terminal=false
EOF

echo
echo "Pronto! Abra 'ZYX Core' no menu de aplicativos ou rode: $APPIMAGE"
echo "Se o AppImage não abrir, instale o FUSE: sudo apt install libfuse2   (Debian/Ubuntu)"

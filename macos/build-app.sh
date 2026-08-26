#!/bin/bash
# Builds aViewer.app from the Swift package.
#
# Command Line Tools are enough: there is no Xcode project and no xcodebuild.
# Pass SIGN_IDENTITY to sign with a Developer ID; without it the bundle is
# ad-hoc signed, which works but makes macOS treat each rebuild as a new
# application, so the Accessibility permission has to be granted again.
set -euo pipefail

cd "$(dirname "$0")"

CONFIGURATION="${CONFIGURATION:-release}"
APP_NAME="aViewer"
BUNDLE="build/${APP_NAME}.app"
VERSION_FILE="../VERSION.txt"
BUILD_VERSION="$(tr -d '[:space:]' < "$VERSION_FILE" 2>/dev/null || echo '1.0.0')"
SHORT_VERSION="$(echo "$BUILD_VERSION" | cut -d. -f1-3)"

echo "==> Building ($CONFIGURATION)"
if [ "${UNIVERSAL:-1}" = "1" ] && \
   swift build -c "$CONFIGURATION" --arch arm64 --arch x86_64 2>/dev/null; then
    BINARY="$(swift build -c "$CONFIGURATION" --arch arm64 --arch x86_64 --show-bin-path)/AViewerMac"
else
    echo "    universal build unavailable, building for the host architecture"
    swift build -c "$CONFIGURATION"
    BINARY="$(swift build -c "$CONFIGURATION" --show-bin-path)/AViewerMac"
fi

echo "==> Assembling $BUNDLE"
rm -rf "$BUNDLE"
mkdir -p "$BUNDLE/Contents/MacOS" "$BUNDLE/Contents/Resources"

cp "$BINARY" "$BUNDLE/Contents/MacOS/AViewerMac"
cp Resources/HelpMenuLinks.json "$BUNDLE/Contents/Resources/"
cp -R Resources/*.lproj "$BUNDLE/Contents/Resources/" 2>/dev/null || true

sed -e "s/__SHORT_VERSION__/${SHORT_VERSION}/" \
    -e "s/__BUILD_VERSION__/${BUILD_VERSION}/" \
    Resources/Info.plist > "$BUNDLE/Contents/Info.plist"

echo "==> Signing"

# A self-signed local certificate is enough to keep the Accessibility
# permission across rebuilds, so pick one up automatically when it exists.
# See "Why the Accessibility permission keeps being revoked" in README.md.
LOCAL_IDENTITY="aViewer Local Signing"
if [ -z "${SIGN_IDENTITY:-}" ] && \
   security find-identity -v -p codesigning 2>/dev/null | grep -qF "$LOCAL_IDENTITY"; then
    SIGN_IDENTITY="$LOCAL_IDENTITY"
    echo "    found local signing identity: $LOCAL_IDENTITY"
fi

if [ "${SIGN_IDENTITY:-}" = "$LOCAL_IDENTITY" ]; then
    # A local certificate cannot be notarised, so the hardened runtime and a
    # timestamp would only get in the way.
    codesign --force --sign "$SIGN_IDENTITY" "$BUNDLE"
elif [ -n "${SIGN_IDENTITY:-}" ]; then
    codesign --force --options runtime --timestamp \
        --sign "$SIGN_IDENTITY" "$BUNDLE"
else
    # Ad-hoc. Deliberately without --options runtime: a hardened runtime with
    # no real identity buys nothing and complicates local testing.
    codesign --force --sign - "$BUNDLE"
fi

echo "==> Built $BUNDLE"
codesign --display --verbose=2 "$BUNDLE" 2>&1 | sed -n '1,6p'

# The designated requirement is what macOS records when you grant the
# Accessibility permission. If it is a bare cdhash the grant dies on the next
# rebuild, so say so rather than letting it be discovered the hard way.
REQUIREMENT="$(codesign -d --requirements - "$BUNDLE" 2>&1 | grep '^# designated' || true)"
if [ -n "$REQUIREMENT" ]; then
    echo "$REQUIREMENT"
fi
case "$REQUIREMENT" in
    *cdhash*)
        echo ""
        echo "    NOTE: this build is identified by its content hash, so granting"
        echo "    Accessibility access to it will stop working the next time you"
        echo "    rebuild. Create a self-signed certificate named"
        echo "    '$LOCAL_IDENTITY' to grant it once and be done."
        echo "    See README.md > Why the Accessibility permission keeps being revoked."
        ;;
esac

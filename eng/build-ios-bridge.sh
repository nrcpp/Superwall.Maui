#!/usr/bin/env bash
# Builds SuperwallMauiBridge.xcframework outside of .NET (for debugging the Swift wrapper).
# Uses the same xcodebuild flags as the .NET XcodeProject build item.
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
PROJECT="$ROOT/native/ios/SuperwallMauiBridge.xcodeproj"
OUT="$ROOT/artifacts/native/ios"
SCHEME=SuperwallMauiBridge
FLAGS=(
  BUILD_LIBRARY_FOR_DISTRIBUTION=YES
  ENABLE_BITCODE=NO
  OTHER_LDFLAGS=-ObjC
  OTHER_SWIFT_FLAGS=-no-verify-emitted-module-interface
  SKIP_INSTALL=NO
  SWIFT_INSTALL_OBJC_HEADER=YES
)

rm -rf "$OUT"
mkdir -p "$OUT"

for DEST in "generic/platform=iOS" "generic/platform=iOS Simulator"; do
  NAME=$([[ "$DEST" == *Simulator ]] && echo simulator || echo device)
  xcodebuild archive \
    -project "$PROJECT" -scheme "$SCHEME" -configuration Release \
    -destination "$DEST" \
    -archivePath "$OUT/$NAME.xcarchive" \
    -derivedDataPath "$OUT/DerivedData" \
    "${FLAGS[@]}"
done

xcodebuild -create-xcframework \
  -framework "$OUT/device.xcarchive/Products/Library/Frameworks/$SCHEME.framework" \
  -framework "$OUT/simulator.xcarchive/Products/Library/Frameworks/$SCHEME.framework" \
  -output "$OUT/$SCHEME.xcframework"

echo "Created $OUT/$SCHEME.xcframework"

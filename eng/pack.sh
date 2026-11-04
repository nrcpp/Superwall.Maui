#!/usr/bin/env bash
# Packs Superwall.Maui into artifacts/packages. macOS only: the iOS bridge is built by Xcode.
#   eng/pack.sh           release package; must run in a clone of the public repo so SourceLink
#                         and the nuspec repository commit point at public sources
#   eng/pack.sh --local   local test package from any clone
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
PUBLIC_REPO="github.com/nrcpp/Superwall.Maui"

EXTRA=()
if [[ "${1:-}" == "--local" ]]; then
  EXTRA+=(-p:EnableSourceLink=false)
else
  ORIGIN="$(git -C "$ROOT" remote get-url origin 2>/dev/null || true)"
  if [[ "$ORIGIN" != *"$PUBLIC_REPO" && "$ORIGIN" != *"$PUBLIC_REPO.git" ]]; then
    echo "error: origin is '$ORIGIN'; release packages must be built from a clone of https://$PUBLIC_REPO" >&2
    echo "       (use --local for a test package)" >&2
    exit 1
  fi
  if [[ -n "$(git -C "$ROOT" status --porcelain)" ]]; then
    echo "error: working tree is not clean" >&2
    exit 1
  fi
  EXTRA+=(-p:ContinuousIntegrationBuild=true)
fi

export JAVA_HOME="${JAVA_HOME:-$(/usr/libexec/java_home -v 17)}"
export ANDROID_HOME="${ANDROID_HOME:-$HOME/Library/Android/sdk}"

dotnet test "$ROOT/tests/Superwall.Maui.Tests" -c Release
dotnet pack "$ROOT/src/Superwall.Maui" -c Release -o "$ROOT/artifacts/packages" "${EXTRA[@]}"
ls -lh "$ROOT"/artifacts/packages/Superwall.Maui.*

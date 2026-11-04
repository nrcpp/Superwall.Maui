#!/usr/bin/env bash
# Creates a fresh MAUI app that consumes Superwall.Maui from artifacts/packages and builds it for
# Android and the iOS Simulator. The build log must not contain xcodebuild/gradle invocations.
#   eng/smoke-test-package.sh [version] [dir]
# Keys for a runtime check (optional): SWM_IOS_KEY, SWM_ANDROID_KEY; SWM_APP_ID overrides the bundle id / package name.
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
FEED="$ROOT/artifacts/packages"
VERSION="${1:-$(ls "$FEED"/Superwall.Maui.*.nupkg | grep -v snupkg | sed -E 's/.*Superwall\.Maui\.(.*)\.nupkg/\1/' | sort -V | tail -1)}"
DIR="${2:-/tmp/swm-smoke-$VERSION}"

export JAVA_HOME="${JAVA_HOME:-$(/usr/libexec/java_home -v 17)}"
export ANDROID_HOME="${ANDROID_HOME:-$HOME/Library/Android/sdk}"

rm -rf "$DIR"
dotnet new maui -n SuperwallSmoke -o "$DIR" --no-restore >/dev/null
cd "$DIR"

cat > nuget.config <<EOF
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <clear />
    <add key="local" value="$FEED" />
    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" />
  </packageSources>
</configuration>
EOF

# Only the platforms the package supports; Android minSdk 23 is required (template default 21 fails manifest merge).
sed -i '' -E 's#<TargetFrameworks>.*</TargetFrameworks>#<TargetFrameworks>net10.0-android;net10.0-ios</TargetFrameworks>#' SuperwallSmoke.csproj
sed -i '' -E '/<TargetFrameworks Condition=/d' SuperwallSmoke.csproj
sed -i '' -E "s#(== 'android'\">)21\.0<#\123.0<#" SuperwallSmoke.csproj
if [[ -n "${SWM_APP_ID:-}" ]]; then
  sed -i '' -E "s#<ApplicationId>.*</ApplicationId>#<ApplicationId>$SWM_APP_ID</ApplicationId>#" SuperwallSmoke.csproj
fi
dotnet add package Superwall.Maui --version "$VERSION" >/dev/null

# Minimal integration: UseSuperwall + a button that registers a placement.
sed -i '' -E 's#\.UseMauiApp<App>\(\)#.UseMauiApp<App>()\
			.UseSuperwall(o =>\
			{\
				o.AppleApiKey = Environment.GetEnvironmentVariable("SWM_IOS_KEY") ?? "'"${SWM_IOS_KEY:-pk_smoke}"'";\
				o.AndroidApiKey = Environment.GetEnvironmentVariable("SWM_ANDROID_KEY") ?? "'"${SWM_ANDROID_KEY:-pk_smoke}"'";\
			})#' MauiProgram.cs
sed -i '' -E '1s#^#using Superwall.Maui;\
#' MauiProgram.cs
cat > MainPage.xaml.cs <<'EOF'
using Superwall.Maui;

namespace SuperwallSmoke;

public partial class MainPage : ContentPage
{
	public MainPage()
	{
		InitializeComponent();
	}

	private async void OnCounterClicked(object? sender, EventArgs e)
	{
		var superwall = IPlatformApplication.Current!.Services.GetRequiredService<ISuperwall>();
		await superwall.RegisterPlacementAsync("campaign_trigger", handler: new PaywallPresentationHandler
		{
			OnPresent = info => CounterBtn.Text = $"Presented {info.Identifier}",
			OnSkip = reason => CounterBtn.Text = $"Skipped: {reason}",
			OnError = error => CounterBtn.Text = $"Error: {error}",
		});
	}
}
EOF

for TFM in net10.0-android net10.0-ios; do
  ARGS=(-f "$TFM")
  [[ "$TFM" == net10.0-ios ]] && ARGS+=(-p:RuntimeIdentifier=iossimulator-arm64)
  LOG="$DIR/build-$TFM.log"
  dotnet build "${ARGS[@]}" -v:n > "$LOG" 2>&1 || { tail -30 "$LOG"; exit 1; }
  if grep -Eq "xcodebuild (archive|build)|gradlew" "$LOG"; then
    echo "error: $TFM build invoked native project builds" >&2
    exit 1
  fi
  echo "$TFM: OK ($(grep -c 'warning' "$LOG" || true) warning lines)"
done
echo "Smoke project: $DIR"

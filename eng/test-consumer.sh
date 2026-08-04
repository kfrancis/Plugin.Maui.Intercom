#!/usr/bin/env bash
# Clean-room package-consumption test.
#
# Creates a minimal .NET MAUI iOS app in a temporary directory that references
# ONLY the locally packed Plugin.Maui.Intercom NuGet (by exact version) through
# a temporary NuGet.config. No ProjectReference, no linked sources, no
# repository-relative native paths. Proves that:
#   - the iOS binding package resolves transitively,
#   - the app compiles for the iOS simulator,
#   - the app builds (unsigned) for ios-arm64 devices,
#   - the native Intercom framework + resources land in the .app.
#
# Usage: eng/test-consumer.sh --version <pkg-version> --feed <dir-with-nupkgs> [--clear-cache]
set -euo pipefail

VERSION=""
FEED=""
CLEAR_CACHE="false"
while [[ $# -gt 0 ]]; do
  case "$1" in
    --version) VERSION="$2"; shift 2 ;;
    --feed)    FEED="$(cd "$2" && pwd)"; shift 2 ;;
    --clear-cache) CLEAR_CACHE="true"; shift ;;
    *) echo "Unknown argument: $1" >&2; exit 2 ;;
  esac
done
[[ -n "$VERSION" && -n "$FEED" ]] || { echo "Usage: eng/test-consumer.sh --version <v> --feed <dir> [--clear-cache]" >&2; exit 2; }

if [[ "$(uname -s)" != "Darwin" ]]; then
  echo "ERROR: the consumer test builds iOS apps and requires macOS." >&2
  exit 1
fi

echo "── Diagnostics ─────────────────────────────────────────"
dotnet --info | head -20
xcodebuild -version
swift --version 2>/dev/null | head -1
echo "Feed: $FEED"
ls -1 "$FEED"
echo "────────────────────────────────────────────────────────"

# Short path to keep intermediate paths well under limits.
APP_ROOT="/tmp/icom-test"
APP_DIR="$APP_ROOT/App"
rm -rf "$APP_ROOT"
mkdir -p "$APP_DIR/Platforms/iOS"

if [[ "$CLEAR_CACHE" == "true" ]]; then
  dotnet nuget locals all --clear
fi

# ── Temporary NuGet.config: local feed first, nuget.org for everything else ──
cat > "$APP_ROOT/NuGet.config" <<EOF
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <clear />
    <add key="local" value="$FEED" />
    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" />
  </packageSources>
</configuration>
EOF

# ── Minimal MAUI iOS app (no template dependency, fully deterministic) ──────
cat > "$APP_DIR/App.csproj" <<EOF
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0-ios</TargetFramework>
    <OutputType>Exe</OutputType>
    <UseMaui>true</UseMaui>
    <SingleProject>true</SingleProject>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <ApplicationId>com.plugintest.consumer</ApplicationId>
    <ApplicationDisplayVersion>1.0</ApplicationDisplayVersion>
    <ApplicationVersion>1</ApplicationVersion>
    <SupportedOSPlatformVersion>15.0</SupportedOSPlatformVersion>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.Maui.Controls" Version="\$(MauiVersion)" />
    <PackageReference Include="Plugin.Maui.Intercom" Version="$VERSION" />
  </ItemGroup>
</Project>
EOF

cat > "$APP_DIR/MauiProgram.cs" <<'EOF'
using Plugin.Maui.Intercom;

namespace ConsumerApp;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder.UseMauiApp<App>().UseIntercom();
        return builder.Build();
    }
}

public class App : Application
{
    protected override Window CreateWindow(IActivationState? activationState)
        => new(new MainPage());
}

public class MainPage : ContentPage
{
    public MainPage()
    {
        var status = new Label { Text = "Plugin.Maui.Intercom consumer test" };
        var button = new Button { Text = "Exercise API" };
        button.Clicked += (_, _) =>
        {
            // Exercises the full public surface so the managed binding and the
            // native frameworks must resolve and link. Every IIntercom member belongs
            // here — a member nothing references is a member whose native symbol the
            // linker never has to find. Async calls are discarded rather than awaited:
            // this is a link check, and nothing is initialized against a real workspace.
            var intercom = Intercom.Default;

            intercom.EnableLogging(IntercomLogLevel.Verbose);
            intercom.Initialize("placeholder_api_key", "placeholder_app_id");
            intercom.ChangeWorkspace("placeholder_api_key", "placeholder_app_id");

            intercom.SetUserHash("placeholder");
            intercom.SetUserJwt("placeholder");
            _ = intercom.LoginUnidentifiedUserAsync();

            var attributes = new IntercomUserAttributes
            {
                UserId = "user-1",
                Email = "test@example.com",
                Name = "Consumer Test",
                Phone = "+353 1 234 5678",
                LanguageOverride = "en",
                SignedUpAt = DateTimeOffset.UtcNow,
                UnsubscribedFromEmails = false,
            };
            attributes.CustomAttributes["items_in_cart"] = 8;
            attributes.Companies.Add(new IntercomCompany
            {
                CompanyId = "company-1",
                Name = "Consumer Co",
                Plan = "Pro",
                MonthlySpend = 99,
                CreatedAt = DateTimeOffset.UtcNow,
            });

            _ = intercom.LoginUserAsync(attributes);
            _ = intercom.UpdateUserAsync(attributes);
            _ = intercom.SetAuthTokensAsync(new Dictionary<string, string> { ["token"] = "value" });
            _ = intercom.IsUserLoggedIn;
            _ = intercom.FetchLoggedInUserAttributes();

            intercom.LogEvent("consumer_test");
            intercom.LogEvent("consumer_test", new Dictionary<string, object?> { ["count"] = 1 });

            intercom.Present();
            intercom.Present(IntercomSpace.Messages);
            intercom.Present(IntercomSpace.HelpCenter);
            intercom.Present(IntercomSpace.Tickets);
            intercom.PresentMessageComposer();
            intercom.PresentMessageComposer("hello");
            intercom.PresentContent(new IntercomContent.Article("article-1"));
            intercom.PresentContent(new IntercomContent.Carousel("carousel-1"));
            intercom.PresentContent(new IntercomContent.Survey("survey-1"));
            intercom.PresentContent(new IntercomContent.Conversation("conversation-1"));
            intercom.PresentContent(new IntercomContent.Ticket("ticket-1"));
            intercom.PresentContent(new IntercomContent.HelpCenterCollections(["collection-1"]));
            intercom.HideIntercom();

            intercom.SetLauncherVisible(true);
            intercom.SetInAppMessagesVisible(true);
            intercom.SetBottomPaddingDp(10);
            intercom.SetThemeMode(IntercomThemeMode.Dark);

            _ = intercom.UnreadConversationCount;
            EventHandler<int> onUnreadCountChanged = (_, _) => { };
            intercom.UnreadConversationCountChanged += onUnreadCountChanged;
            intercom.UnreadConversationCountChanged -= onUnreadCountChanged;

            _ = intercom.FetchHelpCenterCollectionsAsync();
            _ = intercom.FetchHelpCenterCollectionAsync("collection-1");
            _ = intercom.SearchHelpCenterAsync("refund");

            _ = intercom.SendPushTokenToIntercomAsync("00ff");
            var payload = new Dictionary<string, string> { ["message"] = "test" };
            if (intercom.IsIntercomPush(payload))
            {
                intercom.HandlePush(payload);
            }

            intercom.Logout();
            status.Text = "API exercised";
        };
        Content = new VerticalStackLayout { Children = { status, button } };
    }
}
EOF

cat > "$APP_DIR/Platforms/iOS/Program.cs" <<'EOF'
using Foundation;
using ObjCRuntime;
using UIKit;

namespace ConsumerApp;

public class Program
{
    private static void Main(string[] args)
        => UIApplication.Main(args, null, typeof(AppDelegate));
}

[Register("AppDelegate")]
public class AppDelegate : MauiUIApplicationDelegate
{
    protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();
}
EOF

cat > "$APP_DIR/Platforms/iOS/Info.plist" <<'EOF'
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
    <key>LSRequiresIPhoneOS</key>
    <true/>
    <key>UIDeviceFamily</key>
    <array>
        <integer>1</integer>
        <integer>2</integer>
    </array>
    <key>UIRequiredDeviceCapabilities</key>
    <array>
        <string>arm64</string>
    </array>
    <key>UISupportedInterfaceOrientations</key>
    <array>
        <string>UIInterfaceOrientationPortrait</string>
    </array>
    <key>XSAppIconAssets</key>
    <string>Assets.xcassets/appicon.appiconset</string>
</dict>
</plist>
EOF

cd "$APP_DIR"

# ── Restore strictly from the temp config ───────────────────────────────────
dotnet restore App.csproj --configfile "$APP_ROOT/NuGet.config"

echo ""
echo "── Verifying transitive iOS binding resolution ─────────"
ASSETS="obj/project.assets.json"
python3 - "$ASSETS" "$VERSION" <<'PYEOF'
import json, sys
assets = json.load(open(sys.argv[1]))
version = sys.argv[2]
libs = assets.get("libraries", {})
expected = {
    f"Plugin.Maui.Intercom/{version}",
    f"Plugin.Maui.Intercom.iOS.Binding/{version}",
}
missing = [e for e in expected if e not in libs]
if missing:
    print("MISSING from resolved graph:", missing)
    print("Resolved Plugin/Intercom packages:", [k for k in libs if "Intercom" in k])
    sys.exit(1)
direct = [d for d in assets["project"]["frameworks"].values()
          for d in d.get("dependencies", {})]
print("Direct dependencies:", sorted(set(direct)))
assert "Plugin.Maui.Intercom.iOS.Binding" not in direct, \
    "Binding must be transitive, not a direct reference of the test app"
print("OK: Plugin.Maui.Intercom", version, "resolved; iOS binding resolved TRANSITIVELY.")
PYEOF

echo ""
echo "── Simulator build (iossimulator-arm64) ────────────────"
# Implicit restore picks up $APP_ROOT/NuGet.config via directory hierarchy and
# restores for exactly this RuntimeIdentifier.
dotnet build App.csproj -c Release -r iossimulator-arm64

echo ""
echo "── Device build (ios-arm64, unsigned) ──────────────────"
# Clean between RID builds: the iOS toolchain caches target-platform state under
# obj/ and otherwise tries to link the device build against simulator settings.
rm -rf obj bin
dotnet build App.csproj -c Release -r ios-arm64 -p:EnableCodeSigning=false -bl:"$APP_ROOT/device.binlog"

echo ""
echo "── Inspecting device .app ──────────────────────────────"
APP_BUNDLE="$(find bin/Release/net10.0-ios/ios-arm64 -maxdepth 1 -name '*.app' -type d | head -1)"
[[ -n "$APP_BUNDLE" ]] || { echo "ERROR: device .app not found"; exit 1; }
echo "App bundle: $APP_BUNDLE"
echo ""
echo "Frameworks:"
ls -1 "$APP_BUNDLE/Frameworks" || { echo "ERROR: no Frameworks directory"; exit 1; }

fail=0
if [[ ! -d "$APP_BUNDLE/Frameworks/Intercom.framework" ]]; then
  echo "ERROR: Intercom.framework not embedded in the app bundle."; fail=1
fi

INTERCOM_BIN="$APP_BUNDLE/Frameworks/Intercom.framework/Intercom"
if [[ -f "$INTERCOM_BIN" ]]; then
  echo ""; echo "Intercom binary:"
  file "$INTERCOM_BIN"
  lipo -info "$INTERCOM_BIN"
  if lipo -info "$INTERCOM_BIN" | grep -q x86_64; then
    echo "ERROR: simulator (x86_64) slice present in device build."; fail=1
  fi
fi

if [[ ! -f "$APP_BUNDLE/Frameworks/Intercom.framework/PrivacyInfo.xcprivacy" ]]; then
  echo "ERROR: PrivacyInfo.xcprivacy missing from embedded Intercom.framework."; fail=1
fi

for bundle in Intercom.bundle IntercomTranslations.bundle InterBlocksAssets.bundle; do
  if [[ ! -e "$APP_BUNDLE/Frameworks/Intercom.framework/$bundle" ]]; then
    echo "ERROR: $bundle missing from embedded Intercom.framework."; fail=1
  fi
done

APP_BIN="$APP_BUNDLE/$(basename "$APP_BUNDLE" .app)"
if [[ -f "$APP_BIN" ]]; then
  echo ""; echo "App binary linkage (otool -L, Intercom/Swift excerpts):"
  otool -L "$APP_BIN" | grep -iE 'intercom|swift' || true
fi

if [[ $fail -ne 0 ]]; then
  echo ""; echo "Consumer test FAILED — see errors above."
  exit 1
fi

echo ""
echo "Consumer test PASSED: package restored from local feed, binding resolved"
echo "transitively, simulator + device builds succeeded, native framework and"
echo "resources embedded."

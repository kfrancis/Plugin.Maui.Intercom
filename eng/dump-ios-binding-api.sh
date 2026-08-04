#!/usr/bin/env bash
# Print the public API that SwiftBindings.Sdk actually generated for the iOS binding.
#
# The generated C# is not committed (see CLAUDE.md) and hand-written supplements are not
# allowed, so what the generator produced is the whole iOS surface available to
# src/Plugin.Maui.Intercom/Intercom.macios.cs. Anything absent here has to be reported
# upstream and marked "todo" in eng/api-coverage.json rather than worked around locally.
#
# Run eng/generate-ios-binding.sh (macOS + Xcode 26+) first; this reads its output.
#
# Usage:
#   eng/dump-ios-binding-api.sh                 # every generated public member
#   eng/dump-ios-binding-api.sh Intercom        # members of one generated type
#   eng/dump-ios-binding-api.sh --check         # verify the members Intercom.macios.cs needs
set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
OBJ="$REPO_ROOT/src/macios/Intercom.iOS.Binding/obj"

if [[ ! -d "$OBJ" ]]; then
  echo "ERROR: $OBJ does not exist. Run eng/generate-ios-binding.sh first (macOS only)." >&2
  exit 1
fi

mapfile -t GENERATED < <(find "$OBJ" -name '*.cs' -not -name '*.AssemblyInfo.cs' -not -name '*.GlobalUsings.g.cs' | sort)
if [[ ${#GENERATED[@]} -eq 0 ]]; then
  echo "ERROR: no generated sources under $OBJ. Did the binding build succeed?" >&2
  exit 1
fi

# Members Intercom.macios.cs calls. Kept here rather than in a comment so an Intercom
# upgrade that drops one fails this script instead of failing the C# compile with a
# less obvious message.
REQUIRED=(
  "SetApiKey" "SetUserHash" "SetUserJwt" "SetAuthTokens"
  "LoginUnidentifiedUserWithSuccess" "LoginUserWithUserAttributes" "Logout" "UpdateUser"
  "IsUserLoggedIn" "FetchLoggedInUserAttributes"
  "LogEventWithName"
  "PresentIntercom" "PresentContent" "PresentMessageComposer" "HideIntercom"
  "FetchHelpCenterCollectionsWithCompletion" "FetchHelpCenterCollection" "SearchHelpCenter"
  "SetDeviceToken" "IsIntercomPushNotification" "HandleIntercomPushNotification"
  "SetBottomPadding" "SetInAppMessagesVisible" "SetLauncherVisible"
  "UnreadConversationCount" "EnableLogging"
  "ArticleWithId" "CarouselWithId" "SurveyWithId" "ConversationWithId" "HelpCenterCollectionsWithIds"
)

if [[ "${1:-}" == "--check" ]]; then
  missing=()
  for member in "${REQUIRED[@]}"; do
    grep -qE "\b$member\b" "${GENERATED[@]}" || missing+=("$member")
  done
  if [[ ${#missing[@]} -gt 0 ]]; then
    echo "MISSING from the generated binding (${#missing[@]}):"
    printf '  - %s\n' "${missing[@]}"
    echo ""
    echo "These are generator gaps, not things to patch locally. File them upstream at"
    echo "https://github.com/justinwojo/swift-dotnet-bindings and mark the matching symbols"
    echo "\"todo\" in eng/api-coverage.json with the issue link."
    exit 1
  fi
  echo "All ${#REQUIRED[@]} required members are present in the generated binding."
  exit 0
fi

FILTER="${1:-}"
grep -hnE '^\s*(public|internal)\s+.*(\(|\{ get|\{ set|;)' "${GENERATED[@]}" \
  | sed 's/^[0-9]*://' \
  | sed 's/^\s*//' \
  | { if [[ -n "$FILTER" ]]; then grep -i "$FILTER"; else cat; fi; } \
  | sort -u

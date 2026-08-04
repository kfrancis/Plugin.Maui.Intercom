package com.intercom.mauiintercom;

import android.app.Application;

import androidx.annotation.NonNull;

import org.json.JSONArray;
import org.json.JSONException;
import org.json.JSONObject;

import java.util.ArrayList;
import java.util.List;
import java.util.Map;

import io.intercom.android.sdk.Company;
import io.intercom.android.sdk.AuthToken;
import io.intercom.android.sdk.Intercom;
import io.intercom.android.sdk.IntercomContent;
import io.intercom.android.sdk.IntercomError;
import io.intercom.android.sdk.IntercomSpace;
import io.intercom.android.sdk.IntercomStatusCallback;
import io.intercom.android.sdk.UnreadConversationCountListener;
import io.intercom.android.sdk.UserAttributes;
import io.intercom.android.sdk.helpcenter.api.CollectionContentRequestCallback;
import io.intercom.android.sdk.helpcenter.api.CollectionRequestCallback;
import io.intercom.android.sdk.helpcenter.api.HelpCenterArticleSearchResult;
import io.intercom.android.sdk.helpcenter.api.SearchRequestCallback;
import io.intercom.android.sdk.helpcenter.collections.HelpCenterCollection;
import io.intercom.android.sdk.helpcenter.sections.Author;
import io.intercom.android.sdk.helpcenter.sections.HelpCenterArticle;
import io.intercom.android.sdk.helpcenter.sections.HelpCenterCollectionContent;
import io.intercom.android.sdk.helpcenter.sections.HelpCenterSection;
import io.intercom.android.sdk.identity.Registration;
import io.intercom.android.sdk.push.IntercomPushClient;
import io.intercom.android.sdk.ui.theme.ThemeMode;

/**
 * The complete Android surface of Plugin.Maui.Intercom.
 *
 * <p>This class is the hard ceiling of what an Android consumer can reach: the binding
 * project only binds {@code com.intercom.mauiintercom}, so anything absent here is
 * unreachable from C#. (iOS is the opposite — the generated binding exposes the whole
 * Intercom ObjC surface.) Adding a member to IIntercom therefore always means adding one
 * here first.</p>
 *
 * <p>Failures are never swallowed. Operations with a callback report through it; everything
 * else lets the exception propagate into C#, where it becomes an IntercomException. An
 * earlier version caught everything and logged it, which turned misconfiguration into a
 * silent no-op.</p>
 *
 * <p>Enums cross the binding as ordinals and data crosses as java.util.Map (requests) or
 * JSON (responses), which keeps Kotlin types out of the bound API.</p>
 */
public final class IntercomSdk {

    // Mirrors Plugin.Maui.Intercom.IntercomSpace.
    private static final IntercomSpace[] SPACES = {
            IntercomSpace.Home,
            IntercomSpace.Messages,
            IntercomSpace.HelpCenter,
            IntercomSpace.Tickets
    };

    // Mirrors Plugin.Maui.Intercom.IntercomThemeMode.
    private static final ThemeMode[] THEME_MODES = {
            ThemeMode.SYSTEM,
            ThemeMode.LIGHT,
            ThemeMode.DARK
    };

    // Mirrors the content type ordinals in Plugin.Maui.Intercom.IntercomContent.
    private static final int CONTENT_ARTICLE = 0;
    private static final int CONTENT_CAROUSEL = 1;
    private static final int CONTENT_SURVEY = 2;
    private static final int CONTENT_CONVERSATION = 3;
    private static final int CONTENT_TICKET = 4;
    private static final int CONTENT_HELP_CENTER_COLLECTIONS = 5;

    private static final IntercomPushClient PUSH_CLIENT = new IntercomPushClient();

    private IntercomSdk() {
    }

    // ── Lifecycle ───────────────────────────────────────────────────────────

    /**
     * Initialize the Intercom SDK.
     * @param application {Application} The application instance
     * @param apiKey {String} The Intercom API key
     * @param appId {String} The Intercom app ID
     */
    public static void initialize(Application application, String apiKey, String appId) {
        Intercom.initialize(application, apiKey, appId);
    }

    /**
     * Point Intercom at a different workspace.
     * @param apiKey {String} The API key of the new workspace
     * @param appId {String} The app ID of the new workspace
     */
    public static void changeWorkspace(String apiKey, String appId) {
        Intercom.client().changeWorkspace(apiKey, appId);
    }

    /**
     * Set the SDK log level (logcat tag "intercom"). Call before initialize.
     * @param level {int} An Intercom.LogLevel constant
     */
    public static void setLogLevel(int level) {
        Intercom.setLogLevel(level);
    }

    /**
     * Translate a Plugin.Maui.Intercom.IntercomLogLevel ordinal to an Intercom.LogLevel
     * constant. The two do not line up: Intercom's are an @IntDef starting at VERBOSE = 2.
     * @param ordinal {int} The IntercomLogLevel ordinal
     * @return {int} The matching Intercom.LogLevel constant
     */
    public static int toNativeLogLevel(int ordinal) {
        switch (ordinal) {
            case 0: return Intercom.LogLevel.DISABLED;
            case 1: return Intercom.LogLevel.ERROR;
            case 2: return Intercom.LogLevel.WARN;
            case 3: return Intercom.LogLevel.INFO;
            case 4: return Intercom.LogLevel.DEBUG;
            case 6: return Intercom.LogLevel.ASSERT;
            default: return Intercom.LogLevel.VERBOSE;
        }
    }

    // ── Identity ────────────────────────────────────────────────────────────

    /**
     * Log in a user with no identifiable information.
     * @param callback {IIntercomCallback} Receives the result
     */
    public static void loginUnidentifiedUser(IIntercomCallback callback) {
        Intercom.client().loginUnidentifiedUser(statusCallback(callback));
    }

    /**
     * Log in an identified user.
     * @param attributes {Map} Attribute map; must carry "userId" and/or "email"
     * @param callback {IIntercomCallback} Receives the result
     */
    public static void loginIdentifiedUser(Map<String, Object> attributes, IIntercomCallback callback) {
        Registration registration = Registration.create();
        Object userId = attributes.get("userId");
        Object email = attributes.get("email");
        if (userId instanceof String) {
            registration = registration.withUserId((String) userId);
        }
        if (email instanceof String) {
            registration = registration.withEmail((String) email);
        }
        // Any non-identifying attributes travel with the login rather than needing a
        // follow-up updateUser call.
        UserAttributes userAttributes = buildUserAttributes(attributes);
        if (!userAttributes.isEmpty()) {
            registration = registration.withUserAttributes(userAttributes);
        }
        Intercom.client().loginIdentifiedUser(registration, statusCallback(callback));
    }

    /**
     * Update the logged-in user.
     * @param attributes {Map} Attribute map
     * @param callback {IIntercomCallback} Receives the result
     */
    public static void updateUser(Map<String, Object> attributes, IIntercomCallback callback) {
        Intercom.client().updateUser(buildUserAttributes(attributes), statusCallback(callback));
    }

    /**
     * Provide auth tokens for features such as Fin Actions.
     * @param tokens {Map} Token names mapped to values
     * @param callback {IIntercomCallback} Receives the result
     */
    public static void setAuthTokens(Map<String, String> tokens, IIntercomCallback callback) {
        List<AuthToken> authTokens = new ArrayList<>();
        for (Map.Entry<String, String> entry : tokens.entrySet()) {
            authTokens.add(new AuthToken(entry.getKey(), entry.getValue()));
        }
        Intercom.client().setAuthTokens(authTokens, statusCallback(callback));
    }

    /**
     * Set the identity verification hash. Call before logging a user in.
     * @param userHash {String} HMAC digest of the user ID or email
     */
    public static void setUserHash(String userHash) {
        Intercom.client().setUserHash(userHash);
    }

    /**
     * Set the Messenger Security JWT. Call before logging a user in.
     * @param jwt {String} A JWT signed with the app secret
     */
    public static void setUserJwt(String jwt) {
        Intercom.client().setUserJwt(jwt);
    }

    /**
     * Log the current user out.
     */
    public static void logout() {
        Intercom.client().logout();
    }

    /**
     * Whether a user is currently logged in.
     * @return {boolean} true when a user is logged in
     */
    public static boolean isUserLoggedIn() {
        return Intercom.client().isUserLoggedIn();
    }

    /**
     * Read back the logged-in user's identifiers.
     * @return {String} JSON with "userId" and "email", or null when no user is logged in
     */
    public static String fetchLoggedInUserAttributes() {
        Registration registration = Intercom.client().fetchLoggedInUserAttributes();
        if (registration == null) {
            return null;
        }
        try {
            JSONObject json = new JSONObject();
            putOrNull(json, "userId", registration.getUserId());
            putOrNull(json, "email", registration.getEmail());
            return json.toString();
        } catch (JSONException e) {
            throw new IllegalStateException("Serialising the logged-in user failed", e);
        }
    }

    // ── Events ──────────────────────────────────────────────────────────────

    /**
     * Log an event.
     * @param name {String} The event name
     * @param metadata {Map} Optional metadata; may be null
     */
    public static void logEvent(String name, Map<String, Object> metadata) {
        if (metadata == null || metadata.isEmpty()) {
            Intercom.client().logEvent(name);
        } else {
            Intercom.client().logEvent(name, metadata);
        }
    }

    // ── Presentation ────────────────────────────────────────────────────────

    /**
     * Open the Messenger at a space.
     * @param spaceOrdinal {int} A Plugin.Maui.Intercom.IntercomSpace ordinal
     */
    public static void present(int spaceOrdinal) {
        Intercom.client().present(SPACES[spaceOrdinal]);
    }

    /**
     * Present a specific piece of Intercom content.
     * @param typeOrdinal {int} The content type ordinal
     * @param id {String} The content ID; null for help center collections
     * @param ids {List} The collection IDs; null for every other content type
     */
    public static void presentContent(int typeOrdinal, String id, List<String> ids) {
        IntercomContent content;
        switch (typeOrdinal) {
            case CONTENT_ARTICLE:
                content = new IntercomContent.Article(id);
                break;
            case CONTENT_CAROUSEL:
                content = new IntercomContent.Carousel(id);
                break;
            case CONTENT_SURVEY:
                content = new IntercomContent.Survey(id);
                break;
            case CONTENT_CONVERSATION:
                content = new IntercomContent.Conversation(id);
                break;
            case CONTENT_TICKET:
                content = new IntercomContent.Ticket(id);
                break;
            case CONTENT_HELP_CENTER_COLLECTIONS:
                content = new IntercomContent.HelpCenterCollections(ids);
                break;
            default:
                throw new IllegalArgumentException("Unknown Intercom content type: " + typeOrdinal);
        }
        Intercom.client().presentContent(content);
    }

    /**
     * Open the message composer.
     * @param initialMessage {String} Text to pre-populate; may be null
     */
    public static void presentMessageComposer(String initialMessage) {
        if (initialMessage == null) {
            Intercom.client().displayMessageComposer();
        } else {
            Intercom.client().displayMessageComposer(initialMessage);
        }
    }

    /**
     * Hide every Intercom screen currently displayed.
     */
    public static void hideIntercom() {
        Intercom.client().hideIntercom();
    }

    // ── Chrome ──────────────────────────────────────────────────────────────

    /**
     * Show or hide the launcher.
     * @param visible {boolean} Whether the launcher should be visible
     */
    public static void setLauncherVisible(boolean visible) {
        Intercom.client().setLauncherVisibility(visible ? Intercom.Visibility.VISIBLE : Intercom.Visibility.GONE);
    }

    /**
     * Show or hide in-app messages.
     * @param visible {boolean} Whether in-app messages should be visible
     */
    public static void setInAppMessagesVisible(boolean visible) {
        Intercom.client().setInAppMessageVisibility(visible ? Intercom.Visibility.VISIBLE : Intercom.Visibility.GONE);
    }

    /**
     * Set the bottom padding of the launcher and in-app messages.
     * @param bottomPaddingPx {int} The padding in pixels (the caller converts from dp)
     */
    public static void setBottomPadding(int bottomPaddingPx) {
        Intercom.client().setBottomPadding(bottomPaddingPx);
    }

    /**
     * Override the Messenger's light/dark appearance.
     * @param themeModeOrdinal {int} A Plugin.Maui.Intercom.IntercomThemeMode ordinal
     */
    public static void setThemeMode(int themeModeOrdinal) {
        Intercom.client().setThemeMode(THEME_MODES[themeModeOrdinal]);
    }

    // ── Unread conversations ────────────────────────────────────────────────

    /**
     * The number of unread conversations for the logged-in user.
     * @return {int} The unread conversation count
     */
    public static int getUnreadConversationCount() {
        return Intercom.client().getUnreadConversationCount();
    }

    /**
     * Start observing the unread conversation count.
     * @param listener {IIntercomUnreadCountListener} Receives count updates
     * @return {Object} A token to pass to removeUnreadConversationCountListener
     */
    public static Object addUnreadConversationCountListener(final IIntercomUnreadCountListener listener) {
        UnreadConversationCountListener nativeListener = new UnreadConversationCountListener() {
            @Override
            public void onCountUpdate(int count) {
                listener.onCountUpdate(count);
            }
        };
        Intercom.client().addUnreadConversationCountListener(nativeListener);
        return nativeListener;
    }

    /**
     * Stop observing the unread conversation count.
     * @param token {Object} The token returned by addUnreadConversationCountListener
     */
    public static void removeUnreadConversationCountListener(Object token) {
        if (token instanceof UnreadConversationCountListener) {
            Intercom.client().removeUnreadConversationCountListener((UnreadConversationCountListener) token);
        }
    }

    // ── Help Center data ────────────────────────────────────────────────────

    /**
     * Fetch every Help Center collection.
     * @param callback {IIntercomJsonCallback} Receives a JSON array of collections
     */
    public static void fetchHelpCenterCollections(final IIntercomJsonCallback callback) {
        Intercom.client().fetchHelpCenterCollections(new CollectionRequestCallback() {
            @Override
            public void onComplete(@NonNull List<HelpCenterCollection> collections) {
                try {
                    JSONArray array = new JSONArray();
                    for (HelpCenterCollection collection : collections) {
                        array.put(toJson(collection));
                    }
                    callback.onComplete(array.toString());
                } catch (JSONException e) {
                    callback.onFailure(-1, "Serialising Help Center collections failed: " + e.getMessage());
                }
            }

            @Override
            public void onError(int errorCode) {
                callback.onFailure(errorCode, "Fetching Help Center collections failed");
            }

            @Override
            public void onFailure() {
                callback.onFailure(-1, "Fetching Help Center collections failed");
            }
        });
    }

    /**
     * Fetch the contents of one Help Center collection.
     * @param collectionId {String} The collection ID
     * @param callback {IIntercomJsonCallback} Receives the collection content as JSON
     */
    public static void fetchHelpCenterCollection(String collectionId, final IIntercomJsonCallback callback) {
        Intercom.client().fetchHelpCenterCollection(collectionId, new CollectionContentRequestCallback() {
            @Override
            public void onComplete(@NonNull HelpCenterCollectionContent content) {
                try {
                    callback.onComplete(toJson(content).toString());
                } catch (JSONException e) {
                    callback.onFailure(-1, "Serialising the Help Center collection failed: " + e.getMessage());
                }
            }

            @Override
            public void onError(int errorCode) {
                callback.onFailure(errorCode, "Fetching the Help Center collection failed");
            }

            @Override
            public void onFailure() {
                callback.onFailure(-1, "Fetching the Help Center collection failed");
            }
        });
    }

    /**
     * Search the Help Center.
     * @param searchTerm {String} The text to search for
     * @param callback {IIntercomJsonCallback} Receives a JSON array of search results
     */
    public static void searchHelpCenter(String searchTerm, final IIntercomJsonCallback callback) {
        Intercom.client().searchHelpCenter(searchTerm, new SearchRequestCallback() {
            @Override
            public void onComplete(@NonNull List<HelpCenterArticleSearchResult> results) {
                try {
                    JSONArray array = new JSONArray();
                    for (HelpCenterArticleSearchResult result : results) {
                        JSONObject json = new JSONObject();
                        json.put("articleId", result.getArticleId());
                        json.put("title", result.getTitle());
                        putOrNull(json, "summary", result.getSummary());
                        putOrNull(json, "matchingSnippet", result.getMatchingSnippet());
                        array.put(json);
                    }
                    callback.onComplete(array.toString());
                } catch (JSONException e) {
                    callback.onFailure(-1, "Serialising Help Center search results failed: " + e.getMessage());
                }
            }

            @Override
            public void onError(int errorCode) {
                callback.onFailure(errorCode, "Searching the Help Center failed");
            }

            @Override
            public void onFailure() {
                callback.onFailure(-1, "Searching the Help Center failed");
            }
        });
    }

    // ── Push notifications ──────────────────────────────────────────────────

    /**
     * Register the device's FCM token with Intercom.
     * @param application {Application} The application instance
     * @param token {String} The FCM registration token
     */
    public static void sendPushTokenToIntercom(Application application, String token) {
        PUSH_CLIENT.sendTokenToIntercom(application, token);
    }

    /**
     * Whether a push payload came from Intercom.
     * @param payload {Map} The notification data
     * @return {boolean} true when Intercom sent it
     */
    public static boolean isIntercomPush(Map<String, String> payload) {
        return PUSH_CLIENT.isIntercomPush(payload);
    }

    /**
     * Hand an Intercom push payload to the SDK to display.
     * @param application {Application} The application instance
     * @param payload {Map} The notification data
     */
    public static void handlePush(Application application, Map<String, String> payload) {
        PUSH_CLIENT.handlePush(application, payload);
    }

    // ── Helpers ─────────────────────────────────────────────────────────────

    private static IntercomStatusCallback statusCallback(final IIntercomCallback callback) {
        return new IntercomStatusCallback() {
            @Override
            public void onSuccess() {
                if (callback != null) {
                    callback.onSuccess();
                }
            }

            @Override
            public void onFailure(@NonNull IntercomError intercomError) {
                if (callback != null) {
                    callback.onFailure(intercomError.getErrorCode(), intercomError.getErrorMessage());
                }
            }
        };
    }

    @SuppressWarnings("unchecked")
    private static UserAttributes buildUserAttributes(Map<String, Object> attributes) {
        UserAttributes.Builder builder = new UserAttributes.Builder();
        for (Map.Entry<String, Object> entry : attributes.entrySet()) {
            String key = entry.getKey();
            Object value = entry.getValue();
            if (value == null) {
                continue;
            }
            switch (key) {
                case "userId":
                    builder.withUserId((String) value);
                    break;
                case "email":
                    builder.withEmail((String) value);
                    break;
                case "name":
                    builder.withName((String) value);
                    break;
                case "phone":
                    builder.withPhone((String) value);
                    break;
                case "languageOverride":
                    builder.withLanguageOverride((String) value);
                    break;
                case "signedUpAt":
                    builder.withSignedUpAt(((Number) value).longValue());
                    break;
                case "unsubscribedFromEmails":
                    builder.withUnsubscribedFromEmails((Boolean) value);
                    break;
                case "customAttributes":
                    builder.withCustomAttributes((Map<String, ?>) value);
                    break;
                case "companies":
                    for (Object company : (List<Object>) value) {
                        builder.withCompany(buildCompany((Map<String, Object>) company));
                    }
                    break;
                default:
                    throw new IllegalArgumentException("Unknown Intercom user attribute: " + key);
            }
        }
        return builder.build();
    }

    @SuppressWarnings("unchecked")
    private static Company buildCompany(Map<String, Object> attributes) {
        Company.Builder builder = new Company.Builder();
        for (Map.Entry<String, Object> entry : attributes.entrySet()) {
            String key = entry.getKey();
            Object value = entry.getValue();
            if (value == null) {
                continue;
            }
            switch (key) {
                case "companyId":
                    builder.withCompanyId((String) value);
                    break;
                case "name":
                    builder.withName((String) value);
                    break;
                case "createdAt":
                    builder.withCreatedAt(((Number) value).longValue());
                    break;
                case "monthlySpend":
                    builder.withMonthlySpend(((Number) value).intValue());
                    break;
                case "plan":
                    builder.withPlan((String) value);
                    break;
                case "customAttributes":
                    builder.withCustomAttributes((Map<String, ?>) value);
                    break;
                default:
                    throw new IllegalArgumentException("Unknown Intercom company attribute: " + key);
            }
        }
        return builder.build();
    }

    private static JSONObject toJson(HelpCenterCollection collection) throws JSONException {
        JSONObject json = new JSONObject();
        json.put("id", collection.getId());
        json.put("title", collection.getTitle());
        putOrNull(json, "summary", collection.getSummary());
        json.put("articleCount", collection.getArticlesCount());
        json.put("collectionCount", collection.getCollectionsCount());
        return json;
    }

    private static JSONObject toJson(HelpCenterCollectionContent content) throws JSONException {
        JSONObject json = new JSONObject();
        json.put("id", content.getCollectionId());
        json.put("title", content.getTitle());
        putOrNull(json, "summary", content.getSummary());
        json.put("articleCount", content.getArticlesCount());

        JSONArray articles = new JSONArray();
        for (HelpCenterArticle article : content.getHelpCenterArticles()) {
            articles.put(toJson(article));
        }
        json.put("articles", articles);

        JSONArray sections = new JSONArray();
        for (HelpCenterSection section : content.getHelpCenterSections()) {
            JSONObject sectionJson = new JSONObject();
            sectionJson.put("title", section.getTitle());
            JSONArray sectionArticles = new JSONArray();
            for (HelpCenterArticle article : section.getHelpCenterArticles()) {
                sectionArticles.put(toJson(article));
            }
            sectionJson.put("articles", sectionArticles);
            sections.put(sectionJson);
        }
        json.put("sections", sections);

        JSONArray subCollections = new JSONArray();
        for (HelpCenterCollection subCollection : content.getSubCollections()) {
            subCollections.put(toJson(subCollection));
        }
        json.put("subCollections", subCollections);

        JSONArray authors = new JSONArray();
        for (Author author : content.getAuthors()) {
            JSONObject authorJson = new JSONObject();
            authorJson.put("authorId", author.getId());
            authorJson.put("displayName", author.getName());
            putOrNull(authorJson, "avatarUrl", author.getAvatar() == null ? null : author.getAvatar().getImageUrl());
            authors.put(authorJson);
        }
        json.put("authors", authors);

        return json;
    }

    private static JSONObject toJson(HelpCenterArticle article) throws JSONException {
        JSONObject json = new JSONObject();
        json.put("articleId", article.getArticleId());
        json.put("title", article.getTitle());
        return json;
    }

    // JSONObject.put(key, null) removes the key, which is what we want, but passing a typed
    // null is ambiguous in Java 8 — route everything through Object to keep the call legal.
    private static void putOrNull(JSONObject json, String key, String value) throws JSONException {
        json.put(key, value == null ? JSONObject.NULL : (Object) value);
    }
}

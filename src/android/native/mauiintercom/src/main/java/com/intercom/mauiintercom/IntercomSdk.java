package com.intercom.mauiintercom;

import android.app.Activity;
import android.app.Application;

import androidx.annotation.NonNull;

import java.util.Map;
import java.util.Objects;

import io.intercom.android.sdk.Intercom;
import io.intercom.android.sdk.IntercomContent;
import io.intercom.android.sdk.IntercomError;
import io.intercom.android.sdk.IntercomSpace;
import io.intercom.android.sdk.IntercomStatusCallback;
import io.intercom.android.sdk.identity.Registration;

public class IntercomSdk {
    public static void Initialize(Activity activity, String apiKey, String appId) {
        Application application = activity.getApplication();
        Intercom.initialize(application, apiKey, appId);
    }

    public static void RegisterUser(Map<String, String> userAttributes) {
        if (userAttributes == null) {
            Intercom.client().loginUnidentifiedUser(new IntercomStatusCallback() {
                @Override
                public void onSuccess() {
                    //if (callback != null)
                    //    callback.onSuccess();
                }

                @Override
                public void onFailure(@NonNull IntercomError intercomError) {
                    //if (callback != null)
                    //    callback.onFailure(intercomError.getErrorMessage());
                }
            });
        } else {
            boolean hasEmail = userAttributes.containsKey("email") && userAttributes.get("email") != null;
            boolean hasUserId = userAttributes.containsKey("userId") && userAttributes.get("userId") != null;
            Registration userRegistration = Registration.create();
            if (hasEmail) {
                userRegistration.withEmail(Objects.requireNonNull(userAttributes.get("email")));
            }
            if (hasUserId) {
                userRegistration.withUserId(Objects.requireNonNull(userAttributes.get("userId")));
            }
            Intercom.client().loginIdentifiedUser(userRegistration, new IntercomStatusCallback() {
                @Override
                public void onSuccess() {
                    //if (callback != null)
                    //    callback.onSuccess();
                }

                @Override
                public void onFailure(@NonNull IntercomError intercomError) {
                    //if (callback != null)
                    //    callback.onFailure(intercomError.getErrorMessage());
                }
            });
        }
    }

    public static void SetUserHash(String userHash) {
        if (userHash != null) {
            Intercom.client().setUserHash(userHash);
        }
    }

    public static void PresentMessenger(String message) {
        if (message != null) {
            Intercom.client().displayMessageComposer(message);
        } else {
            Intercom.client().present();
        }
    }

    public static void PresentHelpCenter() {
        Intercom.client().present(IntercomSpace.HelpCenter);
    }

    public static void PresentSupportCenter() {
        Intercom.client().present(IntercomSpace.Home);
    }

    public static void PresentMessageComposer(String message) {
        PresentMessenger(message);
    }

    public static void PresentCarousel(String carouselId) {
        Intercom.client().presentContent(new IntercomContent.Carousel(carouselId));
    }

    public static void SetVisible(Boolean isVisible) {
        Intercom.client().setLauncherVisibility(isVisible ? Intercom.Visibility.VISIBLE : Intercom.Visibility.GONE);
    }

    public static void SetBottomPadding(Integer bottomPadding) {
        Intercom.client().setBottomPadding(bottomPadding);
    }

    public static void Logout() {
        Intercom.client().logout();
    }
}

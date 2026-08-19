# Getting started with Plugin.Maui.Intercom

This guide adds Intercom Messenger to a new .NET MAUI app for Android and iOS. At the end, tapping a button signs an unidentified user in and opens the Intercom Messenger.

The plugin supports .NET MAUI 9 and 10. Android requires API 23 or later; iOS requires iOS 15 or later.

## 1. Create a .NET MAUI app

If you do not already have an app, create one from a terminal:

```bash
dotnet new maui -n IntercomSample
cd IntercomSample
```

## 2. Install the package

Add the package to the MAUI app project:

```bash
dotnet add package Plugin.Maui.Intercom
```

Do not add either binding package directly. `Plugin.Maui.Intercom` restores the Android and iOS bindings automatically.

## 3. Get your Intercom credentials

Before configuring the app, collect these values for the Intercom workspace you want the app to use.

### App ID

In your Intercom workspace, open **Settings**, select **Installation**, then select **Install for mobile**. Choose either iOS or Android. The installation page shows the App ID; use the same App ID for both platforms.

### Android API key

On the mobile installation page, choose **Android** and copy the Android API key shown with the App ID. Confirm that the Android Messenger is enabled on that page; Intercom rejects Android requests when it is disabled.

### iOS API key

On the mobile installation page, choose **iOS** and copy the iOS API key shown with the App ID. Confirm that the iOS Messenger is enabled on that page; Intercom rejects iOS requests when it is disabled.

### Android secret

Open your workspace's **Messenger Security** settings and obtain the Android-enabled secret key. Keep it only on your app server. Use it to create a user hash or, preferably, a JWT for the Android app; never ship it in the app.

### iOS secret

Open your workspace's **Messenger Security** settings and obtain the iOS-enabled secret key. Keep it only on your app server. Use it to create a user hash or, preferably, a JWT for the iOS app; never ship it in the app.

The App ID and platform API keys are required for the basic setup. The secrets are only relevant when using Messenger security. Intercom recommends JWTs; it continues to support HMAC user hashes for identity verification. Do not include either secret in a production app: anything placed in the app can be extracted. Generate the JWT or HMAC on your server instead. See Intercom's [Android installation](https://developers.intercom.com/installing-intercom/android/installation), [iOS installation](https://developers.intercom.com/installing-intercom/ios/installation), [Android Messenger security](https://developers.intercom.com/installing-intercom/android/secure-your-messenger), and [iOS Messenger security](https://developers.intercom.com/installing-intercom/ios/secure-your-messenger) documentation.

## 4. Configure the plugin at app startup

Open `MauiProgram.cs`, add the `Plugin.Maui.Intercom` namespace, and add `UseIntercom` to the builder chain. Replace the placeholder values with the credentials from the previous step.

```csharp
using Plugin.Maui.Intercom;

namespace IntercomSample;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();

        builder
            .UseMauiApp<App>()
            .UseIntercom(options =>
            {
                options.AppId = "your-app-id";
                options.AndroidApiKey = "your-android-api-key";
                options.IosApiKey = "your-ios-api-key";
            });

        return builder.Build();
    }
}
```

Keep both platform API keys in the configuration. The plugin selects the key for the platform on which the app is running and initializes the native Intercom SDK during that platform's startup lifecycle.

## 5. Add the Android permissions

In `Platforms/Android/AndroidManifest.xml`, add the network permissions as direct children of the `manifest` element:

```xml
<uses-permission android:name="android.permission.ACCESS_NETWORK_STATE" />
<uses-permission android:name="android.permission.INTERNET" />
```

No additional iOS configuration is required.

## 6. Log in and open Messenger

After a user taps a support button, log them in and show Messenger. Add this handler to `MainPage.xaml.cs`:

```csharp
using Plugin.Maui.Intercom;

namespace IntercomSample;

public partial class MainPage : ContentPage
{
    public MainPage()
    {
        InitializeComponent();
    }

    private async void OnOpenSupportClicked(object? sender, EventArgs e)
    {
        await Intercom.Default.LoginUnidentifiedUserAsync();
        Intercom.Default.Present();
    }
}
```

Add a button that calls it in `MainPage.xaml`:

```xml
<Button Text="Open support" Clicked="OnOpenSupportClicked" />
```

`LoginUnidentifiedUserAsync` is the simplest way to get started. When your app has an authenticated user, use `LoginUserAsync` with `IntercomUserAttributes` instead. See the [README](README.md#logging-users-in) for an example.

## 7. Run the app

Run the app on an Android device or emulator, or on an iOS simulator or device. Tap **Open support**. Messenger should appear and the user should be logged in anonymously.

If Messenger does not appear, first verify that the App ID and API key match the platform you are running. An Android API key cannot be used on iOS, and an iOS API key cannot be used on Android.

## Next steps

- Use [identified-user login](README.md#logging-users-in) to associate Messenger activity with an app user.
- Configure [identity verification](README.md#identity-verification) from a backend service when your Intercom workspace requires it.
- Review the [full API reference](README.md#api-usage) for Help Center, push notifications, events, and Messenger controls.

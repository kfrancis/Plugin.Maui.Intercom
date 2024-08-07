![](nuget.png)
# Plugin.Maui.Intercom

`Plugin.Maui.Intercom` provides the ability to add [Intercom](https://www.intercom.com/) to your .NET MAUI application using [Native Library Interop](https://github.com/CommunityToolkit/Maui.NativeLibraryInterop) (NLI).

## Should you use this yet?

**NO**, this is a work in progress and not ready for production use.

## What works so far?

- [x] Can build without error
- [x] Can initialize Intercom (runtime) without error
- [x] Can register a user without error 
- [ ] Can show the Intercom Messenger
- [ ] Works end-to-end on Android
- [ ] Works end-to-end on iOS

## Install Plugin

[![NuGet](https://img.shields.io/nuget/v/Plugin.Maui.Intercom.svg?label=NuGet)](https://www.nuget.org/packages/Plugin.Maui.Intercom/)

Available on [NuGet](http://www.nuget.org/packages/Plugin.Maui.Intercom).

Install with the dotnet CLI: `dotnet add package Plugin.Maui.Intercom`, or through the NuGet Package Manager in Visual Studio.

### Supported Platforms

| Platform | Minimum Version Supported |
|----------|---------------------------|
| iOS      | 11+                       |
| macOS    | 10.15+                    |
| Android  | 5.0 (API 21)              |

## API Usage

`Plugin.Maui.Intercom` provides the `Intercom` class that has a single property `Property` that you can get or set.

You can either use it as a static class, e.g.: `Intercom.Default.Property = 1` or with dependency injection: `builder.Services.AddSingleton<IIntercom>(Intercom.Default);`

### Permissions

Before you can start using Feature, you will need to request the proper permissions on each platform.

#### iOS

No permissions are needed for iOS.

#### Android

No permissions are needed for Android.

### Dependency Injection

You will first need to register the `Feature` with the `MauiAppBuilder` following the same pattern that the .NET MAUI Essentials libraries follow.

```csharp
builder.Services.AddSingleton(Intercom.Default);
```

You can then enable your classes to depend on `IFeature` as per the following example.

```csharp
public class FeatureViewModel
{
    private readonly IIntercom _intercom;

    public FeatureViewModel(IIntercom intercom)
    {
        _intercom = intercom;
    }

    public void StartFeature()
    {
        _intercom.Initialize("", "");
    }
}
```

### Straight usage

Alternatively if you want to skip using the dependency injection approach you can use the `Feature.Default` property.

```csharp
public class FeatureViewModel
{
    public void StartFeature()
    {
        Intercom.Default.Initialize("", "");
    }
}
```

### Intercom

Once you have created a `Intercom` you can interact with it in the following ways:

#### Events

##### `ReadingChanged`

Occurs when feature reading changes.

#### Properties

##### `IsSupported`

Gets a value indicating whether reading the feature is supported on this device.

##### `IsMonitoring`

Gets a value indicating whether the feature is actively being monitored.

#### Methods

##### `Start()`

Start monitoring for changes to the feature.

##### `Stop()`

Stop monitoring for changes to the feature.

# Acknowledgements

This project could not have came to be without these projects and people, thank you! <3

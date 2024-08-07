using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using Intercom;
using static System.Net.Mime.MediaTypeNames;

namespace Plugin.Maui.Intercom;

public static class DictionaryExtensions
{
    public static Java.Util.IMap ToJavaMap(this IDictionary<string, string> dictionary)
    {
        var javaMap = new Java.Util.HashMap();
        foreach (var kvp in dictionary)
        {
            javaMap.Put(new Java.Lang.String(kvp.Key), new Java.Lang.String(kvp.Value));
        }
        return javaMap;
    }
}

partial class IntercomImplementation : IIntercom
{
    //class IntercomCallback : Java.Lang.Object, IIntercomCallback
    //{
    //    private readonly Action? _onSuccess;
    //    private readonly Action<string?>? _onFailure;

    //    public IntercomCallback(Action? onSuccess, Action<string>? onFailure)
    //    {
    //        _onSuccess = onSuccess;
    //        _onFailure = onFailure;
    //    }

    //    public void OnFailure(string? error)
    //    {
    //        if (_onFailure != null)
    //            _onFailure.Invoke(error);
    //    }

    //    public void OnSuccess()
    //    {
    //        if (_onSuccess != null)
    //            _onSuccess.Invoke();
    //    }
    //}

   

    /// <summary>
    /// Initialize Intercom with your API key and App ID.
    /// </summary>
    /// <param name="apiKey">Your Intercom API key.</param>
    /// <param name="appId">Your Intercom App ID.</param>
    public void Initialize(string apiKey, string appId)
    {
        IntercomSdk.Initialize(Platform.CurrentActivity, apiKey, appId);
    }

    /// <summary>
    /// Register a user using their userId 
    /// </summary>
    /// <param name="userId">The userId of the user you want to register</param>
    /// <param name="onSuccess">An optional callback used when the registration is successful</param>
    /// <param name="onFailure">An optional callback used when the registration is not successful</param>
    /// <exception cref="ArgumentException">Thrown when the userId is null or empty</exception>
    public void RegisterWithUserId(string userId, Action? onSuccess = null, Action<string>? onFailure = null)
    {
        if (string.IsNullOrEmpty(userId))
        {
            throw new ArgumentException($"'{nameof(userId)}' cannot be null or empty.", nameof(userId));
        }

        var userAttributes = new Dictionary<string, string>();
        userAttributes.Add("userId", userId);
        IntercomSdk.RegisterUser(userAttributes);
        //IntercomSdk.RegisterUser(userAttributes.ToJavaMap(), new IntercomCallback(onSuccess, onFailure));
    }

    public void Register(Action? onSuccess = null, Action<string>? onFailure = null)
    {

    }

    /// <summary>
    /// Register a user using their email 
    /// </summary>
    /// <param name="email">The email address of the user you want to register</param>
    /// <param name="onSuccess">An optional callback used when the registration is successful</param>
    /// <param name="onFailure">An optional callback used when the registration is not successful</param>
    /// <exception cref="ArgumentException">Thrown when the email is null or empty</exception>
    public void RegisterWithEmail(string email, Action? onSuccess = null, Action<string>? onFailure = null)
    {
        if (string.IsNullOrEmpty(email))
        {
            throw new ArgumentException($"'{nameof(email)}' cannot be null or empty.", nameof(email));
        }

        var userAttributes = new Dictionary<string, string>();
        userAttributes.Add("email", email);
        IntercomSdk.RegisterUser(userAttributes);
    }

    public void SetUserHash(string userHash)
    {
        IntercomSdk.SetUserHash(userHash);
    }

    public void PresentMessenger(string? message)
    {
        IntercomSdk.PresentMessenger(message);
    }

    public void PresentHelpCenter()
    {
        IntercomSdk.PresentHelpCenter();
    }

    public void PresentSupportCenter()
    {
        IntercomSdk.PresentSupportCenter();
    }

    public void PresentCarousel(string carouselId)
    {
        IntercomSdk.PresentCarousel(carouselId);
    }

    public void SetVisible(bool isVisible)
    {
        IntercomSdk.SetVisible(isVisible ? Java.Lang.Boolean.True : Java.Lang.Boolean.False);
    }

    public void SetBottomPadding(int bottomPadding)
    {
        IntercomSdk.SetBottomPadding(new Java.Lang.Integer(bottomPadding));
    }

    public void Logout()
    {
        IntercomSdk.Logout();
    }
}

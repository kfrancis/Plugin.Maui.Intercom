namespace Plugin.Maui.Intercom;

public interface IIntercom
{
    /// <summary>
    /// Initialize Intercom with your API key and App ID.
    /// </summary>
    /// <param name="apiKey">Your Intercom API key.</param>
    /// <param name="appId">Your Intercom App ID.</param>
    void Initialize(string apiKey, string appId);

    void Register(Action? onSuccess = null, Action<string?>? onFailure = null);

    /// <summary>
    /// Register a user using their userId 
    /// </summary>
    /// <param name="userId">The userId of the user you want to register</param>
    /// <param name="onSuccess">An optional callback used when the registration is successful</param>
    /// <param name="onFailure">An optional callback used when the registration is not successful</param>
    /// <exception cref="ArgumentException">Thrown when the userId is null or empty</exception>
    void RegisterWithUserId(string userId, Action? onSuccess = null, Action<string?>? onFailure = null);

    /// <summary>
    /// Register a user using their email 
    /// </summary>
    /// <param name="email">The email address of the user you want to register</param>
    /// <param name="onSuccess">An optional callback used when the registration is successful</param>
    /// <param name="onFailure">An optional callback used when the registration is not successful</param>
    /// <exception cref="ArgumentException">Thrown when the email is null or empty</exception>
    void RegisterWithEmail(string email, Action? onSuccess = null, Action<string?>? onFailure = null);

    void Logout();

    void SetUserHash(string userHash);

    void PresentMessenger(string? message);

    void PresentHelpCenter();

    void PresentSupportCenter();

    void PresentCarousel(string carouselId);

    void SetVisible(bool isVisible);

    void SetBottomPadding(int bottomPadding);
}

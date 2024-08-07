using System;

namespace Plugin.Maui.Intercom;

partial class IntercomImplementation : IIntercom
{
    public void Initialize(string apiKey, string appId)
    {
        throw new NotImplementedException();
    }

    public void RegisterWithUserId(string userId, Action? onSuccess = null, Action<string>? onFailure = null)
    {
        throw new NotImplementedException();
    }

    public void RegisterWithEmail(string email, Action? onSuccess = null, Action<string>? onFailure = null)
    {
        throw new NotImplementedException();
    }

    public void Logout()
    {
        throw new NotImplementedException();
    }

    public void SetUserHash(string userHash)
    {
        throw new NotImplementedException();
    }

    public void PresentMessenger(string? message)
    {
        throw new NotImplementedException();
    }

    public void PresentHelpCenter()
    {
        throw new NotImplementedException();
    }

    public void PresentSupportCenter()
    {
        throw new NotImplementedException();
    }

    public void PresentCarousel(string carouselId)
    {
        throw new NotImplementedException();
    }

    public void SetVisible(bool isVisible)
    {
        throw new NotImplementedException();
    }

    public void SetBottomPadding(int bottomPadding)
    {
        throw new NotImplementedException();
    }

}
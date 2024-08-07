using System.Security.Cryptography;
using System.Text;
using CommunityToolkit.Mvvm.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Plugin.Maui.Intercom;

namespace MauiSample;

public partial class MainPage : ContentPage
{
    private readonly IConfiguration _configuration;

    public MainPage(IConfiguration configuration)
    {
        InitializeComponent();
        newBindingSampleLabel.Text = "Hello, world!";
        _configuration = configuration;
    }
    public string GetHMAC(string key, string message)
    {
        // change according to your needs, an UTF8Encoding
        // could be more suitable in certain situations
        var encoding = new UTF8Encoding();

        var messageBytes = encoding.GetBytes(message);
        var keyBytes = encoding.GetBytes(key);

        byte[] hashBytes;

        using (var hash = new HMACSHA256(keyBytes))
            hashBytes = hash.ComputeHash(messageBytes);

        return BitConverter.ToString(hashBytes).Replace("-", "").ToLower();
    }


    protected override void OnAppearing()
    {
        base.OnAppearing();

        var intercom = Ioc.Default.GetRequiredService<IIntercom>();
        var intercomApiKey = _configuration.GetValue("Intercom:DroidApiKey", string.Empty) ?? string.Empty;
        var intercomAppId = _configuration.GetValue("Intercom:AppId", string.Empty) ?? string.Empty;
        var intercomSecret = _configuration.GetValue("Intercom:DroidSecret", string.Empty) ?? string.Empty;
        intercom?.Initialize(intercomApiKey, intercomAppId);

        //// If user verification is not on, you don't need to set the user hash
        //intercom?.RegisterWithEmail("test@test.com");

        //// If user verification is on, you need to set the user hash
        //intercom?.SetUserHash(GetHMAC(intercomSecret, "test@test.com"));
        //intercom?.RegisterWithEmail("test@test.com");

        // If there's no user info at all, you can just call register
        intercom?.Register();

        intercom?.PresentHelpCenter();
    }

    async void OnDocsButtonClicked(object sender, EventArgs e)
    {
        try
        {
            Uri uri = new Uri("https://learn.microsoft.com/dotnet/communitytoolkit/maui/native-library-interop/get-started");
            await Browser.Default.OpenAsync(uri, BrowserLaunchMode.SystemPreferred);
        }
        catch (Exception ex)
        {
            throw new Exception("Browser failed to launch", ex);
        }
    }
}


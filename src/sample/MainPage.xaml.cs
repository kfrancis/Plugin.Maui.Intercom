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

    protected override void OnAppearing()
    {
        base.OnAppearing();

        var intercom = Ioc.Default.GetRequiredService<IIntercom>();
        var intercomApiKey = _configuration.GetValue("Intercom:ApiKey", string.Empty) ?? string.Empty;
        var intercomAppId = _configuration.GetValue("Intercom:AppId", string.Empty) ?? string.Empty;
        intercom?.Initialize(intercomApiKey, intercomAppId);
        intercom?.RegisterWithUserId("kfrancis@clinicalsupportsystems.com");
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


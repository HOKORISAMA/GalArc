using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GalArc.I18n;
using GalArc.Infrastructure.Logging;
using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;

namespace GalArc.ViewModels;

internal partial class UpdateViewModel : ViewModelBase
{
    private static readonly Assembly CurrentAssembly = Assembly.GetExecutingAssembly();

    private const string ApiUrl = "https://api.github.com/repos/detached64/GalArc/releases/latest";

    private static string UpdateUrl;

    public UpdateViewModel()
    {
        if (Design.IsDesignMode)
            return;
        CheckUpdatesCommand.Execute(null);
    }

    [ObservableProperty]
    private string statusMessage;

    [ObservableProperty]
    private bool isChecking;

    [ObservableProperty]
    private bool isSuccess;

    [RelayCommand]
    private async Task CheckUpdates()
    {
        LogInfo(MsgStrings.CheckingUpdates);
        IsChecking = true;

        using HttpClient client = new();
        string currentVer = CurrentAssembly.GetName().Version?.ToString();
        client.DefaultRequestHeaders.UserAgent.Add(
            new ProductInfoHeaderValue(CurrentAssembly.GetName().Name, currentVer));
        try
        {
            HttpResponseMessage response = await client.GetAsync(ApiUrl);
            response.EnsureSuccessStatusCode();
            string jsonResponse = await response.Content.ReadAsStringAsync();
            using JsonDocument doc = JsonDocument.Parse(jsonResponse);
            JsonElement root = doc.RootElement;
            string latestVersion = root.TryGetProperty("tag_name", out JsonElement tagNameElement)
                ? tagNameElement.GetString()
                : throw new JsonException("Tag \"tag_name\" not found in the response.");
            UpdateUrl = root.TryGetProperty("html_url", out JsonElement htmlUrlElement)
                ? htmlUrlElement.GetString()
                : throw new JsonException("Tag \"html_url\" not found in the response.");
            LogInfo(string.Format(MsgStrings.CurrentVersion, currentVer.TrimStart('v')) + Environment.NewLine + string.Format(MsgStrings.LatestVersion, latestVersion.TrimStart('v')));
            IsSuccess = true;
        }
        catch (Exception e)
        {
            LogError(string.Format(MsgStrings.ErrorCheckingUpdates, e.Message), e);
        }
        finally
        {
            IsChecking = false;
        }
    }

    [RelayCommand]
    private async Task OpenUpdateUrl()
    {
        if (!string.IsNullOrEmpty(UpdateUrl))
        {
            try
            {
                await App.Top.Launcher.LaunchUriAsync(new Uri(UpdateUrl));
            }
            catch (Exception ex)
            {
                LogError(string.Format(MsgStrings.ErrorOpenUrl, ex.Message), ex);
            }
        }
    }

    [RelayCommand]
    private static void Exit(Window window)
    {
        window?.Close();
    }

    private void LogInfo(string log)
    {
        StatusMessage = log;
        Logger.Info(log);
    }

    private void LogError(string log, Exception ex)
    {
        StatusMessage = log;
        Logger.Error(log);
        Logger.Error(ex.ToString());
    }
}

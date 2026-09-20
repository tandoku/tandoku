namespace Tandoku.App;

using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Tandoku.App.Films;

public partial class MainPage : ContentPage
{
    private static readonly FilePickerFileType YamlFileType = new(
        new Dictionary<DevicePlatform, IEnumerable<string>>
        {
            [DevicePlatform.MacCatalyst] = ["public.yaml", "public.text"],
            [DevicePlatform.WinUI] = [".yaml", ".yml"],
        });

    private readonly FilmBrowserViewModel viewModel = new();

    public MainPage()
    {
        this.InitializeComponent();
        this.BindingContext = this.viewModel;
        this.SizeChanged += this.OnPageSizeChanged;
    }

    private async void OnOpenDatabaseClicked(object? sender, EventArgs e)
    {
        if (this.viewModel.IsLoading)
        {
            return;
        }

        try
        {
#if MACCATALYST
            var result = await MacCatalystFilmDatabasePicker.PickAsync(this.ReportStatus);
#else
            this.ReportStatus("Presenting the system file picker.");
            var result = await FilePicker.Default.PickAsync(
                new PickOptions
                {
                    PickerTitle = "Choose a films.yaml database",
                    FileTypes = YamlFileType,
                });
#endif
            if (result is null)
            {
                this.ReportStatus("No film database selected.");
                return;
            }

            await this.LoadDatabaseAsync(result);
        }
        catch (Exception exception) when (
            exception is InvalidDataException
            or IOException
            or UnauthorizedAccessException
            or SharpYaml.YamlException)
        {
            this.viewModel.ShowError($"Unable to open films.yaml: {exception.Message}");
        }
    }

    private async Task LoadDatabaseAsync(FileResult result)
    {
        this.WriteDiagnostic($"LoadDatabaseAsync entered for {result.FileName}.");
        this.viewModel.BeginLoading($"Reading {result.FileName}…");
        this.WriteDiagnostic("Loading state displayed.");
        try
        {
            await Task.Yield();
            this.WriteDiagnostic("UI thread yielded.");
            this.WriteDiagnostic("Opening selected file stream.");
            await using var stream = await result.OpenReadAsync();
            this.WriteDiagnostic("Selected file stream opened.");
            this.ReportStatus($"Parsing {result.FileName}…");
            var films = await FilmDatabaseLoader.LoadAsync(stream);
            this.ReportStatus($"Preparing {films.Count} film cards…");
            await this.viewModel.LoadFilmsAsync(films, result.FileName);
            this.ReportStatus($"Loaded {films.Count} films from {result.FileName}.");
        }
        catch (Exception exception) when (
            exception is InvalidDataException
            or IOException
            or UnauthorizedAccessException
            or SharpYaml.YamlException)
        {
            this.viewModel.ShowError($"Unable to open films.yaml: {exception.Message}");
        }
        finally
        {
            this.viewModel.EndLoading();
        }
    }

    private void ReportStatus(string message)
    {
        this.viewModel.ShowStatus(message);
        this.WriteDiagnostic(message);
    }

    private void WriteDiagnostic(string message)
    {
        var logMessage = $"{DateTimeOffset.Now:O} [FilmBrowser] {message}";
        Debug.WriteLine(logMessage);
        if (this.Handler?.MauiContext?.Services.GetService(typeof(ILogger<MainPage>)) is ILogger<MainPage> logger)
        {
            logger.LogInformation("{FilmBrowserStatus}", message);
        }

    }

    private void OnPageSizeChanged(object? sender, EventArgs e)
    {
        var galleryWidth = Math.Max(0, this.Width - this.BrowserLayout.ColumnDefinitions[0].Width.Value - 48);
        this.FilmGridLayout.Span = galleryWidth switch
        {
            >= 1100 => 5,
            >= 860 => 4,
            >= 620 => 3,
            >= 390 => 2,
            _ => 1,
        };
    }

    private void OnIntegerSliderValueChanged(object? sender, ValueChangedEventArgs e)
    {
        if (sender is not Slider slider)
        {
            return;
        }

        var wholeLevel = Math.Clamp(
            Math.Round(e.NewValue, MidpointRounding.AwayFromZero),
            slider.Minimum,
            slider.Maximum);
        if (slider.Value != wholeLevel)
        {
            slider.Value = wholeLevel;
        }
    }

    private async void OnSourceLinkClicked(object? sender, EventArgs e)
    {
        if (sender is not Button { CommandParameter: string url }
            || !Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            return;
        }

        try
        {
            if (!await Launcher.Default.OpenAsync(uri))
            {
                this.viewModel.ShowError($"Unable to open {uri.Host}.");
            }
        }
        catch (FeatureNotSupportedException exception)
        {
            this.viewModel.ShowError($"Unable to open the source link: {exception.Message}");
        }
    }
}
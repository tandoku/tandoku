namespace Tandoku.App;

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
        try
        {
            var result = await FilePicker.Default.PickAsync(
                new PickOptions
                {
                    PickerTitle = "Choose a films.yaml database",
                    FileTypes = YamlFileType,
                });
            if (result is null)
            {
                return;
            }

            await using var stream = await result.OpenReadAsync();
            var films = await FilmDatabaseLoader.LoadAsync(stream);
            this.viewModel.LoadFilms(films, result.FileName);
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
}
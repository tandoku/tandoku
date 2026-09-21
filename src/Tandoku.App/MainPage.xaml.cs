namespace Tandoku.App;

using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Tandoku.App.Films;
#if MACCATALYST
using Microsoft.Maui.Controls.Handlers.Items;
using UIKit;
#endif

public partial class MainPage : ContentPage
{
    private const double HorizontalItemSpacing = 18;
    private const double MaximumCardWidth = 200;
    private const double PosterHeightToWidthRatio = 1.5;
    private const double CardDetailsHeight = 68;
    private const double VerticalItemSpacing = 24;

    private static readonly FilePickerFileType YamlFileType = new(
        new Dictionary<DevicePlatform, IEnumerable<string>>
        {
            [DevicePlatform.MacCatalyst] = ["public.yaml", "public.text"],
            [DevicePlatform.WinUI] = [".yaml", ".yml"],
        });

    private readonly FilmBrowserViewModel viewModel = new();
    private CancellationTokenSource? galleryLayoutCancellation;
    private double cardWidth = 160;
    private double posterHeight = 240;
#if MACCATALYST
    private MauiCollectionView? platformFilmCollection;
#endif

    public double CardWidth
    {
        get => this.cardWidth;
        private set
        {
            if (this.cardWidth == value)
            {
                return;
            }

            this.cardWidth = value;
            this.OnPropertyChanged();
        }
    }

    public double PosterHeight
    {
        get => this.posterHeight;
        private set
        {
            if (this.posterHeight == value)
            {
                return;
            }

            this.posterHeight = value;
            this.OnPropertyChanged();
        }
    }

    public MainPage()
    {
        this.InitializeComponent();
        this.BindingContext = this.viewModel;
        this.FilmCollection.SizeChanged += this.OnGallerySizeChanged;
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
            this.ApplyGalleryLayout();
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

    private void OnGallerySizeChanged(object? sender, EventArgs e)
    {
        var cancellation = new CancellationTokenSource();
        var previousCancellation = this.galleryLayoutCancellation;
        this.galleryLayoutCancellation = cancellation;
        previousCancellation?.Cancel();
        _ = this.UpdateGalleryLayoutAsync(cancellation);
    }

    private async Task UpdateGalleryLayoutAsync(CancellationTokenSource cancellation)
    {
        try
        {
            await Task.Delay(TimeSpan.FromMilliseconds(150), cancellation.Token);
            this.ApplyGalleryLayout();
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
        }
        finally
        {
            if (ReferenceEquals(this.galleryLayoutCancellation, cancellation))
            {
                this.galleryLayoutCancellation = null;
            }

            cancellation.Dispose();
        }
    }

    private void ApplyGalleryLayout()
    {
        var galleryWidth = this.FilmCollection.Width;
        if (galleryWidth <= 0)
        {
            return;
        }

        var (span, cardWidth) = CalculateGalleryLayout(galleryWidth);
#if MACCATALYST
        this.EnsureMacCatalystGalleryLayout();
#endif
        this.CardWidth = cardWidth;
        this.PosterHeight = cardWidth * PosterHeightToWidthRatio;
#if MACCATALYST
        (this.platformFilmCollection?.CollectionViewLayout as AdaptiveGalleryLayout)?.InvalidateLayout();
#else
        if (this.FilmGridLayout.Span != span)
        {
            this.FilmGridLayout.Span = span;
        }
#endif
    }

    private static (int Span, double CardWidth) CalculateGalleryLayout(double galleryWidth)
    {
        var span = Math.Max(
            1,
            (int)Math.Ceiling(
                (galleryWidth + HorizontalItemSpacing) / (MaximumCardWidth + HorizontalItemSpacing)));
        var availableCardWidth =
            (galleryWidth - (HorizontalItemSpacing * (span - 1))) / span;

        return (span, Math.Max(1, Math.Floor(Math.Min(MaximumCardWidth, availableCardWidth))));
    }

#if MACCATALYST
    private void EnsureMacCatalystGalleryLayout()
    {
        if (this.platformFilmCollection is null
            && this.FilmCollection.Handler?.PlatformView is UIView platformView)
        {
            this.platformFilmCollection = FindCollectionView(platformView);
        }

        if (this.platformFilmCollection is null)
        {
            return;
        }

        if (this.platformFilmCollection.CollectionViewLayout is AdaptiveGalleryLayout)
        {
            return;
        }

        this.platformFilmCollection.SetCollectionViewLayout(new AdaptiveGalleryLayout(), false);
    }

    private static NSCollectionLayoutSection CreateGallerySection(double galleryWidth)
    {
        var (span, cardWidth) = CalculateGalleryLayout(galleryWidth);
        var cardHeight = (cardWidth * PosterHeightToWidthRatio) + CardDetailsHeight;
        var itemSize = NSCollectionLayoutSize.Create(
            NSCollectionLayoutDimension.CreateAbsolute((nfloat)cardWidth),
            NSCollectionLayoutDimension.CreateAbsolute((nfloat)cardHeight));
        var item = NSCollectionLayoutItem.Create(itemSize);
        var groupSize = NSCollectionLayoutSize.Create(
            NSCollectionLayoutDimension.CreateFractionalWidth(1),
            NSCollectionLayoutDimension.CreateAbsolute((nfloat)cardHeight));
        var group = NSCollectionLayoutGroup.CreateHorizontal(groupSize, item, span);
        group.InterItemSpacing = NSCollectionLayoutSpacing.CreateFixed((nfloat)HorizontalItemSpacing);

        var section = NSCollectionLayoutSection.Create(group);
        section.InterGroupSpacing = (nfloat)VerticalItemSpacing;
        return section;
    }

    private static MauiCollectionView? FindCollectionView(UIView view)
    {
        if (view is MauiCollectionView collectionView)
        {
            return collectionView;
        }

        foreach (var subview in view.Subviews)
        {
            var descendantCollectionView = FindCollectionView(subview);
            if (descendantCollectionView is not null)
            {
                return descendantCollectionView;
            }
        }

        return null;
    }

    private sealed class AdaptiveGalleryLayout : UICollectionViewCompositionalLayout
    {
        public AdaptiveGalleryLayout()
            : base(
                static (_, environment) => CreateGallerySection(environment.Container.ContentSize.Width),
                new UICollectionViewCompositionalLayoutConfiguration
                {
                    ScrollDirection = UICollectionViewScrollDirection.Vertical,
                })
        {
        }
    }
#endif

    private void OnFilmCardTapped(object? sender, TappedEventArgs e)
    {
        if (e.Parameter is not FilmRecord film)
        {
            return;
        }

        this.viewModel.ShowDetailsCommand.Execute(film);
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
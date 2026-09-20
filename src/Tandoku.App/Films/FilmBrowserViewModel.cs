namespace Tandoku.App.Films;

using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Windows.Input;

public sealed class FilmBrowserViewModel : INotifyPropertyChanged
{
    public const string AllFilter = "All";
    public const string MoviesFilter = "Movies";
    public const string TvFilter = "TV";
    public const string AnimatedFilter = "Animated";
    public const string LiveActionFilter = "Live action";

    private readonly List<FilmRecord> allFilms = [];
    private CancellationTokenSource? filterCancellation;
    private double minimumLevel;
    private double maximumLevel;
    private double selectedMinimumLevel;
    private double selectedMaximumLevel;
    private double minimumImdbRating;
    private string mediaTypeFilter = AllFilter;
    private string formatFilter = AllFilter;
    private bool hasFilms;
    private bool hasError;
    private string errorMessage = string.Empty;
    private string databaseName = string.Empty;
    private FilmRecord? selectedFilm;
    private bool isDetailsVisible;
    private bool isLoading;
    private IReadOnlyList<FilmRecord> visibleFilms = [];
    private string statusMessage = "Choose a films.yaml database to begin.";
    private bool suppressFilterUpdates;

    public FilmBrowserViewModel()
    {
        this.ShowDetailsCommand = new Command<FilmRecord>(this.ShowDetails);
        this.CloseDetailsCommand = new Command(this.CloseDetails);
        this.ClearFiltersCommand = new Command(this.ClearFilters);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public IReadOnlyList<FilmRecord> VisibleFilms
    {
        get => this.visibleFilms;
        private set => this.SetProperty(ref this.visibleFilms, value);
    }

    public ObservableCollection<ImdbListFilter> ImdbListFilters { get; } = [];

    public ICommand ShowDetailsCommand { get; }

    public ICommand CloseDetailsCommand { get; }

    public ICommand ClearFiltersCommand { get; }

    public double MinimumLevel
    {
        get => this.minimumLevel;
        private set => this.SetProperty(ref this.minimumLevel, value);
    }

    public double MaximumLevel
    {
        get => this.maximumLevel;
        private set => this.SetProperty(ref this.maximumLevel, value);
    }

    public double SelectedMinimumLevel
    {
        get => this.selectedMinimumLevel;
        set
        {
            var wholeLevel = Math.Round(value, MidpointRounding.AwayFromZero);
            var adjustedValue = Math.Clamp(wholeLevel, this.MinimumLevel, this.SelectedMaximumLevel);
            if (this.SetProperty(ref this.selectedMinimumLevel, adjustedValue))
            {
                this.OnPropertyChanged(nameof(this.SelectedLevelRangeDisplay));
                this.QueueApplyFilters();
            }
        }
    }

    public double SelectedMaximumLevel
    {
        get => this.selectedMaximumLevel;
        set
        {
            var wholeLevel = Math.Round(value, MidpointRounding.AwayFromZero);
            var adjustedValue = Math.Clamp(wholeLevel, this.SelectedMinimumLevel, this.MaximumLevel);
            if (this.SetProperty(ref this.selectedMaximumLevel, adjustedValue))
            {
                this.OnPropertyChanged(nameof(this.SelectedLevelRangeDisplay));
                this.QueueApplyFilters();
            }
        }
    }

    public string SelectedLevelRangeDisplay => $"{this.SelectedMinimumLevel:0} – {this.SelectedMaximumLevel:0}";

    public double MinimumImdbRating
    {
        get => this.minimumImdbRating;
        set
        {
            if (this.SetProperty(ref this.minimumImdbRating, value))
            {
                this.OnPropertyChanged(nameof(this.MinimumImdbRatingDisplay));
                this.QueueApplyFilters();
            }
        }
    }

    public string MinimumImdbRatingDisplay => this.MinimumImdbRating == 0
        ? "Any rating"
        : $"{this.MinimumImdbRating:0.0}+";

    public string MediaTypeFilter
    {
        get => this.mediaTypeFilter;
        set
        {
            if (value is not (AllFilter or MoviesFilter or TvFilter))
            {
                throw new ArgumentOutOfRangeException(nameof(value), value, "Unknown media type filter.");
            }

            if (this.SetProperty(ref this.mediaTypeFilter, value))
            {
                this.QueueApplyFilters();
            }
        }
    }

    public string FormatFilter
    {
        get => this.formatFilter;
        set
        {
            if (value is not (AllFilter or AnimatedFilter or LiveActionFilter))
            {
                throw new ArgumentOutOfRangeException(nameof(value), value, "Unknown format filter.");
            }

            if (this.SetProperty(ref this.formatFilter, value))
            {
                this.QueueApplyFilters();
            }
        }
    }

    public bool HasFilms
    {
        get => this.hasFilms;
        private set
        {
            if (this.SetProperty(ref this.hasFilms, value))
            {
                this.OnPropertyChanged(nameof(this.NeedsDatabase));
            }
        }
    }

    public bool NeedsDatabase => !this.HasFilms;

    public bool IsLoading
    {
        get => this.isLoading;
        private set
        {
            if (this.SetProperty(ref this.isLoading, value))
            {
                this.OnPropertyChanged(nameof(this.CanOpenDatabase));
            }
        }
    }

    public bool CanOpenDatabase => !this.IsLoading;

    public string StatusMessage
    {
        get => this.statusMessage;
        private set => this.SetProperty(ref this.statusMessage, value);
    }

    public bool HasError
    {
        get => this.hasError;
        private set => this.SetProperty(ref this.hasError, value);
    }

    public string ErrorMessage
    {
        get => this.errorMessage;
        private set => this.SetProperty(ref this.errorMessage, value);
    }

    public string DatabaseName
    {
        get => this.databaseName;
        private set => this.SetProperty(ref this.databaseName, value);
    }

    public FilmRecord? SelectedFilm
    {
        get => this.selectedFilm;
        private set => this.SetProperty(ref this.selectedFilm, value);
    }

    public bool IsDetailsVisible
    {
        get => this.isDetailsVisible;
        private set => this.SetProperty(ref this.isDetailsVisible, value);
    }

    public string ResultCountDisplay =>
        $"{this.VisibleFilms.Count.ToString(CultureInfo.InvariantCulture)} of {this.allFilms.Count.ToString(CultureInfo.InvariantCulture)} titles";

    public async Task LoadFilmsAsync(IEnumerable<FilmRecord> films, string databaseName)
    {
        this.allFilms.Clear();
        this.allFilms.AddRange(films);
        this.DatabaseName = databaseName;
        this.HasError = false;
        this.ErrorMessage = string.Empty;

        var levels = this.allFilms
            .Select(film => film.EffectiveNativelyLevel)
            .OfType<int>()
            .ToList();
        this.MinimumLevel = levels.Count > 0 ? levels.Min() : 1;
        this.MaximumLevel = levels.Count > 0
            ? Math.Max(this.MinimumLevel + 1, levels.Max())
            : 100;
        this.selectedMinimumLevel = this.MinimumLevel;
        this.selectedMaximumLevel = this.MaximumLevel;
        this.minimumImdbRating = 0;
        this.OnPropertyChanged(nameof(this.SelectedMinimumLevel));
        this.OnPropertyChanged(nameof(this.SelectedMaximumLevel));
        this.OnPropertyChanged(nameof(this.SelectedLevelRangeDisplay));
        this.OnPropertyChanged(nameof(this.MinimumImdbRating));
        this.OnPropertyChanged(nameof(this.MinimumImdbRatingDisplay));

        foreach (var filter in this.ImdbListFilters)
        {
            filter.PropertyChanged -= this.OnImdbListFilterChanged;
        }

        this.ImdbListFilters.Clear();
        var imdbLists = this.allFilms
            .SelectMany(film => film.EffectiveImdbLists)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(value => value, StringComparer.OrdinalIgnoreCase);
        foreach (var list in imdbLists)
        {
            var filter = new ImdbListFilter(list);
            filter.PropertyChanged += this.OnImdbListFilterChanged;
            this.ImdbListFilters.Add(filter);
        }

        this.HasFilms = this.allFilms.Count > 0;
        this.CloseDetails();
        await this.ApplyFiltersAsync(TimeSpan.Zero);
    }

    public void ShowError(string message)
    {
        this.ErrorMessage = message;
        this.HasError = true;
        this.StatusMessage = message;
    }

    public void ShowStatus(string message)
    {
        this.StatusMessage = message;
    }

    public void BeginLoading(string message)
    {
        this.HasError = false;
        this.ErrorMessage = string.Empty;
        this.StatusMessage = message;
        this.IsLoading = true;
    }

    public void EndLoading()
    {
        this.IsLoading = false;
    }

    private void QueueApplyFilters()
    {
        if (!this.suppressFilterUpdates)
        {
            _ = this.ApplyFiltersAsync(TimeSpan.FromMilliseconds(150));
        }
    }

    private async Task ApplyFiltersAsync(TimeSpan delay)
    {
        var cancellation = new CancellationTokenSource();
        var previousCancellation = this.filterCancellation;
        this.filterCancellation = cancellation;
        previousCancellation?.Cancel();

        var films = this.allFilms.ToArray();
        var selectedLists = this.ImdbListFilters
            .Where(filter => filter.IsSelected)
            .Select(filter => filter.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var criteria = new FilmFilterCriteria(
            this.SelectedMinimumLevel,
            this.SelectedMaximumLevel,
            this.MinimumLevel,
            this.MaximumLevel,
            this.MinimumImdbRating,
            this.MediaTypeFilter,
            this.FormatFilter,
            selectedLists);

        try
        {
            if (delay > TimeSpan.Zero)
            {
                await Task.Delay(delay, cancellation.Token);
            }

            var filteredFilms = await Task.Run(
                () => FilterFilms(films, criteria, cancellation.Token),
                cancellation.Token);
            if (!ReferenceEquals(this.filterCancellation, cancellation))
            {
                return;
            }

            this.VisibleFilms = filteredFilms;
            this.OnPropertyChanged(nameof(this.ResultCountDisplay));
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
        }
        finally
        {
            if (ReferenceEquals(this.filterCancellation, cancellation))
            {
                this.filterCancellation = null;
            }

            cancellation.Dispose();
        }
    }

    private static IReadOnlyList<FilmRecord> FilterFilms(
        IEnumerable<FilmRecord> films,
        FilmFilterCriteria criteria,
        CancellationToken cancellationToken)
    {
        var levelRangeIsUnrestricted =
            (criteria.MinimumLevel <= criteria.AvailableMinimumLevel)
            && (criteria.MaximumLevel >= criteria.AvailableMaximumLevel);

        return films
            .Where(film =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                return (criteria.MediaType == AllFilter
                    || (film.IsTv ? criteria.MediaType == TvFilter : criteria.MediaType == MoviesFilter))
                    && (criteria.Format == AllFilter
                        || (film.Animated
                            ? criteria.Format == AnimatedFilter
                            : criteria.Format == LiveActionFilter))
                    && (criteria.MinimumImdbRating == 0
                        || (film.EffectiveImdbRating is double rating && rating >= criteria.MinimumImdbRating))
                    && (levelRangeIsUnrestricted
                        || (film.EffectiveNativelyLevel is int level
                            && (level >= criteria.MinimumLevel)
                            && (level <= criteria.MaximumLevel)))
                    && (criteria.ImdbLists.Count == 0
                        || film.EffectiveImdbLists.Any(criteria.ImdbLists.Contains));
            })
            .OrderBy(film => film.EffectiveNativelyLevel ?? int.MaxValue)
            .ThenByDescending(film => film.EffectiveImdbRating ?? double.MinValue)
            .ThenBy(film => film.Title, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    private void ClearFilters()
    {
        this.suppressFilterUpdates = true;
        try
        {
            this.MediaTypeFilter = AllFilter;
            this.FormatFilter = AllFilter;
            this.SelectedMinimumLevel = this.MinimumLevel;
            this.SelectedMaximumLevel = this.MaximumLevel;
            this.MinimumImdbRating = 0;
            foreach (var filter in this.ImdbListFilters)
            {
                filter.IsSelected = false;
            }
        }
        finally
        {
            this.suppressFilterUpdates = false;
        }

        this.QueueApplyFilters();
    }

    private void ShowDetails(FilmRecord? film)
    {
        if (film is null)
        {
            return;
        }

        this.SelectedFilm = film;
        this.IsDetailsVisible = true;
    }

    private void CloseDetails()
    {
        this.IsDetailsVisible = false;
        this.SelectedFilm = null;
    }

    private void OnImdbListFilterChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ImdbListFilter.IsSelected))
        {
            this.QueueApplyFilters();
        }
    }

    private bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        this.OnPropertyChanged(propertyName);
        return true;
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    private sealed record FilmFilterCriteria(
        double MinimumLevel,
        double MaximumLevel,
        double AvailableMinimumLevel,
        double AvailableMaximumLevel,
        double MinimumImdbRating,
        string MediaType,
        string Format,
        HashSet<string> ImdbLists);
}

public sealed class ImdbListFilter : INotifyPropertyChanged
{
    private bool isSelected;

    public ImdbListFilter(string name)
    {
        this.Name = name;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public string Name { get; }

    public bool IsSelected
    {
        get => this.isSelected;
        set
        {
            if (this.isSelected == value)
            {
                return;
            }

            this.isSelected = value;
            this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(this.IsSelected)));
        }
    }
}

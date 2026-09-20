namespace Tandoku.App.Films;

using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Windows.Input;

public sealed class FilmBrowserViewModel : INotifyPropertyChanged
{
    private readonly List<FilmRecord> allFilms = [];
    private double minimumLevel;
    private double maximumLevel;
    private double selectedMinimumLevel;
    private double selectedMaximumLevel;
    private double minimumImdbRating;
    private bool includeMovies = true;
    private bool includeTv = true;
    private bool includeAnimated = true;
    private bool includeLiveAction = true;
    private bool hasFilms;
    private bool hasError;
    private string errorMessage = string.Empty;
    private string databaseName = string.Empty;
    private FilmRecord? selectedFilm;
    private bool isDetailsVisible;
    private bool isLoading;
    private IReadOnlyList<FilmRecord> visibleFilms = [];
    private string statusMessage = "Choose a films.yaml database to begin.";

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
            var adjustedValue = Math.Min(value, this.SelectedMaximumLevel);
            if (this.SetProperty(ref this.selectedMinimumLevel, adjustedValue))
            {
                this.OnPropertyChanged(nameof(this.SelectedLevelRangeDisplay));
                this.ApplyFilters();
            }
        }
    }

    public double SelectedMaximumLevel
    {
        get => this.selectedMaximumLevel;
        set
        {
            var adjustedValue = Math.Max(value, this.SelectedMinimumLevel);
            if (this.SetProperty(ref this.selectedMaximumLevel, adjustedValue))
            {
                this.OnPropertyChanged(nameof(this.SelectedLevelRangeDisplay));
                this.ApplyFilters();
            }
        }
    }

    public string SelectedLevelRangeDisplay => $"{this.SelectedMinimumLevel:0.#} – {this.SelectedMaximumLevel:0.#}";

    public double MinimumImdbRating
    {
        get => this.minimumImdbRating;
        set
        {
            if (this.SetProperty(ref this.minimumImdbRating, value))
            {
                this.OnPropertyChanged(nameof(this.MinimumImdbRatingDisplay));
                this.ApplyFilters();
            }
        }
    }

    public string MinimumImdbRatingDisplay => this.MinimumImdbRating == 0
        ? "Any rating"
        : $"{this.MinimumImdbRating:0.0}+";

    public bool IncludeMovies
    {
        get => this.includeMovies;
        set
        {
            if (this.SetProperty(ref this.includeMovies, value))
            {
                this.ApplyFilters();
            }
        }
    }

    public bool IncludeTv
    {
        get => this.includeTv;
        set
        {
            if (this.SetProperty(ref this.includeTv, value))
            {
                this.ApplyFilters();
            }
        }
    }

    public bool IncludeAnimated
    {
        get => this.includeAnimated;
        set
        {
            if (this.SetProperty(ref this.includeAnimated, value))
            {
                this.ApplyFilters();
            }
        }
    }

    public bool IncludeLiveAction
    {
        get => this.includeLiveAction;
        set
        {
            if (this.SetProperty(ref this.includeLiveAction, value))
            {
                this.ApplyFilters();
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

    public void LoadFilms(IEnumerable<FilmRecord> films, string databaseName)
    {
        this.allFilms.Clear();
        this.allFilms.AddRange(films);
        this.DatabaseName = databaseName;
        this.HasError = false;
        this.ErrorMessage = string.Empty;

        var levels = this.allFilms
            .Select(film => film.EffectiveNativelyLevel)
            .OfType<double>()
            .ToList();
        this.MinimumLevel = levels.Count > 0 ? Math.Floor(levels.Min()) : 0;
        this.MaximumLevel = levels.Count > 0
            ? Math.Max(this.MinimumLevel + 1, Math.Ceiling(levels.Max()))
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
        this.ApplyFilters();
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

    private void ApplyFilters()
    {
        var selectedLists = this.ImdbListFilters
            .Where(filter => filter.IsSelected)
            .Select(filter => filter.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var levelRangeIsUnrestricted =
            (this.SelectedMinimumLevel <= this.MinimumLevel)
            && (this.SelectedMaximumLevel >= this.MaximumLevel);

        var filteredFilms = this.allFilms
            .Where(film =>
                (film.IsTv ? this.IncludeTv : this.IncludeMovies)
                && (film.Animated ? this.IncludeAnimated : this.IncludeLiveAction)
                && (this.MinimumImdbRating == 0
                    || (film.EffectiveImdbRating is double rating && rating >= this.MinimumImdbRating))
                && (levelRangeIsUnrestricted
                    || (film.EffectiveNativelyLevel is double level
                        && (level >= this.SelectedMinimumLevel)
                        && (level <= this.SelectedMaximumLevel)))
                && (selectedLists.Count == 0
                    || film.EffectiveImdbLists.Any(selectedLists.Contains)))
            .OrderBy(film => film.EffectiveNativelyLevel ?? double.MaxValue)
            .ThenByDescending(film => film.EffectiveImdbRating ?? double.MinValue)
            .ThenBy(film => film.Title, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

        this.VisibleFilms = filteredFilms;
        this.OnPropertyChanged(nameof(this.ResultCountDisplay));
    }

    private void ClearFilters()
    {
        this.IncludeMovies = true;
        this.IncludeTv = true;
        this.IncludeAnimated = true;
        this.IncludeLiveAction = true;
        this.SelectedMinimumLevel = this.MinimumLevel;
        this.SelectedMaximumLevel = this.MaximumLevel;
        this.MinimumImdbRating = 0;
        foreach (var filter in this.ImdbListFilters)
        {
            filter.IsSelected = false;
        }
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
            this.ApplyFilters();
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

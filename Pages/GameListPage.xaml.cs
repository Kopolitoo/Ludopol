using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace Ludopol
{
    /// <summary>
    /// VIEW: a list of games (a category, a tag, a developer, recently added, or all).
    ///
    /// The filter buttons are in a column on the left of the screen. They have no menu: each
    /// activation moves a button to its next value, in a loop. For example
    /// "Any rating" → "3 or more" → "4 or more" → "5" → "Any rating". A filter in use is blue.
    /// </summary>
    public partial class GameListPage : UserControl, IPage
    {
        /// <summary>The choices of the compatibility menu, best first; null: all.</summary>
        private static readonly Compatibility?[] CompatibilityChoices =
        {
            null, Compatibility.Full, Compatibility.PartialGaze, Compatibility.PartialWithSwitch,
            Compatibility.Presumed, Compatibility.Unknown, Compatibility.None,
        };
        private static readonly int?[] RatingSteps = { null, 3, 4, 5 };
        private static readonly bool?[] YesNoSteps = { null, true, false };

        private readonly StackPanel _filters = new StackPanel();
        private readonly TileButton _installedFilter = new TileButton();
        private readonly TileButton _ownedFilter = new TileButton();
        private readonly TileButton _triedFilter = new TileButton();
        private readonly TileButton _categoryFilter = new TileButton();
        private readonly TileButton _compatibilityFilter = new TileButton();
        private readonly TileButton _ratingFilter = new TileButton();

        /// <summary>Below the filters: adds categories to every game of the list (batch editing).</summary>
        private readonly TileButton _addCategoryButton = new TileButton();
        private List<Game> _shown = new List<Game>();

        /// <summary>Right of the title: the sort criterion (a menu) and the direction (a toggle).</summary>
        private readonly StackPanel _sortButtons = new StackPanel { Orientation = Orientation.Horizontal };
        private readonly TileButton _sortByButton = new TileButton();
        private readonly TileButton _sortOrderButton = new TileButton();
        private int _count = -1;

        private readonly GameFilter _filter;
        private readonly string _title;

        private readonly bool _searchMode;

        /// <summary>A list of games. searchMode: the search page, with a field for the title above the tiles.</summary>
        public GameListPage(GameFilter filter, string title, bool searchMode = false)
        {
            InitializeComponent();
            _filter = filter;
            _title = title;
            _searchMode = searchMode;
            if (searchMode)
            {
                SearchBar.Visibility = Visibility.Visible;
                SearchBox.Text = filter.Search ?? "";
            }

            // A list opened from a category keeps it: no category filter there.
            List<TileButton> buttons = new List<TileButton> { _installedFilter, _ownedFilter, _triedFilter };
            if (filter.Category == null)
            {
                buttons.Add(_categoryFilter);
            }
            buttons.Add(_compatibilityFilter);
            buttons.Add(_ratingFilter);
            foreach (TileButton button in buttons)
            {
                button.SetResourceReference(WidthProperty, "SideButtonWidth");
                button.SetResourceReference(FontSizeProperty, "SmallFontSize");
                _filters.Children.Add(button);
            }
            _installedFilter.Click += OnInstalledFilterClick;
            _ownedFilter.Click += OnOwnedFilterClick;
            _triedFilter.Click += OnTriedFilterClick;
            _categoryFilter.Click += OnCategoryFilterClick;
            _compatibilityFilter.Click += OnCompatibilityFilterClick;
            _ratingFilter.Click += OnRatingFilterClick;

            _addCategoryButton.SetResourceReference(WidthProperty, "SideButtonWidth");
            _addCategoryButton.SetResourceReference(FontSizeProperty, "SmallFontSize");
            _addCategoryButton.Margin = new Thickness(12, 36, 12, 12);   // apart from the filters
            _addCategoryButton.Click += OnAddCategoryClick;
            _filters.Children.Add(_addCategoryButton);

            foreach (TileButton button in new[] { _sortByButton, _sortOrderButton })
            {
                button.SetResourceReference(WidthProperty, "SideButtonWidth");
                button.SetResourceReference(FontSizeProperty, "SmallFontSize");
                _sortButtons.Children.Add(button);
            }
            _sortByButton.Click += OnSortByClick;
            _sortOrderButton.Click += OnSortOrderClick;
        }

        /// <summary>The list's name and its number of games: "All the games (316)".</summary>
        public string Title => _count < 0 ? _title : Texts.Format("list.titleCount", _title, _count);

        public UIElement? SideContent => _filters;

        public UIElement? TitleContent => _sortButtons;

        /// <summary>Reads the games again: a change made on a game's page shows when coming back.</summary>
        public void OnShown()
        {
            UpdateFilterButtons();
            ShowGames();
            if (_searchMode)
            {
                // Ready for Prunel's keyboard or Dragon, without having to aim at the field.
                Dispatcher.BeginInvoke(new Action(() => System.Windows.Input.Keyboard.Focus(SearchBox)),
                    System.Windows.Threading.DispatcherPriority.Input);
            }
        }

        private void OnSearchChanged(object sender, TextChangedEventArgs e)
        {
            _filter.Search = SearchBox.Text;
            ShowGames();
            Scroll.ScrollToTop();
        }

        /// <summary>Reads the games of the filter, shows them and their number in the title.</summary>
        private void ShowGames()
        {
            List<Game> games = App.Database.GetGames(_filter);
            _shown = games;
            _addCategoryButton.Content = Texts.Format("batch.addCategory", games.Count);
            _addCategoryButton.IsEnabled = games.Count > 0;
            Games.ItemsSource = games;
            EmptyText.Visibility = games.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            _count = games.Count;
            MainWindow.Current.RefreshTitle();
        }

        // ----- Batch editing -----

        /// <summary>
        /// Gives one or more categories to all the games shown (for example every game with the
        /// tag "FPS": open the tag, filter if needed, then this button). Nothing is taken away,
        /// except "To sort (Steam)" once a real category is given.
        /// </summary>
        private void OnAddCategoryClick(object sender, RoutedEventArgs e)
        {
            List<Game> games = _shown;
            if (games.Count == 0)
            {
                return;
            }
            string toSort = Texts.Get("steamSync.newCategory");
            List<string> names = App.Database.GetCategories().Select(c => c.Name).Where(n => n != toSort).ToList();
            ChoiceMenu.ShowSeveral(Texts.Format("batch.title", games.Count), names, new List<string>(), chosen =>
            {
                chosen.Remove(toSort);
                if (chosen.Count == 0)
                {
                    return;
                }
                App.Database.AddCategories(games.Select(g => g.Id), chosen, toSort);
                ApplyFilter();
            });
        }

        // ----- Sort -----

        private void OnSortByClick(object sender, RoutedEventArgs e)
        {
            List<MenuChoice> choices = new List<MenuChoice>();
            foreach (SortBy sortBy in new[] { SortBy.Name, SortBy.Added, SortBy.Rating, SortBy.Compatibility })
            {
                SortBy chosen = sortBy;
                choices.Add(new MenuChoice(SortName(chosen), chosen == _filter.SortBy, () =>
                {
                    if (chosen != _filter.SortBy)
                    {
                        // A new criterion starts in its natural direction: A to Z, the newest,
                        // the best rated, the most compatible first.
                        _filter.SortBy = chosen;
                        _filter.Descending = chosen != SortBy.Name;
                    }
                }));
            }
            ShowMenu("sort.title", choices);
        }

        private void OnSortOrderClick(object sender, RoutedEventArgs e)
        {
            _filter.Descending = !_filter.Descending;
            ApplyFilter();
        }

        private static string SortName(SortBy sortBy) => Texts.Get(sortBy switch
        {
            SortBy.Added => "sort.added",
            SortBy.Rating => "sort.rating",
            SortBy.Compatibility => "sort.compatibility",
            _ => "sort.name",
        });

        /// <summary>The direction in words fitting the criterion: "A → Z", "Newest first"…</summary>
        private string SortOrderText() => Texts.Get((_filter.SortBy, _filter.Descending) switch
        {
            (SortBy.Name, false) => "sort.name.ascending",
            (SortBy.Name, true) => "sort.name.descending",
            (SortBy.Added, false) => "sort.added.ascending",
            (SortBy.Added, true) => "sort.added.descending",
            (SortBy.Rating, false) => "sort.rating.ascending",
            (SortBy.Rating, true) => "sort.rating.descending",
            (_, false) => "sort.compatibility.ascending",
            _ => "sort.compatibility.descending",
        });

        private void OnClearSearchClick(object sender, RoutedEventArgs e)
        {
            SearchBox.Text = "";
            System.Windows.Input.Keyboard.Focus(SearchBox);
        }

        public void OnLeaving()
        {
            ChoiceMenu.Hide();
        }

        private void OnGameClick(object sender, RoutedEventArgs e)
        {
            if (((FrameworkElement)sender).DataContext is Game game)
            {
                MainWindow.Current.Navigate(new GameDetailsPage(game.Id));
            }
        }

        // ----- Filters -----

        private void OnInstalledFilterClick(object sender, RoutedEventArgs e)
        {
            _filter.Installed = Next(YesNoSteps, _filter.Installed);
            ApplyFilter();
        }

        private void OnOwnedFilterClick(object sender, RoutedEventArgs e)
        {
            _filter.OwnedOnSteam = Next(YesNoSteps, _filter.OwnedOnSteam);
            ApplyFilter();
        }

        private void OnTriedFilterClick(object sender, RoutedEventArgs e)
        {
            _filter.Tried = Next(YesNoSteps, _filter.Tried);
            ApplyFilter();
        }

        private void OnCompatibilityFilterClick(object sender, RoutedEventArgs e)
        {
            ShowCompatibilityMenu();
        }

        /// <summary>
        /// The category and compatibility filters have too many values for a loop: each opens
        /// a menu of big buttons over the list (Ui/ChoiceMenu.cs), the current value in blue.
        /// </summary>
        private void ShowCompatibilityMenu()
        {
            List<MenuChoice> choices = new List<MenuChoice>();
            foreach (Compatibility? level in CompatibilityChoices)
            {
                object content = Texts.Get("filter.compatibility.all");
                if (level is Compatibility shown)
                {
                    // The name with its gauge under it, as on the tiles.
                    content = new StackPanel
                    {
                        Children =
                        {
                            new TextBlock { Text = CompatibilityTexts.Name(shown), TextWrapping = TextWrapping.Wrap, TextAlignment = TextAlignment.Center },
                            new CompatibilityBar { Level = shown, Height = 16, Margin = new Thickness(0, 10, 0, 0) },
                        },
                    };
                }
                choices.Add(new MenuChoice(content, level == _filter.ExactCompatibility, () => _filter.ExactCompatibility = level));
            }
            ShowMenu("filter.compatibility.title", choices);
        }

        private void OnCategoryFilterClick(object sender, RoutedEventArgs e)
        {
            List<MenuChoice> choices = new List<MenuChoice>
            {
                new MenuChoice(Texts.Get("filter.category.all"), _filter.Category == null, () => _filter.Category = null),
            };
            foreach (NamedCount category in App.Database.GetCategories())
            {
                string name = category.Name;
                choices.Add(new MenuChoice(name, name == _filter.Category, () => _filter.Category = name));
            }
            ShowMenu("filter.category.title", choices);
        }

        private void ShowMenu(string titleKey, List<MenuChoice> choices) =>
            ChoiceMenu.Show(Texts.Get(titleKey), choices, ApplyFilter);

        private void OnRatingFilterClick(object sender, RoutedEventArgs e)
        {
            _filter.MinRating = Next(RatingSteps, _filter.MinRating);
            ApplyFilter();
        }

        private void ApplyFilter()
        {
            OnShown();
            Scroll.ScrollToTop();
        }

        private void UpdateFilterButtons()
        {
            _installedFilter.Content = Texts.Get(_filter.Installed switch
            {
                true => "filter.installed.only",
                false => "filter.installed.no",
                null => "filter.installed.all",
            });
            _installedFilter.IsSelected = _filter.Installed.HasValue;

            _ownedFilter.Content = Texts.Get(_filter.OwnedOnSteam switch
            {
                true => "filter.owned.yes",
                false => "filter.owned.no",
                null => "filter.owned.all",
            });
            _ownedFilter.IsSelected = _filter.OwnedOnSteam.HasValue;

            _triedFilter.Content = Texts.Get(_filter.Tried switch
            {
                true => "filter.tried.yes",
                false => "filter.tried.no",
                null => "filter.tried.all",
            });
            _triedFilter.IsSelected = _filter.Tried.HasValue;

            _categoryFilter.Content = _filter.Category ?? Texts.Get("filter.category.all");
            _categoryFilter.IsSelected = _filter.Category != null;

            _compatibilityFilter.Content = _filter.ExactCompatibility is Compatibility level
                ? Texts.Format("filter.compatibility.is", CompatibilityTexts.Name(level))
                : Texts.Get("filter.compatibility.all");
            _compatibilityFilter.IsSelected = _filter.ExactCompatibility.HasValue;

            _ratingFilter.Content = _filter.MinRating is int rating
                ? Texts.Format(rating == 5 ? "filter.rating.five" : "filter.rating.atLeast", rating)
                : Texts.Get("filter.rating.all");
            _ratingFilter.IsSelected = _filter.MinRating.HasValue;

            _sortByButton.Content = Texts.Format("sort.by", SortName(_filter.SortBy));
            _sortOrderButton.Content = SortOrderText();
        }

        /// <summary>The value after the current one in a list of steps, back to the first after the last.</summary>
        private static T Next<T>(T[] steps, T current)
        {
            int index = System.Array.IndexOf(steps, current);
            return steps[(index + 1) % steps.Length];
        }
    }

    /// <summary>The names of the compatibility levels, in the interface language.</summary>
    internal static class CompatibilityTexts
    {
        public static string Name(Compatibility level) => Texts.Get(level switch
        {
            Compatibility.None => "compatibility.none",
            Compatibility.PartialWithSwitch => "compatibility.partialSwitch",
            Compatibility.PartialGaze => "compatibility.partialGaze",
            Compatibility.Full => "compatibility.full",
            Compatibility.Presumed => "compatibility.presumed",
            _ => "compatibility.unknown",
        });
    }
}

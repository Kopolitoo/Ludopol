using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;

namespace Ludopol
{
    /// <summary>
    /// VIEW: a list of games (a category, a tag, a developer, recently added, or all).
    ///
    /// The filter buttons have no menu: each look on a button moves to its next value,
    /// in a loop. For example "Rating: all" → "3 or more" → "4 or more" → "5" → "all".
    /// A filter in use is shown in blue.
    /// </summary>
    public partial class GameListPage : UserControl, IPage
    {
        private static readonly Compatibility?[] CompatibilitySteps =
            { null, Compatibility.PartialWithSwitch, Compatibility.PartialGaze, Compatibility.Full };
        private static readonly int?[] RatingSteps = { null, 3, 4, 5 };
        private static readonly bool?[] TriedSteps = { null, true, false };

        private readonly GameFilter _filter;
        private readonly string _title;

        public GameListPage(GameFilter filter, string title)
        {
            InitializeComponent();
            _filter = filter;
            _title = title;
        }

        public string Title => _title;

        /// <summary>Reads the games again: a change made on a game's page shows when coming back.</summary>
        public void OnShown()
        {
            UpdateFilterButtons();
            List<Game> games = App.Database.GetGames(_filter);
            Games.ItemsSource = games;
            EmptyText.Visibility = games.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        public void OnLeaving() { }

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
            _filter.InstalledOnly = !_filter.InstalledOnly;
            ApplyFilter();
        }

        private void OnTriedFilterClick(object sender, RoutedEventArgs e)
        {
            _filter.Tried = Next(TriedSteps, _filter.Tried);
            ApplyFilter();
        }

        private void OnCompatibilityFilterClick(object sender, RoutedEventArgs e)
        {
            _filter.MinCompatibility = Next(CompatibilitySteps, _filter.MinCompatibility);
            ApplyFilter();
        }

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
            InstalledFilter.Content = Texts.Get(_filter.InstalledOnly ? "filter.installed.only" : "filter.installed.all");
            InstalledFilter.IsSelected = _filter.InstalledOnly;

            TriedFilter.Content = Texts.Get(_filter.Tried switch
            {
                true => "filter.tried.yes",
                false => "filter.tried.no",
                null => "filter.tried.all",
            });
            TriedFilter.IsSelected = _filter.Tried.HasValue;

            CompatibilityFilter.Content = _filter.MinCompatibility is Compatibility minimum
                ? Texts.Format("filter.compatibility.atLeast", CompatibilityTexts.Name(minimum))
                : Texts.Get("filter.compatibility.all");
            CompatibilityFilter.IsSelected = _filter.MinCompatibility.HasValue;

            RatingFilter.Content = _filter.MinRating is int rating
                ? Texts.Format(rating == 5 ? "filter.rating.five" : "filter.rating.atLeast", rating)
                : Texts.Get("filter.rating.all");
            RatingFilter.IsSelected = _filter.MinRating.HasValue;
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
            _ => "compatibility.unknown",
        });
    }
}

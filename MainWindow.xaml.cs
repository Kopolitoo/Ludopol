using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;

namespace Ludopol
{
    /// <summary>
    /// A page of Ludopol (Home, a list, a game…). The main window asks it for its title and
    /// tells it when it is shown again or left.
    /// </summary>
    internal interface IPage
    {
        string Title { get; }

        /// <summary>The page's own buttons, shown in a column on the left (null: none).</summary>
        UIElement? SideContent { get; }

        /// <summary>Called each time the page is shown, including when coming back to it.</summary>
        void OnShown();

        /// <summary>Called when another page replaces it (to save, for example).</summary>
        void OnLeaving();
    }

    /// <summary>
    /// VIEW: the main window, and the NAVIGATION between pages.
    ///
    /// The pages already visited are kept in a pile (history): Navigate puts a new page on
    /// top, "Back" takes it off and shows the previous one, as it was left.
    /// </summary>
    public partial class MainWindow : Window
    {
        private readonly Stack<UserControl> _history = new Stack<UserControl>();
        private UserControl? _current;

        /// <summary>The window, for the pages that open another page.</summary>
        public static MainWindow Current { get; private set; } = null!;

        public MainWindow()
        {
            InitializeComponent();
            Current = this;
            ShowPage(new HomePage());
        }

        /// <summary>Opens a new page; "Back" will come back to the current one.</summary>
        public void Navigate(UserControl page)
        {
            if (_current != null)
            {
                (_current as IPage)?.OnLeaving();
                _history.Push(_current);
            }
            ShowPage(page);
        }

        /// <summary>Goes back to the previous page.</summary>
        public void GoBack()
        {
            if (_history.Count == 0)
            {
                return;
            }
            (_current as IPage)?.OnLeaving();
            ShowPage(_history.Pop());
        }

        private void ShowPage(UserControl page)
        {
            _current = page;
            PageHost.Content = page;
            bool atHome = _history.Count == 0;
            BackButton.Visibility = atHome ? Visibility.Hidden : Visibility.Visible;
            HomeButton.Visibility = atHome ? Visibility.Hidden : Visibility.Visible;
            (page as IPage)?.OnShown();
            // Read after OnShown: a game's page knows its title once the game is read.
            TitleText.Text = (page as IPage)?.Title ?? "";
            SideHost.Content = (page as IPage)?.SideContent;
        }

        /// <summary>Goes straight back to the home page (the first page of the history).</summary>
        public void GoHome()
        {
            if (_history.Count == 0)
            {
                return;
            }
            (_current as IPage)?.OnLeaving();
            UserControl home = _history.ToArray()[_history.Count - 1];
            _history.Clear();
            ShowPage(home);
        }

        private void OnBackClick(object sender, RoutedEventArgs e) => GoBack();

        private void OnHomeClick(object sender, RoutedEventArgs e) => GoHome();

        private void OnSettingsClick(object sender, RoutedEventArgs e)
        {
            if (_current is not SettingsPage)
            {
                Navigate(new SettingsPage());
            }
        }

        private void OnQuitClick(object sender, RoutedEventArgs e) => QuitPanel.Visibility = Visibility.Visible;

        private void OnQuitCancelClick(object sender, RoutedEventArgs e) => QuitPanel.Visibility = Visibility.Collapsed;

        private void OnQuitConfirmClick(object sender, RoutedEventArgs e)
        {
            (_current as IPage)?.OnLeaving();
            Close();
        }
    }
}

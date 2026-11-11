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

        /// <summary>The page's buttons shown on the right of its title (null: none).</summary>
        UIElement? TitleContent => null;

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
            ApplyWindowMode();
            ShowPage(new HomePage());
            PreviewKeyDown += OnPreviewKeyDown;
        }

        /// <summary>
        /// Keyboard shortcuts (in a text field, only Page Up / Page Down):
        /// Backspace = "Back"; Page Up / Page Down, Home / End, arrows = scroll the page
        /// (see ScrollPanel.HandleKey).
        /// </summary>
        private void OnPreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            // In a text field (the search), the keys write and move the cursor; only
            // Page Up / Page Down still scroll the page.
            // A Windows Forms field (the notes for Dragon) keeps every key.
            if (System.Windows.Input.Keyboard.FocusedElement is System.Windows.Forms.Integration.WindowsFormsHost)
            {
                return;
            }
            if (System.Windows.Input.Keyboard.FocusedElement is TextBox
                && e.Key != System.Windows.Input.Key.PageUp && e.Key != System.Windows.Input.Key.PageDown)
            {
                return;
            }
            if (e.Key == System.Windows.Input.Key.Back)
            {
                GoBack();
                e.Handled = true;
                return;
            }
            ScrollPanel? scroll = FindScrollPanel(PageHost);
            if (scroll != null && scroll.HandleKey(e.Key))
            {
                e.Handled = true;
            }
        }

        /// <summary>The first scrolling area shown in an element (the current page), or null.</summary>
        private static ScrollPanel? FindScrollPanel(DependencyObject parent)
        {
            int count = System.Windows.Media.VisualTreeHelper.GetChildrenCount(parent);
            for (int i = 0; i < count; i++)
            {
                DependencyObject child = System.Windows.Media.VisualTreeHelper.GetChild(parent, i);
                if (child is ScrollPanel panel && panel.IsVisible)
                {
                    return panel;
                }
                if (child is UIElement element && !element.IsVisible)
                {
                    continue;
                }
                ScrollPanel? inner = FindScrollPanel(child);
                if (inner != null)
                {
                    return inner;
                }
            }
            return null;
        }

        /// <summary>
        /// Full screen (no frame, over the taskbar) or an ordinary window, according to the
        /// FullScreen setting. Called at startup and when the setting changes.
        /// </summary>
        public void ApplyWindowMode()
        {
            // The frame must be changed while the window is not maximised, otherwise Windows
            // keeps the taskbar visible in full screen.
            WindowState = WindowState.Normal;
            if (AppSettings.Current.FullScreen)
            {
                WindowStyle = WindowStyle.None;
                ResizeMode = ResizeMode.NoResize;
                WindowState = WindowState.Maximized;
            }
            else
            {
                WindowStyle = WindowStyle.SingleBorderWindow;
                ResizeMode = ResizeMode.CanResize;
            }
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
            TitleExtras.Content = (page as IPage)?.TitleContent;
        }

        /// <summary>Shows the current page's title again (a list whose number of games changed).</summary>
        public void RefreshTitle()
        {
            TitleText.Text = (_current as IPage)?.Title ?? "";
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
            if (_current is not SettingsPage { IsMenu: true })
            {
                Navigate(new SettingsPage());
            }
        }

        private void OnQuitClick(object sender, RoutedEventArgs e) => QuitPanel.Visibility = Visibility.Visible;

        private void OnQuitCancelClick(object sender, RoutedEventArgs e) => QuitPanel.Visibility = Visibility.Collapsed;

        private void OnQuitConfirmClick(object sender, RoutedEventArgs e) => Close();

        /// <summary>
        /// Before closing ("Quit", or the window's cross in windowed mode): the current page
        /// saves what it must (the notes of a game's page).
        /// </summary>
        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            (_current as IPage)?.OnLeaving();
            base.OnClosing(e);
        }
    }
}

using System.Windows;
using System.Windows.Controls;

namespace Ludopol
{
    /// <summary>VIEW: the home page. Each tile opens a way to browse the catalogue.</summary>
    public partial class HomePage : UserControl, IPage
    {
        public HomePage()
        {
            InitializeComponent();
        }

        public string Title => Texts.Get("home.title");

        public UIElement? SideContent => null;

        public void OnShown() { }

        public void OnLeaving() { }

        private void OnCategoriesClick(object sender, RoutedEventArgs e) =>
            MainWindow.Current.Navigate(new ChoicePage(ChoiceKind.Categories));

        private void OnTagsClick(object sender, RoutedEventArgs e) =>
            MainWindow.Current.Navigate(new ChoicePage(ChoiceKind.Tags));

        private void OnDevelopersClick(object sender, RoutedEventArgs e) =>
            MainWindow.Current.Navigate(new ChoicePage(ChoiceKind.Developers));

        private void OnRecentClick(object sender, RoutedEventArgs e) =>
            MainWindow.Current.Navigate(new GameListPage(new GameFilter { NewestFirst = true }, Texts.Get("home.recent")));

        private void OnAllClick(object sender, RoutedEventArgs e) =>
            MainWindow.Current.Navigate(new GameListPage(new GameFilter(), Texts.Get("home.all")));

        private void OnSearchClick(object sender, RoutedEventArgs e) =>
            MainWindow.Current.Navigate(new GameListPage(new GameFilter(), Texts.Get("home.search"), searchMode: true));
    }
}

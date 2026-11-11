using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace Ludopol
{
    /// <summary>What a ChoicePage lists.</summary>
    public enum ChoiceKind
    {
        Categories,
        Tags,
        Developers,
    }

    /// <summary>
    /// VIEW: the list of categories, tags or developers. Choosing one opens its games.
    /// The list is read again each time the page is shown, so it stays up to date.
    /// </summary>
    public partial class ChoicePage : UserControl, IPage
    {
        private readonly ChoiceKind _kind;

        public ChoicePage(ChoiceKind kind)
        {
            InitializeComponent();
            _kind = kind;
        }

        public string Title => Texts.Get(_kind switch
        {
            ChoiceKind.Tags => "home.tags",
            ChoiceKind.Developers => "home.developers",
            _ => "home.categories",
        });

        public void OnShown()
        {
            List<NamedCount> names = _kind switch
            {
                ChoiceKind.Tags => App.Database.GetTags(),
                ChoiceKind.Developers => App.Database.GetDevelopers(),
                _ => App.Database.GetCategories(),
            };
            Items.ItemsSource = names.Select(n => new ChoiceItem(n)).ToList();
        }

        public void OnLeaving() { }

        private void OnItemClick(object sender, RoutedEventArgs e)
        {
            if (((FrameworkElement)sender).DataContext is not ChoiceItem item)
            {
                return;
            }
            GameFilter filter = new GameFilter();
            switch (_kind)
            {
                case ChoiceKind.Tags: filter.Tag = item.Name; break;
                case ChoiceKind.Developers: filter.Developer = item.Name; break;
                default: filter.Category = item.Name; break;
            }
            MainWindow.Current.Navigate(new GameListPage(filter, item.Name));
        }
    }

    /// <summary>One tile: the name and "3 games".</summary>
    public sealed class ChoiceItem
    {
        public ChoiceItem(NamedCount source)
        {
            Name = source.Name;
            CountText = Texts.Format(source.Count == 1 ? "game.count.one" : "game.count.many", source.Count);
        }

        public string Name { get; }
        public string CountText { get; }
    }
}

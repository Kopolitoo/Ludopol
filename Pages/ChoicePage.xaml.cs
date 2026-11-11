using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

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
            Items.ItemTemplate = (DataTemplate)Resources[kind == ChoiceKind.Categories ? "CardTemplate" : "TextTemplate"];
        }

        public string Title => Texts.Get(_kind switch
        {
            ChoiceKind.Tags => "home.tags",
            ChoiceKind.Developers => "home.developers",
            _ => "home.categories",
        });

        public UIElement? SideContent => null;

        public void OnShown()
        {
            List<NamedCount> names = _kind switch
            {
                ChoiceKind.Tags => App.Database.GetTags(),
                ChoiceKind.Developers => App.Database.GetDevelopers(),
                _ => App.Database.GetCategories(),
            };
            Items.ItemsSource = names.Select(n => new ChoiceItem(n, _kind == ChoiceKind.Categories)).ToList();
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

    /// <summary>
    /// One tile or card: the name, "3 games", and for a category its picture.
    ///
    /// A category's picture is, in this order:
    ///   1. my own picture, if there is one: a file named like the category in
    ///      %LocalAppData%\Ludopol\Categories (.png, .jpg or .jpeg). Characters not allowed in a
    ///      file name are replaced by "_": "Romans visuels / Narratif" → "Romans visuels _ Narratif.png";
    ///   2. otherwise the image of one of its games (see Database.GetCategories);
    ///   3. otherwise a coloured background with the initials.
    /// </summary>
    public sealed class ChoiceItem
    {
        /// <summary>Muted background colours for the cards without a picture.</summary>
        private static readonly Brush[] Palette =
        {
            MakeBrush(0x3B, 0x5B, 0x8C), MakeBrush(0x6B, 0x4C, 0x8C), MakeBrush(0x2E, 0x7A, 0x6B),
            MakeBrush(0x8C, 0x5A, 0x3B), MakeBrush(0x7A, 0x3B, 0x5B), MakeBrush(0x4C, 0x6B, 0x2E),
            MakeBrush(0x3B, 0x7A, 0x8C), MakeBrush(0x8C, 0x3B, 0x3B),
        };

        public ChoiceItem(NamedCount source, bool withPicture)
        {
            Name = source.Name;
            CountText = Texts.Format(source.Count == 1 ? "game.count.one" : "game.count.many", source.Count);
            if (withPicture)
            {
                ImageSource = OwnPicture(source.Name) ?? source.ImageUrl;
            }
        }

        public string Name { get; }
        public string CountText { get; }

        /// <summary>A file path or a web address, or null.</summary>
        public string? ImageSource { get; }

        public string Initials
        {
            get
            {
                string[] words = Name.Split(new[] { ' ', '/', '-' }, StringSplitOptions.RemoveEmptyEntries);
                return string.Concat(words.Take(2).Select(w => char.ToUpperInvariant(w[0])));
            }
        }

        /// <summary>Always the same colour for the same name.</summary>
        public Brush PlaceholderBrush
        {
            get
            {
                int sum = Name.Sum(c => (int)c);
                return Palette[sum % Palette.Length];
            }
        }

        private static string? OwnPicture(string name)
        {
            string fileName = string.Concat(name.Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c));
            string folder = Path.Combine(App.DataFolder, "Categories");
            foreach (string extension in new[] { ".png", ".jpg", ".jpeg" })
            {
                string path = Path.Combine(folder, fileName + extension);
                if (File.Exists(path))
                {
                    return path;
                }
            }
            return null;
        }

        private static Brush MakeBrush(byte r, byte g, byte b)
        {
            SolidColorBrush brush = new SolidColorBrush(Color.FromRgb(r, g, b));
            brush.Freeze();
            return brush;
        }
    }
}

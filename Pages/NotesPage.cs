using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms.Integration;
using System.Windows.Media;

namespace Ludopol
{
    /// <summary>
    /// VIEW (TRIAL): my notes on a game, written in a real Windows text field so that Dragon
    /// can dictate into it directly ("Full-Text Control").
    ///
    /// WPF draws its own text fields, which Dragon does not recognise (it then opens its
    /// dictation box). A Windows Forms TextBox is a standard Windows "Edit" control, the kind
    /// Dragon knows (as in Notepad). WindowsFormsHost puts it in the WPF page.
    ///
    /// The field fills the whole page: a Windows control is always drawn above WPF content,
    /// so it must not sit under a menu or in a scrolling area. "Back" saves the notes.
    /// </summary>
    public sealed class NotesPage : UserControl, IPage
    {
        private readonly long _gameId;
        private readonly System.Windows.Forms.TextBox _box;
        private Game? _game;

        public NotesPage(long gameId)
        {
            _gameId = gameId;
            _box = new System.Windows.Forms.TextBox
            {
                Multiline = true,
                AcceptsReturn = true,
                WordWrap = true,
                ScrollBars = System.Windows.Forms.ScrollBars.Vertical,
                BorderStyle = System.Windows.Forms.BorderStyle.None,
            };
            Border frame = new Border
            {
                Margin = new Thickness(12),
                Padding = new Thickness(12),
                CornerRadius = new CornerRadius(10),
                Child = new WindowsFormsHost { Child = _box },
            };
            frame.SetResourceReference(Border.BackgroundProperty, "SurfaceBrush");
            Content = frame;
        }

        public string Title => _game == null ? "" : Texts.Format("notes.title", _game.Name);

        public UIElement? SideContent => null;

        public void OnShown()
        {
            _game = App.Database.GetGame(_gameId);
            if (_game == null)
            {
                MainWindow.Current.GoBack();
                return;
            }
            // Windows Forms wants "\r\n" between lines.
            _box.Text = (_game.Notes ?? "").Replace("\r\n", "\n").Replace("\n", "\r\n");
            ApplyLook();
            // Ready for Dragon or Prunel's keyboard, the cursor at the end.
            Dispatcher.BeginInvoke(new Action(() =>
            {
                _box.Focus();
                _box.SelectionStart = _box.TextLength;
            }), System.Windows.Threading.DispatcherPriority.Input);
        }

        public void OnLeaving()
        {
            if (_game == null)
            {
                return;
            }
            string? notes = string.IsNullOrWhiteSpace(_box.Text) ? null : _box.Text.Replace("\r\n", "\n").Trim();
            if (notes != _game.Notes)
            {
                _game.Notes = notes;
                App.Database.SaveUserFields(_game);
                App.Log("Notes saved for \"" + _game.Name + "\" (Windows field)");
            }
        }

        /// <summary>The theme's colours and the interface's text size, read again each time (they can change).</summary>
        private void ApplyLook()
        {
            _box.BackColor = ToDrawing(FindResource("SurfaceBrush"), System.Drawing.Color.White);
            _box.ForeColor = ToDrawing(FindResource("TextBrush"), System.Drawing.Color.Black);
            // WPF sizes are 1/96 inch, Windows Forms font sizes are points (1/72 inch).
            double size = FindResource("BaseFontSize") is double pixels ? pixels * 72 / 96 : 18;
            _box.Font = new System.Drawing.Font("Segoe UI", (float)size);
        }

        private static System.Drawing.Color ToDrawing(object resource, System.Drawing.Color fallback) =>
            resource is SolidColorBrush brush
                ? System.Drawing.Color.FromArgb(brush.Color.A, brush.Color.R, brush.Color.G, brush.Color.B)
                : fallback;
    }
}

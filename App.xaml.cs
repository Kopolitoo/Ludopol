using System;
using System.IO;
using System.Windows;
using System.Windows.Threading;

namespace Ludopol
{
    /// <summary>
    /// The application: starts everything, in this order:
    ///   1. reads the settings (settings.json, see AppSettings);
    ///   2. loads the interface texts in the chosen language (Languages/*.json, see Texts);
    ///   3. opens the database (creates it with two fictitious games the first time);
    ///   4. checks which games are installed (GameLauncher.cs);
    ///   5. shows the main window.
    ///
    /// Ludopol's own files live in %LocalAppData%\Ludopol (DataFolder):
    /// settings.json, ludopol.db (the catalogue) and ludopol.log (what happened, for debugging).
    /// </summary>
    public partial class App : Application
    {
        /// <summary>The folder of Ludopol's own files: %LocalAppData%\Ludopol.</summary>
        public static string DataFolder { get; } = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Ludopol");

        /// <summary>The catalogue, opened at startup.</summary>
        internal static Database Database { get; private set; } = null!;

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            Directory.CreateDirectory(DataFolder);
            DispatcherUnhandledException += OnUnhandledException;
            Log("Ludopol starting");

            AppSettings.Load();
            AppSettings.Current.ApplyTo(Resources);
            Texts.Load(AppSettings.Current.Language);

            Database = new Database(Path.Combine(DataFolder, "ludopol.db"));
            Database.Open();

            // Which games are installed: Steam's libraries and the programs chosen in Ludopol.
            try
            {
                GameLauncher.RefreshAll(Database);
            }
            catch (Exception ex)
            {
                Log("Installed games not checked: " + ex);
            }

            // "Ludopol." is needed here: inside App, "MainWindow" alone means Application.MainWindow.
            Ludopol.MainWindow window = new Ludopol.MainWindow();
            MainWindow = window;
            window.Show();
        }

        /// <summary>Adds a dated line to ludopol.log. Never throws.</summary>
        public static void Log(string message)
        {
            try
            {
                File.AppendAllText(Path.Combine(DataFolder, "ludopol.log"),
                    DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "  " + message + Environment.NewLine);
            }
            catch (Exception)
            {
                // Logging must never stop the application.
            }
        }

        /// <summary>
        /// An unexpected error: it is logged and shown, and Ludopol keeps running
        /// instead of closing without a word.
        /// </summary>
        private void OnUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            Log("Unexpected error: " + e.Exception);
            MessageBox.Show(Texts.Format("error.unexpected", e.Exception.Message), "Ludopol");
            e.Handled = true;
        }
    }
}

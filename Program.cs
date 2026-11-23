using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using Timer = System.Windows.Forms.Timer;

namespace CursorSeam
{
    internal static class Program
    {
        [DllImport("user32.dll")] private static extern bool SetProcessDpiAwarenessContext(IntPtr context);

        [STAThread]
        private static void Main(string[] args)
        {
            if (args.Contains(InstanceWindow.CloseForUpdateArgument))
            {
                Environment.ExitCode = InstanceWindow.RequestCloseForUpdate(!args.Contains(InstanceWindow.SilentUpdateArgument)) ? 0 : 1;
                return;
            }
            bool startInTray = args.Any(arg => string.Equals(arg, StartupRegistration.Argument, StringComparison.OrdinalIgnoreCase));
            bool created;
            using (var single = new Mutex(true, InstanceWindow.MutexName, out created))
            {
                if (!created)
                {
                    if (!startInTray) InstanceWindow.RequestRestore();
                    return;
                }
                try
                {
                    SetProcessDpiAwarenessContext(new IntPtr(-4));
                    Application.EnableVisualStyles();
                    Application.SetCompatibleTextRenderingDefault(false);
                    using (var window = new MainWindow(Preferences.FilePath)) RunWindow(window, startInTray);
                }
                finally { single.ReleaseMutex(); }
            }
        }

        internal static void RunWindow(MainWindow window, bool startInTray)
        {
            if (!startInTray)
            {
                Application.Run(window);
                return;
            }

            // A context with MainForm set would automatically make that form visible.
            using (var context = new ApplicationContext())
            {
                window.FormClosed += (sender, e) => context.ExitThread();
                window.StartInTray();
                Application.Run(context);
            }
        }
    }

    internal sealed class MainWindow : Form
    {
        private const uint StopKey = 0x79; // F10
        private const string StopShortcut = "Ctrl + Alt + F10";
        private const int MinimumWidth = 880, MinimumHeight = 640;
        private readonly LayoutView view = new LayoutView { Dock = DockStyle.Fill, Margin = new Padding(0) };
        private readonly Label selection = MakeLabel(), status = MakeLabel(), hint = MakeLabel(), correctionState = MakeLabel();
        private readonly BoardButton measure = new BoardButton("Calibrate", true), identify = new BoardButton("Identify", false),
            undo = new BoardButton("Undo", false), correction = new BoardButton("StopCorrection", false),
            reset = new BoardButton("RestoreDefaults", false), save = new BoardButton("SaveSettings", true),
            deleteSettings = new BoardButton("DeleteSettings", false), exportSettings = new BoardButton("ExportSettings", false),
            restoreSettings = new BoardButton("RestoreSettings", false);
        private readonly BoardToggle trayPreference = new BoardToggle(), startupPreference = new BoardToggle();
        private readonly ToolTip startupHelp = new ToolTip();
        private readonly Label languageLabel = MakeLabel();
        private readonly BoardLanguagePicker languageChoice = new BoardLanguagePicker();
        private readonly MouseRouter router = new MouseRouter();
        private readonly Timer refresh = new Timer { Interval = 350 };
        private readonly NotifyIcon tray;
        private readonly Icon windowIcon;
        private readonly Font windowFont, headingFont;
        private Icon trayIcon;
        private int trayIconSize;
        private readonly ToolStripMenuItem trayOpen, trayCorrection, trayExit;
        private readonly List<RulerOverlay> rulers = new List<RulerOverlay>();
        // Saved and current undo histories can share an unchanged tail.
        private sealed class Edit
        {
            internal readonly List<Display> Before;
            internal readonly Edit Previous;
            internal Edit(List<Display> before, Edit previous) { Before = before; Previous = previous; }
        }
        private Edit history, savedHistory;
        private readonly string layoutPath, settingsPath;
        private List<Display> displays;
        private List<Display> committed, saved, defaults;
        private bool hotkey, paused, exitRequested, displayChangePending, resettingSettings;
        private bool initialTrayLaunch, resourcesDisposed, closingForUpdate;
        private Layout routingToRestore;
        private IntPtr markedWindow;
        private string notice;

        [DllImport("user32.dll")] private static extern bool RegisterHotKey(IntPtr window, int id, uint modifiers, uint key);
        [DllImport("user32.dll")] private static extern bool UnregisterHotKey(IntPtr window, int id);
        [DllImport("user32.dll")] private static extern bool IsWindowEnabled(IntPtr window);

        internal MainWindow(string layoutPath)
        {
            BoardStyle.RefreshTheme();
            this.layoutPath = layoutPath;
            settingsPath = Path.ChangeExtension(layoutPath, ".settings.xml");
            AppPreferences preferences = Preferences.LoadAppPreferences(layoutPath, settingsPath);
            UiText.SelectLanguage(preferences.Language);
            Icon = windowIcon = AppIcons.Load(SystemInformation.IconSize.Width);
            Font = windowFont = BoardStyle.CreateFont(10f, FontStyle.Regular, GraphicsUnit.Point);
            BackColor = BoardStyle.Surface;
            ForeColor = BoardStyle.Ink;
            AutoScaleMode = AutoScaleMode.Dpi;
            Rectangle workArea = Screen.FromPoint(Cursor.Position).WorkingArea;
            StartPosition = FormStartPosition.Manual;
            MinimumSize = new Size(Math.Min(MinimumWidth, workArea.Width), Math.Min(MinimumHeight, workArea.Height));
            Bounds = InitialBounds(workArea);

            var chrome = new TableLayoutPanel { ColumnCount = 1, RowCount = 10, Dock = DockStyle.Fill, Padding = new Padding(22, 15, 22, 14) };
            chrome.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            for (int row = 0; row < chrome.RowCount; row++)
                chrome.RowStyles.Add(row == 2 ? new RowStyle(SizeType.Percent, 100) : new RowStyle(SizeType.AutoSize));
            var heading = MakeLabel();
            heading.Text = "CursorSeam";
            heading.Font = headingFont = new Font(Font.Name, 20, FontStyle.Bold);
            var header = new TableLayoutPanel { ColumnCount = 2, RowCount = 1, AutoSize = true, Dock = DockStyle.Fill, Margin = new Padding(0, 0, 0, 9) };
            header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            header.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            var correctionControls = new FlowLayoutPanel { AutoSize = true, Anchor = AnchorStyles.Right, WrapContents = false, Margin = new Padding(0) };
            correctionState.Margin = new Padding(0, 0, 10, 5);
            correctionControls.Controls.AddRange(new Control[] { correctionState, correction });
            header.Controls.Add(heading, 0, 0);
            header.Controls.Add(correctionControls, 1, 0);
            var editingTools = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, Margin = new Padding(0, 0, 0, 7) };
            editingTools.Controls.AddRange(new Control[] { identify, reset, undo });
            var finishingTools = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft, Margin = new Padding(0) };
            finishingTools.Controls.AddRange(new Control[] { save, measure });
            hint.ForeColor = BoardStyle.Muted;
            hint.Margin = new Padding(3, 6, 3, 1);
            selection.ForeColor = BoardStyle.Muted;
            selection.Margin = new Padding(4, 0, 3, 3);
            status.Margin = new Padding(3, 2, 3, 0);
            var toggles = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, Margin = new Padding(0) };
            var languageControls = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Dock = DockStyle.Fill, Margin = new Padding(0) };
            var settingsActions = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, Margin = new Padding(0) };
            trayPreference.Margin = new Padding(3, 0, 12, 4);
            startupPreference.Margin = trayPreference.Margin;
            languageLabel.Margin = new Padding(3, 0, 10, 4);
            languageChoice.Margin = new Padding(3, 0, 3, 4);
            toggles.Controls.AddRange(new Control[] { startupPreference, trayPreference });
            languageControls.Controls.AddRange(new Control[] { languageLabel, languageChoice });
            settingsActions.Controls.AddRange(new Control[] { exportSettings, restoreSettings, deleteSettings });
            chrome.Controls.Add(header, 0, 0);
            chrome.Controls.Add(editingTools, 0, 1);
            chrome.Controls.Add(view, 0, 2);
            chrome.Controls.Add(hint, 0, 3);
            chrome.Controls.Add(selection, 0, 4);
            chrome.Controls.Add(finishingTools, 0, 5);
            chrome.Controls.Add(toggles, 0, 6);
            chrome.Controls.Add(languageControls, 0, 7);
            chrome.Controls.Add(settingsActions, 0, 8);
            chrome.Controls.Add(status, 0, 9);
            chrome.SizeChanged += (sender, e) =>
            {
                int width = Math.Max(1, chrome.ClientSize.Width - chrome.Padding.Horizontal - 6);
                var limit = new Size(width, 0);
                hint.MaximumSize = status.MaximumSize = editingTools.MaximumSize = selection.MaximumSize = finishingTools.MaximumSize = toggles.MaximumSize = settingsActions.MaximumSize = limit;
            };
            Controls.Add(chrome);

            var menu = new ContextMenuStrip();
            trayOpen = (ToolStripMenuItem)menu.Items.Add(UiText.Get("TrayOpen"), null, (sender, e) => RestoreWindow());
            trayCorrection = (ToolStripMenuItem)menu.Items.Add(UiText.Get("StopCorrection"), null, (sender, e) => ToggleCorrection());
            trayExit = (ToolStripMenuItem)menu.Items.Add(UiText.Get("TrayExit"), null, (sender, e) => ExitApplication());
            trayPreference.Checked = preferences.TrayResident;
            tray = new NotifyIcon { Text = "CursorSeam", ContextMenuStrip = menu };
            RefreshTrayIcon();
            tray.Visible = trayPreference.Checked;
            trayPreference.CheckedChanged += (sender, e) => UpdateTrayPreference();
            startupPreference.Click += (sender, e) => SetStartupPreference();
            languageChoice.SelectionChanged += (sender, e) =>
            {
                UiText.SelectLanguage(languageChoice.SelectedLanguage);
                notice = null;
                ApplyLanguage();
                SaveAppPreferences();
            };
            tray.DoubleClick += (sender, e) => RestoreWindow();
            Resize += (sender, e) =>
            {
                if (WindowState == FormWindowState.Minimized)
                {
                    CloseRulers();
                    if (trayPreference.Checked) Hide();
                }
                else ShowRulers();
            };
            identify.Click += (sender, e) => Identify();
            measure.Click += (sender, e) => { if (view.ResizingEnabled) TryFinishCalibration(); else SetResizeMode(true); };
            undo.Click += (sender, e) => UndoEdit();
            reset.Click += (sender, e) => RestoreDefaults();
            save.Click += (sender, e) => SaveLayout(true);
            deleteSettings.Click += (sender, e) =>
            {
                using (var dialog = new BoardPrompt("DeleteSettings", "DeleteSettingsPrompt", "DeleteSettings", null, "Cancel"))
                    if (dialog.ShowDialog(this) == DialogResult.Yes) ResetSettings();
            };
            exportSettings.Click += (sender, e) => ExportSettings();
            restoreSettings.Click += (sender, e) => ImportSettings();
            correction.Click += (sender, e) => ToggleCorrection();
            view.GeometryChanged += UpdateRulers;
            view.StateChanged += UpdateControls;
            view.EditCommitted += CommitEdit;
            refresh.Tick += (sender, e) => { refresh.Stop(); RefreshDisplays(); };
            LoadDisplays(Windows.ReadDisplays());
#if STORE_BUILD
            if (StartupRegistration.HasPackageIdentity)
            {
                startupPreference.Enabled = false;
                Shown += async (sender, e) =>
                {
                    try
                    {
                        startupPreference.Checked = await StartupRegistration.IsPackageStartupEnabledAsync();
                        startupPreference.Enabled = true;
                    }
                    catch (ExternalException) { StartupReadFailed(); }
                    catch (UnauthorizedAccessException) { StartupReadFailed(); }
                    if (!IsDisposed) UpdateControls();
                };
            }
            else
#endif
                ReadStartupPreference();
            ApplyLanguage();
            ApplyTheme();
        }

        private void ApplyTheme()
        {
            BackColor = BoardStyle.Surface;
            ForeColor = BoardStyle.Ink;
            hint.ForeColor = selection.ForeColor = BoardStyle.Muted;
            view.ApplyTheme();
            BoardStyle.StyleMenu(tray.ContextMenuStrip);
            if (IsHandleCreated) BoardStyle.ApplyTitleBar(Handle);
            UpdateControls();
            PerformLayout();
            Invalidate(true);
        }

        protected override void OnShown(EventArgs e) { base.OnShown(e); ApplyLayout(); }

        internal void StartInTray()
        {
            initialTrayLaunch = true;
            tray.Visible = true;
            if (!IsHandleCreated) CreateHandle();
            BeginInvoke((Action)ApplyLayout);
        }

        internal static Rectangle InitialBounds(Rectangle workArea)
        {
            int width = Math.Min(workArea.Width, Math.Max(MinimumWidth, Math.Min(1800, workArea.Width * 74 / 100)));
            int height = Math.Min(workArea.Height, Math.Max(MinimumHeight, Math.Min(1120, workArea.Height * 72 / 100)));
            return new Rectangle(workArea.Left + (workArea.Width - width) / 2,
                                 workArea.Top + (workArea.Height - height) / 2, width, height);
        }

        private static Label MakeLabel() { return new Label { AutoSize = true, Anchor = AnchorStyles.Left }; }

        private void RestoreWindow()
        {
            Show();
            if (WindowState == FormWindowState.Minimized) WindowState = FormWindowState.Normal;
            Activate();
            InstanceWindow.SetForegroundWindow(Handle);
            initialTrayLaunch = false;
            tray.Visible = trayPreference.Checked;
            ShowRulers();
        }

        private void ExitApplication()
        {
            exitRequested = true;
            Close();
            exitRequested = false;
        }

        private bool CloseForUpdate(bool interactive)
        {
            if (closingForUpdate || !IsWindowEnabled(Handle)) return false;
            closingForUpdate = true;
            try
            {
                view.CancelDrag();
                if (!SameLayout(committed, saved))
                {
                    if (!interactive) return false;
                    RestoreWindow();
                    if (view.ResizingEnabled)
                    {
                        if (!TryFinishCalibration()) return false;
                    }
                    else if (!SaveLayout(false)) return false;
                }
                BeginInvoke((MethodInvoker)ExitApplication);
                return true;
            }
            finally { closingForUpdate = false; }
        }

        private void UpdateTrayPreference()
        {
            if (trayPreference.Checked) RefreshTrayIcon();
            tray.Visible = initialTrayLaunch || trayPreference.Checked;
            if (resettingSettings) return;
            SaveAppPreferences();
        }

        private void ReadStartupPreference()
        {
            try { startupPreference.Checked = StartupRegistration.IsEnabled(); }
            catch (IOException) { StartupReadFailed(); }
            catch (UnauthorizedAccessException) { StartupReadFailed(); }
            catch (System.Security.SecurityException) { StartupReadFailed(); }
        }

        private void StartupReadFailed()
        {
            startupPreference.Enabled = false;
            notice = UiText.Get("StartupReadError");
        }

        private void SetStartupPreference()
        {
#if STORE_BUILD
            if (StartupRegistration.HasPackageIdentity)
            {
                SetPackageStartupPreference();
                return;
            }
#endif
            bool requested = startupPreference.Checked;
            try
            {
                StartupRegistration.SetEnabled(requested, Application.ExecutablePath);
                notice = null;
            }
            catch (IOException) { StartupSaveFailed(requested); }
            catch (UnauthorizedAccessException) { StartupSaveFailed(requested); }
            catch (System.Security.SecurityException) { StartupSaveFailed(requested); }
            UpdateControls();
        }

#if STORE_BUILD
        private async void SetPackageStartupPreference()
        {
            bool requested = startupPreference.Checked;
            startupPreference.Enabled = false;
            try
            {
                bool enabled = await StartupRegistration.SetPackageStartupEnabledAsync(requested);
                if (IsDisposed) return;
                startupPreference.Checked = enabled;
                notice = enabled == requested ? null : UiText.Get("StartupSaveError");
            }
            catch (ExternalException) { if (!IsDisposed) StartupSaveFailed(requested); }
            catch (UnauthorizedAccessException) { if (!IsDisposed) StartupSaveFailed(requested); }
            finally
            {
                if (!IsDisposed)
                {
                    startupPreference.Enabled = true;
                    UpdateControls();
                }
            }
        }
#endif

        private void StartupSaveFailed(bool requested)
        {
            startupPreference.Checked = !requested;
            notice = UiText.Get("StartupSaveError");
        }

        private void SaveAppPreferences()
        {
            try { Preferences.UpdateAppPreferences(layoutPath, CurrentPreferences()); }
            catch (IOException) { notice = UiText.Get("PreferencesSaveError"); }
            catch (UnauthorizedAccessException) { notice = UiText.Get("PreferencesSaveError"); }
            UpdateControls();
        }

        private AppPreferences CurrentPreferences()
        {
            return new AppPreferences { TrayResident = trayPreference.Checked, Language = UiText.Preference };
        }

        private void ApplyLanguage()
        {
            Text = UiText.Get("WindowTitle");
            identify.Text = UiText.Get("Identify");
            undo.Text = UiText.Get("Undo");
            reset.Text = UiText.Get("RestoreDefaults");
            save.Text = UiText.Get("SaveSettings");
            deleteSettings.Text = UiText.Get("DeleteSettings");
            exportSettings.Text = UiText.Get("ExportSettings");
            restoreSettings.Text = UiText.Get("RestoreSettings");
            trayPreference.Text = UiText.Get("TrayPreference");
            startupPreference.Text = UiText.Get("AutoStart");
            startupPreference.AccessibleDescription = UiText.Get("AutoStartHelp");
            startupHelp.SetToolTip(startupPreference, startupPreference.AccessibleDescription);
            trayOpen.Text = UiText.Get("TrayOpen");
            trayExit.Text = UiText.Get("TrayExit");
            view.AccessibleName = UiText.Get("LayoutAccessibleName");
            view.Invalidate();
            foreach (RulerOverlay ruler in rulers) ruler.Invalidate();

            languageLabel.Text = languageChoice.AccessibleName = UiText.Get("Language");
            var choices = new[] { new KeyValuePair<string, string>("", UiText.Get("SystemLanguage")) }.Concat(UiText.Languages).ToArray();
            languageChoice.SetChoices(choices, UiText.Preference);
            UpdateControls();
        }

        private void RefreshTrayIcon()
        {
            int size = AppIcons.TraySize();
            if (size == trayIconSize) return;
            Icon replacement = AppIcons.Load(size);
            tray.Icon = replacement;
            if (trayIcon != null) trayIcon.Dispose();
            trayIcon = replacement;
            trayIconSize = size;
        }

        private void RefreshDisplays()
        {
            RefreshTrayIcon();
            var detected = Windows.ReadDisplays();
            bool changed = detected.Count != displays.Count ||
                detected.Any(d => !displays.Any(existing => existing.Id == d.Id && existing.Pixels == d.Pixels));
            displayChangePending = changed && view.ResizingEnabled;
            if (changed && !view.ResizingEnabled) LoadDisplays(detected);
            ShowRulers();
            ApplyLayout();
        }

        private void LoadDisplays(List<Display> detected)
        {
            history = savedHistory = null;
            defaults = CopyDisplays(detected);
            var current = Preferences.Load(layoutPath, detected);
            saved = committed = CopyDisplays(current);
            notice = null;
            SetDisplays(current);
        }

        private static List<Display> CopyDisplays(List<Display> source)
        {
            return source.Select(display => display.Copy()).ToList();
        }

        private static bool SameLayout(List<Display> a, List<Display> b)
        {
            if (a == b) return true;
            if (a.Count != b.Count) return false;
            for (int i = 0; i < a.Count; i++)
                if (a[i].Id != b[i].Id || a[i].Area != b[i].Area || a[i].Estimated != b[i].Estimated) return false;
            return true;
        }

        private void SetDisplays(List<Display> current)
        {
            displays = current;
            view.SetDisplays(displays);
        }

        private void UpdateControls()
        {
            if (displays == null) return;
            bool active = router.Active != null;
            bool resizing = view.ResizingEnabled;
            bool dirty = !SameLayout(committed, saved);
            identify.Enabled = displays.Count > 0;
            measure.Enabled = view.Selected != null;
            measure.Text = UiText.Get(resizing ? "FinishCalibration" : "Calibrate");
            measure.Primary = !resizing;
            hint.Text = UiText.Get(resizing ? "CalibrationHint" : "LayoutHint");
            save.Visible = resizing;
            undo.Enabled = history != null && !view.IsDragging;
            save.Enabled = dirty && !view.IsDragging;
            reset.Enabled = !SameLayout(committed, defaults) && !view.IsDragging;
            trayCorrection.Text = correction.Text = UiText.Get(paused ? "ResumeCorrection" : "StopCorrection");
            correctionState.Text = "●  " + UiText.Get(active ? "StateActive" : paused ? "StatePaused" : "StateWaiting");
            correctionState.ForeColor = active ? BoardStyle.Accent : BoardStyle.Muted;
            selection.Text = view.Selected == null ? "" : view.Selected.Estimated ? UiText.Format("EstimatedCaption", view.Selected.Caption) : view.Selected.Caption;
            status.ForeColor = BoardStyle.Muted;
            if (displayChangePending) status.Text = UiText.Get("PendingDisplayChange");
            else if (view.IsDragging) status.Text = UiText.Get(paused ? "DragPaused" : "DragActive");
            else if (notice != null) status.Text = notice;
            else if (dirty) status.Text = UiText.Get("Unsaved");
            else if (displays.Count < 2) status.Text = UiText.Get("NeedsTwoDisplays");
            else if (paused) status.Text = UiText.Get("CorrectionPaused");
            else if (active) status.Text = router.Active.AllDisplaysReachable ? UiText.Format("CorrectionActive", StopShortcut) : UiText.Get("CorrectionPartial");
            else status.Text = UiText.Get("CorrectionWaiting");
        }

        private void ApplyLayout()
        {
            if (paused || refresh.Enabled || displayChangePending || view.Model.Seams.Count == 0)
            {
                StopRouting();
                UpdateControls();
                return;
            }
            StartRouting(view.Model);
        }

        private void StartRouting(Layout layout)
        {
            if (!hotkey) hotkey = RegisterHotKey(Handle, 1, 0x4003, StopKey);
            if (!hotkey)
            {
                Pause(UiText.Format("HotkeyError", StopShortcut));
                return;
            }
            try
            {
                router.Start(layout, Handle);
                UpdateControls();
            }
            catch (Win32Exception e) { Pause(UiText.Format("CorrectionError", e.NativeErrorCode)); }
        }

        private void CommitEdit()
        {
            history = new Edit(committed, history);
            committed = CopyDisplays(displays);
            ApplyEdit();
        }

        private void UndoEdit()
        {
            if (history == null) return;
            committed = history.Before;
            history = history.Previous;
            SetDisplays(CopyDisplays(committed));
            ApplyEdit();
        }

        private void RestoreDefaults()
        {
            if (SameLayout(committed, defaults)) return;
            SetDisplays(CopyDisplays(defaults));
            CommitEdit();
        }

        private void ResetSettings()
        {
            try { Preferences.ArchiveSettings(layoutPath, settingsPath); }
            catch (IOException) { notice = UiText.Get("DeleteSettingsError"); UpdateControls(); return; }
            catch (UnauthorizedAccessException) { notice = UiText.Get("DeleteSettingsError"); UpdateControls(); return; }

            StopRouting();
            SetResizeMode(false);
            refresh.Stop();
            displayChangePending = false;
            paused = false;
            LoadDisplays(Windows.ReadDisplays());
            resettingSettings = true;
            try { trayPreference.Checked = true; }
            finally { resettingSettings = false; }
            UiText.SelectLanguage("");
            ApplyLanguage();
            notice = UiText.Get("DeleteSettingsDone");
            ApplyLayout();
        }

        private static string BackupFileFilter()
        {
            return UiText.Get("BackupFileType") + " (*." + SettingsBackup.Extension + ")|*." + SettingsBackup.Extension;
        }

        private void ExportSettings()
        {
            using (var dialog = new SaveFileDialog { Title = exportSettings.Text, Filter = BackupFileFilter(),
                DefaultExt = SettingsBackup.Extension, FileName = "CursorSeam-" + DateTime.Now.ToString("yyyyMMdd-HHmmss", System.Globalization.CultureInfo.InvariantCulture) })
            {
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                try
                {
                    string path = Path.GetFullPath(dialog.FileName);
                    if (string.Equals(path, Path.GetFullPath(layoutPath), StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(path, Path.GetFullPath(settingsPath), StringComparison.OrdinalIgnoreCase)) throw new IOException();
                    new SettingsBackup(committed, CurrentPreferences()).Export(path);
                    notice = UiText.Get("BackupSaved");
                }
                catch (IOException) { notice = UiText.Get("BackupWriteError"); }
                catch (UnauthorizedAccessException) { notice = UiText.Get("BackupWriteError"); }
            }
            UpdateControls();
        }

        private void ImportSettings()
        {
            string path;
            using (var dialog = new OpenFileDialog { Title = restoreSettings.Text, Filter = BackupFileFilter(), CheckFileExists = true })
            {
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                path = dialog.FileName;
            }
            using (var dialog = new BoardPrompt("RestoreSettings", "RestoreSettingsConfirm", "RestoreSettings", null, "Cancel"))
                if (dialog.ShowDialog(this) != DialogResult.Yes) return;

            try { RestoreSettings(path); }
            catch (InvalidDataException) { notice = UiText.Get("RestoreSettingsError"); }
            catch (IOException) { notice = UiText.Get("RestoreSettingsError"); }
            catch (UnauthorizedAccessException) { notice = UiText.Get("RestoreSettingsError"); }
            catch (System.Xml.XmlException) { notice = UiText.Get("RestoreSettingsError"); }
            catch (FormatException) { notice = UiText.Get("RestoreSettingsError"); }
            catch (ArgumentException) { notice = UiText.Get("RestoreSettingsError"); }
            catch (OverflowException) { notice = UiText.Get("RestoreSettingsError"); }
            UpdateControls();
        }

        private void RestoreSettings(string path)
        {
            var detected = Windows.ReadDisplays();
            var restored = SettingsBackup.Read(path, detected);
            Preferences.Save(layoutPath, restored.Displays, restored.Preferences);

            StopRouting();
            SetResizeMode(false);
            refresh.Stop();
            displayChangePending = false;
            LoadDisplays(detected);
            resettingSettings = true;
            try { trayPreference.Checked = restored.Preferences.TrayResident; }
            finally { resettingSettings = false; }
            UiText.SelectLanguage(restored.Preferences.Language);
            ApplyLanguage();
            notice = UiText.Get("RestoreSettingsDone");
            ApplyLayout();
        }

        private void ApplyEdit()
        {
            notice = null;
            ApplyLayout();
            if (!view.ResizingEnabled && !SameLayout(committed, saved)) SaveLayout(false);
        }

        private bool SaveLayout(bool announce)
        {
            try
            {
                Preferences.Save(layoutPath, committed, CurrentPreferences());
                saved = committed;
                savedHistory = history;
                if (announce) notice = UiText.Get("Saved");
                UpdateControls();
                return true;
            }
            catch (IOException) { notice = UiText.Get("SaveError"); }
            catch (UnauthorizedAccessException) { notice = UiText.Get("SaveDenied"); }
            UpdateControls();
            return false;
        }

        private bool TryFinishCalibration()
        {
            if (!view.ResizingEnabled) return true;
            view.CancelDrag();
            if (SameLayout(committed, saved)) return FinishCalibration(DialogResult.No);
            using (var dialog = new BoardPrompt("SavePromptTitle", "SavePromptMessage", "SaveSettings", "SavePromptDiscard", "SavePromptCancel"))
                return FinishCalibration(dialog.ShowDialog(this));
        }

        private bool FinishCalibration(DialogResult choice)
        {
            if (choice == DialogResult.Yes && !SaveLayout(true)) return false;
            if (choice != DialogResult.Yes && choice != DialogResult.No) return false;
            if (choice == DialogResult.No && !SameLayout(committed, saved))
            {
                committed = saved;
                history = savedHistory;
                notice = null;
                SetDisplays(CopyDisplays(saved));
            }
            SetResizeMode(false);
            if (displayChangePending) RefreshDisplays();
            else ApplyLayout();
            return true;
        }

        private void StopRouting()
        {
            if (router.Active != null) router.Stop();
            if (hotkey && markedWindow != IntPtr.Zero) UnregisterHotKey(markedWindow, 1);
            hotkey = false;
        }

        private void ToggleCorrection()
        {
            if (!paused) { Pause(UiText.Get("CorrectionPaused")); return; }
            paused = false;
            notice = null;
            ApplyLayout();
        }

        private void Pause(string message)
        {
            paused = true;
            StopRouting();
            notice = message;
            UpdateControls();
        }

        private void Identify()
        {
            foreach (Display display in displays) new Identifier(display).Show();
        }

        private void SetResizeMode(bool enabled)
        {
            view.SetResizeMode(enabled);
            if (enabled) ShowRulers();
            else CloseRulers();
            UpdateControls();
        }

        private void ShowRulers()
        {
            if (!view.ResizingEnabled || refresh.Enabled || displayChangePending || rulers.Count > 0 || !Visible || WindowState == FormWindowState.Minimized) return;
            foreach (Display display in view.Model.Displays)
            {
                var ruler = new RulerOverlay(display);
                rulers.Add(ruler);
                ruler.Show(this);
            }
        }

        private void UpdateRulers()
        {
            for (int i = 0; i < rulers.Count; i++) rulers[i].UpdateDisplay(view.Model.Displays[i]);
        }

        private void CloseRulers()
        {
            foreach (RulerOverlay ruler in rulers) ruler.Close();
            rulers.Clear();
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg != 0 && m.Msg == InstanceWindow.RestoreMessage)
            {
                RestoreWindow();
                return;
            }
            if (m.Msg != 0 && m.Msg == InstanceWindow.CloseForUpdateMessage)
            {
                m.Result = CloseForUpdate(m.WParam != IntPtr.Zero) ? new IntPtr(1) : IntPtr.Zero;
                return;
            }
            if (m.Msg == 0x00ff && router != null) router.OnRawMouse(m.LParam);
            if (m.Msg == 0x0312 && m.WParam.ToInt32() == 1 && hotkey) Pause(UiText.Get("StoppedByShortcut"));
            base.WndProc(ref m);
            if (BoardStyle.IsThemeChange(m.Msg) && tray != null && (BoardStyle.RefreshTheme() || BackColor != BoardStyle.Surface)) ApplyTheme();
            if (m.Msg == 0x02e0 && tray != null) RefreshTrayIcon();
            if (m.Msg == 0x007e && refresh != null && displays != null)
            {
                StopRouting();
                view.CancelDrag();
                CloseRulers();
                refresh.Stop();
                refresh.Start();
            }
        }

        protected override bool ProcessCmdKey(ref Message message, Keys keyData)
        {
            if (keyData == Keys.Escape && view.ResizingEnabled)
            {
                TryFinishCalibration();
                return true;
            }
            return base.ProcessCmdKey(ref message, keyData);
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            BoardStyle.ApplyTitleBar(Handle);
            markedWindow = Handle;
            InstanceWindow.Mark(markedWindow);
            if (routingToRestore != null)
            {
                Layout previous = routingToRestore;
                routingToRestore = null;
                StartRouting(previous);
            }
        }

        protected override void OnHandleDestroyed(EventArgs e)
        {
            routingToRestore = RecreatingHandle ? router.Active : null;
            StopRouting();
            if (markedWindow != IntPtr.Zero) InstanceWindow.Unmark(markedWindow);
            markedWindow = IntPtr.Zero;
            base.OnHandleDestroyed(e);
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (e.CloseReason == CloseReason.UserClosing)
            {
                if (trayPreference.Checked && !exitRequested)
                {
                    CloseRulers();
                    Hide();
                    e.Cancel = true;
                }
                else e.Cancel = !TryFinishCalibration();
            }
            base.OnFormClosing(e);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && !resourcesDisposed)
            {
                resourcesDisposed = true;
                StopRouting();
                router.Dispose();
                CloseRulers();
                refresh.Dispose();
                tray.Visible = false;
                tray.ContextMenuStrip.Dispose();
                tray.Dispose();
                startupHelp.Dispose();
                base.Dispose(true);
                trayIcon.Dispose();
                windowIcon.Dispose();
                windowFont.Dispose();
                headingFont.Dispose();
                return;
            }
            base.Dispose(disposing);
        }
    }

    internal sealed class BoardPrompt : Form
    {
        private readonly Font promptFont;
        internal BoardPrompt(string titleKey, string messageKey, string confirmKey, string discardKey, string cancelKey)
        {
            Text = UiText.Get(titleKey);
            Font = promptFont = BoardStyle.CreateFont(10f, FontStyle.Regular, GraphicsUnit.Point);
            BackColor = BoardStyle.Surface;
            ForeColor = BoardStyle.Ink;
            AutoScaleMode = AutoScaleMode.Dpi;
            AutoSize = true;
            AutoSizeMode = AutoSizeMode.GrowAndShrink;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent;
            ShowInTaskbar = false;
            MinimizeBox = MaximizeBox = false;

            var content = new TableLayoutPanel { AutoSize = true, ColumnCount = 1, RowCount = 2, Dock = DockStyle.Fill, Padding = new Padding(15) };
            var message = new Label { AutoSize = true, MaximumSize = new Size(410, 0), Margin = new Padding(3, 2, 3, 15), Text = UiText.Get(messageKey) };
            var actions = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, MaximumSize = new Size(510, 0) };
            var confirm = new BoardButton(confirmKey, true) { DialogResult = DialogResult.Yes };
            var cancel = new BoardButton(cancelKey, false) { DialogResult = DialogResult.Cancel };
            actions.Controls.Add(confirm);
            if (discardKey != null) actions.Controls.Add(new BoardButton(discardKey, false) { DialogResult = DialogResult.No });
            actions.Controls.Add(cancel);
            content.Controls.Add(message, 0, 0);
            content.Controls.Add(actions, 0, 1);
            Controls.Add(content);
            AcceptButton = discardKey == null ? cancel : confirm;
            CancelButton = cancel;
            if (discardKey == null) ActiveControl = cancel;
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            BoardStyle.ApplyTitleBar(Handle);
        }

        protected override void WndProc(ref Message m)
        {
            base.WndProc(ref m);
            if (!BoardStyle.IsThemeChange(m.Msg)) return;
            BoardStyle.RefreshTheme();
            BackColor = BoardStyle.Surface;
            ForeColor = BoardStyle.Ink;
            BoardStyle.ApplyTitleBar(Handle);
            Invalidate(true);
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            if (disposing) promptFont.Dispose();
        }
    }

    internal sealed class Identifier : Form
    {
        private readonly Timer timer = new Timer { Interval = 1500 };
        private readonly Font numberFont = BoardStyle.CreateFont(36, FontStyle.Bold, GraphicsUnit.Point);
        internal Identifier(Display display)
        {
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.Manual;
            ShowInTaskbar = false;
            TopMost = true;
            Bounds = new Rectangle(display.Pixels.Left + 30, display.Pixels.Top + 30, 120, 100);
            BackColor = BoardStyle.Surface;
            Controls.Add(new Label { Dock = DockStyle.Fill, Text = display.Number.ToString(), TextAlign = ContentAlignment.MiddleCenter, ForeColor = BoardStyle.Accent, Font = numberFont });
            timer.Tick += (sender, e) => Close();
            timer.Start();
        }
        protected override bool ShowWithoutActivation { get { return true; } }
        protected override CreateParams CreateParams
        {
            get { var cp = base.CreateParams; cp.ExStyle |= 0x08000020; return cp; }
        }
        protected override void Dispose(bool disposing)
        {
            if (disposing) timer.Dispose();
            base.Dispose(disposing);
            if (disposing) numberFont.Dispose();
        }
    }
}

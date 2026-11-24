using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;

namespace CursorSeam
{
    internal sealed class AboutDialog : Form
    {
        internal const string GithubProfile = "https://github.com/nekomimiyks";
        internal const string StoreApps = "https://apps.microsoft.com/search/publisher?name=nekomimiyks";
        internal const string StoreAppUri = "ms-windows-store://publisher/?name=nekomimiyks";
        private readonly LinkLabel github = MakeLink("GithubProfile"), store = MakeLink("StoreApps");
        private readonly Label error = new Label { AutoSize = true, Visible = false };
        private readonly Label versionLabel = new Label { AutoSize = true, Margin = new Padding(0, 7, 0, 18) };
        private readonly Button close = new Button
        {
            AutoSize = true, MinimumSize = new Size(102, 36), DialogResult = DialogResult.Cancel,
            Anchor = AnchorStyles.Right, Margin = new Padding(0, 17, 0, 0), UseVisualStyleBackColor = false
        };

        internal AboutDialog()
        {
            Text = UiText.Get("AboutApp");
            AutoScaleMode = AutoScaleMode.Dpi;
            AutoSize = true;
            AutoSizeMode = AutoSizeMode.GrowAndShrink;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent;
            ShowInTaskbar = false;
            MinimizeBox = MaximizeBox = false;

            var content = new TableLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, ColumnCount = 1,
                Padding = new Padding(24, 22, 21, 20), MinimumSize = new Size(208, 0) };
            content.Controls.Add(new Label { Text = Application.ProductName, AutoSize = true, Margin = new Padding(0) });
#if STORE_BUILD
            string version = typeof(AboutDialog).Assembly.GetName().Version.ToString();
#else
            string version = Application.ProductVersion;
#endif
            versionLabel.Text = UiText.Format("AppVersion", version);
            content.Controls.Add(versionLabel);
            github.LinkClicked += (sender, e) => ShowLinkResult(TryOpen(GithubProfile));
            store.LinkClicked += (sender, e) => ShowLinkResult(OpenStore(TryOpen));
            github.Margin = new Padding(0, 0, 0, 9);
            store.Margin = new Padding(0);
            content.Controls.Add(github);
            content.Controls.Add(store);
            content.Controls.Add(error);
            close.Text = UiText.Get("CloseAbout");
            content.Controls.Add(close);
            Controls.Add(content);
            CancelButton = close;
            ActiveControl = close;
            ApplyTheme();
        }

        internal static LinkLabel MakeLink(string key)
        {
            var link = new LinkLabel { AutoSize = true, TabStop = true, Text = UiText.Get(key), LinkBehavior = LinkBehavior.HoverUnderline, UseMnemonic = false };
            StyleLink(link);
            return link;
        }

        internal static void StyleLink(LinkLabel link, bool prominent = false)
        {
            link.LinkColor = BoardStyle.HighContrast ? SystemColors.HotTrack :
                (prominent ? BoardStyle.Accent : BoardStyle.Muted);
            link.ActiveLinkColor = BoardStyle.HighContrast ? SystemColors.HotTrack : BoardStyle.Accent;
            link.VisitedLinkColor = link.LinkColor;
        }

        internal static bool OpenStore(Func<string, bool> tryOpen)
        {
            return tryOpen(StoreAppUri) || tryOpen(StoreApps);
        }

        private static bool TryOpen(string url)
        {
            try
            {
                using (var browser = Process.Start(new ProcessStartInfo(url) { UseShellExecute = true })) { }
                return true;
            }
            catch (Win32Exception) { return false; }
        }

        private void ShowLinkResult(bool opened)
        {
            if (!opened) error.Text = UiText.Get("AboutLinkError");
            error.Visible = !opened;
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
            ApplyTheme();
            BoardStyle.ApplyTitleBar(Handle);
        }

        private void ApplyTheme()
        {
            BackColor = BoardStyle.Surface;
            ForeColor = BoardStyle.Ink;
            versionLabel.ForeColor = BoardStyle.Muted;
            StyleLink(github, true);
            StyleLink(store, true);
            close.FlatStyle = BoardStyle.HighContrast ? FlatStyle.System : FlatStyle.Flat;
            close.BackColor = BoardStyle.Field;
            close.ForeColor = BoardStyle.Ink;
            close.FlatAppearance.BorderColor = BoardStyle.FieldBorder;
            close.FlatAppearance.MouseOverBackColor = BoardStyle.QuietButton;
        }
    }
}

using System.Drawing;
using System.Windows.Forms;

namespace DualSenseApp.Services
{
    public sealed class TrayService : IDisposable
    {
        private readonly NotifyIcon _icon;
        private readonly Icon _ownedIcon;
        private readonly Form _form;

        public TrayService(Form form, Action toggleXbox, Action cycleProfile, Action exit)
        {
            _form = form;
            _ownedIcon = CreateIcon();
            _icon = new NotifyIcon
            {
                Icon = _ownedIcon,
                Text = "DualSense Neural Link",
                Visible = true,
                ContextMenuStrip = BuildMenu(toggleXbox, cycleProfile, exit)
            };
            _icon.DoubleClick += (_, _) => Restore();
        }

        public void Restore()
        {
            _form.Show();
            _form.WindowState = FormWindowState.Normal;
            _form.Activate();
        }

        public void HideToTray()
        {
            _form.Hide();
        }

        private ContextMenuStrip BuildMenu(Action toggleXbox, Action cycleProfile, Action exit)
        {
            var menu = new ContextMenuStrip();
            menu.Items.Add("Show", null, (_, _) => Restore());
            menu.Items.Add("Toggle Xbox", null, (_, _) => toggleXbox());
            menu.Items.Add("Next profile", null, (_, _) => cycleProfile());
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("Exit", null, (_, _) => exit());
            return menu;
        }

        private static Icon CreateIcon()
        {
            using var bmp = new Bitmap(16, 16);
            using (var g = Graphics.FromImage(bmp))
            {
                g.Clear(Color.FromArgb(13, 11, 24));
                using var b = new SolidBrush(Color.FromArgb(255, 42, 133));
                g.FillEllipse(b, 2, 2, 12, 12);
            }
            return Icon.FromHandle(bmp.GetHicon());
        }

        public void Dispose()
        {
            _icon.Visible = false;
            _icon.Dispose();
            _ownedIcon.Dispose();
        }
    }
}

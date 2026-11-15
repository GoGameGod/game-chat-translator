using System;
using System.Drawing;
using System.Windows.Forms;

namespace ChatTranslator
{
    // Всплывающее уведомление внизу экрана. Не забирает фокус у игры.
    class Toast : Form
    {
        readonly Label lbl;
        readonly Timer timer = new Timer();
        Color accent = Theme.Accent;

        public static readonly Color Info = Color.FromArgb(100, 150, 255);
        public static readonly Color Ok = Color.FromArgb(80, 200, 120);
        public static readonly Color Err = Color.FromArgb(240, 90, 90);

        public Toast()
        {
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            TopMost = true;
            StartPosition = FormStartPosition.Manual;
            BackColor = Color.FromArgb(32, 34, 40);
            float k = Native.DpiScale();
            lbl = new Label
            {
                AutoSize = true,
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 11f),
                MaximumSize = new Size((int)(700 * k), 0),
                Location = new Point((int)(20 * k), (int)(12 * k)),
                BackColor = Color.Transparent
            };
            Controls.Add(lbl);
            timer.Tick += (s, e) => { timer.Stop(); Hide(); };
            lbl.Click += (s, e) => Hide();
            Click += (s, e) => Hide();
        }

        protected override bool ShowWithoutActivation { get { return true; } }

        protected override CreateParams CreateParams
        {
            get
            {
                var cp = base.CreateParams;
                cp.ExStyle |= 0x08000000 /*NOACTIVATE*/ | 0x80 /*TOOLWINDOW*/ | 0x8 /*TOPMOST*/;
                return cp;
            }
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            Native.RoundCorners(Handle);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            using (var b = new SolidBrush(accent)) e.Graphics.FillRectangle(b, 0, 0, (int)(6 * Native.DpiScale()), Height);
        }

        public void ShowMessage(string text, Color accentColor, int ms, IntPtr nearWindow)
        {
            accent = accentColor;
            lbl.Text = text;
            ClientSize = new Size(lbl.Right + lbl.Left - 2, lbl.Bottom + lbl.Top);
            Screen scr;
            try { scr = nearWindow != IntPtr.Zero ? Screen.FromHandle(nearWindow) : Screen.FromPoint(Cursor.Position); }
            catch { scr = Screen.PrimaryScreen; }
            var wa = scr.WorkingArea;
            Location = DocShots.Offscreen ? new Point(-20000, -20000)
                : new Point(wa.Left + (wa.Width - Width) / 2, wa.Bottom - Height - wa.Height / 7);
            if (!Visible) Show();
            Invalidate();
            timer.Stop();
            timer.Interval = Math.Max(1000, ms);
            timer.Start();
        }

        public static int TimeFor(string text)
        {
            return Math.Min(15000, 3500 + (text ?? "").Length * 50);
        }
    }
}

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace ChatTranslator
{
    // Полупрозрачная панель с переводом чата рядом с областью чата.
    // Не забирает фокус и пропускает клики насквозь — игре не мешает.
    class ChatOverlay : Form
    {
        readonly List<ChatEntry> entries = new List<ChatEntry>();
        readonly Timer cleanup = new Timer { Interval = 5000 };
        readonly Font fHead, fText, fOrig;
        readonly float k;
        Rectangle region;
        string status;
        bool statusError;
        public bool ShowOriginal;
        public string HotkeyHint = "";

        const int MaxEntries = 6;
        static readonly TimeSpan Ttl = TimeSpan.FromSeconds(120);

        public ChatOverlay()
        {
            k = Native.DpiScale();
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            TopMost = true;
            StartPosition = FormStartPosition.Manual;
            BackColor = Color.FromArgb(22, 24, 30);
            Opacity = 0.9;
            DoubleBuffered = true;
            fHead = new Font("Segoe UI", 8.25f);
            fText = new Font("Segoe UI", 10.5f);
            fOrig = new Font("Segoe UI", 8.25f, FontStyle.Italic);
            cleanup.Tick += (s, e) =>
            {
                int before = entries.Count;
                entries.RemoveAll(x => DateTime.Now - x.Time > Ttl);
                if (entries.Count != before) Relayout();
            };
        }

        protected override bool ShowWithoutActivation { get { return true; } }

        protected override CreateParams CreateParams
        {
            get
            {
                var cp = base.CreateParams;
                // NOACTIVATE | TOOLWINDOW | TOPMOST | TRANSPARENT (клики насквозь) | LAYERED
                cp.ExStyle |= 0x08000000 | 0x80 | 0x8 | 0x20 | 0x80000;
                return cp;
            }
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            Native.RoundCorners(Handle);
        }

        public void Begin(Rectangle chatRegion, bool showOriginal, string hotkey)
        {
            region = chatRegion;
            ShowOriginal = showOriginal;
            HotkeyHint = hotkey ?? "";
            entries.Clear();
            status = L.T("Читаю чат…", "Reading chat…");
            statusError = false;
            Relayout();
            if (!Visible) Show();
            cleanup.Start();
        }

        public void End()
        {
            cleanup.Stop();
            Hide();
        }

        public void Add(List<ChatEntry> list)
        {
            entries.AddRange(list);
            if (entries.Count > MaxEntries) entries.RemoveRange(0, entries.Count - MaxEntries);
            status = null;
            Relayout();
        }

        public void SetStatus(string text, bool error)
        {
            status = text;
            statusError = error;
            Relayout();
        }

        int S(int v) { return (int)Math.Round(v * k); }

        string Header()
        {
            string h = L.T("Перевод чата", "Chat translation");
            if (HotkeyHint.Length > 0) h += "   ·   " + HotkeyHint + L.T(" — выкл", " — off");
            return h;
        }

        void Relayout()
        {
            int pad = S(10);
            int width = Math.Max(S(320), Math.Min(region.Width, S(700)));
            int inner = width - 2 * pad;
            int y = pad;
            y += TextRenderer.MeasureText(Header(), fHead).Height + S(4);
            if (status != null) y += Measure(status, fText, inner) + S(4);
            foreach (var e in entries)
            {
                if (ShowOriginal) y += Measure(e.Original, fOrig, inner);
                y += Measure(e.Translated, fText, inner) + S(5);
            }
            int height = y + pad - S(5);

            var scr = Screen.FromRectangle(region).WorkingArea;
            int x = Math.Max(scr.Left, Math.Min(region.Left, scr.Right - width));
            int top = region.Top - height - S(6);
            if (top < scr.Top) top = Math.Min(region.Bottom + S(6), scr.Bottom - height);
            Bounds = DocShots.Offscreen ? new Rectangle(-20000, -20000, width, height) : new Rectangle(x, top, width, height);
            Invalidate();
        }

        static int Measure(string text, Font f, int width)
        {
            return TextRenderer.MeasureText(text ?? "", f, new Size(width, 0), TextFormatFlags.WordBreak).Height;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            int pad = S(10);
            int inner = Width - 2 * pad;
            int y = pad;
            var flags = TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix;
            TextRenderer.DrawText(g, Header(), fHead, new Rectangle(pad, y, inner, 100), Color.FromArgb(130, 160, 230), flags);
            y += TextRenderer.MeasureText(Header(), fHead).Height + S(4);
            if (status != null)
            {
                int h = Measure(status, fText, inner);
                TextRenderer.DrawText(g, status, fText, new Rectangle(pad, y, inner, h),
                    statusError ? Color.FromArgb(255, 140, 120) : Color.FromArgb(150, 155, 170), flags);
                y += h + S(4);
            }
            foreach (var en in entries)
            {
                if (ShowOriginal)
                {
                    int ho = Measure(en.Original, fOrig, inner);
                    TextRenderer.DrawText(g, en.Original, fOrig, new Rectangle(pad, y, inner, ho), Color.FromArgb(140, 145, 160), flags);
                    y += ho;
                }
                int ht = Measure(en.Translated, fText, inner);
                TextRenderer.DrawText(g, en.Translated, fText, new Rectangle(pad, y, inner, ht), Color.White, flags);
                y += ht + S(5);
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) { cleanup.Dispose(); fHead.Dispose(); fText.Dispose(); fOrig.Dispose(); }
            base.Dispose(disposing);
        }
    }
}

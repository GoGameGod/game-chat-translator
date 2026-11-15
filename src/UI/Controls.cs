using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Text;
using System.Windows.Forms;

namespace ChatTranslator
{
    static class Theme
    {
        public static readonly Color Accent = Color.FromArgb(52, 120, 246);
        public static readonly Color AccentDark = Color.FromArgb(30, 85, 200);
        public static readonly Color Gray = Color.FromArgb(105, 105, 112);
        public static readonly Color Heading = Color.FromArgb(40, 80, 160);
        public static readonly Color GuideBack = Color.FromArgb(244, 246, 250);
        public static readonly Color Warn = Color.FromArgb(160, 80, 0);
        public static readonly Color Good = Color.FromArgb(20, 115, 55);
        public static readonly Color Bad = Color.FromArgb(190, 40, 40);
        public static readonly Color Flash = Color.FromArgb(255, 238, 160);

        public static Icon MakeIcon(Color bg)
        {
            using (var bmp = new Bitmap(32, 32))
            {
                using (var g = Graphics.FromImage(bmp))
                {
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
                    g.Clear(Color.Transparent);
                    using (var b = new SolidBrush(bg)) g.FillEllipse(b, 1, 1, 30, 30);
                    using (var f = new Font("Segoe UI", 15f, FontStyle.Bold, GraphicsUnit.Pixel))
                    using (var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                        g.DrawString("Tr", f, Brushes.White, new RectangleF(0, 0, 32, 32), sf);
                }
                IntPtr h = bmp.GetHicon();
                Icon ic = (Icon)Icon.FromHandle(h).Clone();
                Native.DestroyIcon(h);
                return ic;
            }
        }
    }

    // Мелкие помощники для вёрстки в FlowLayoutPanel.
    static class Ui
    {
        public const int W = 566; // ширина содержимого вкладки

        public static FlowLayoutPanel Flow()
        {
            return new FlowLayoutPanel
            {
                Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false,
                AutoScroll = true, Padding = new Padding(6, 8, 6, 8)
            };
        }

        public static Label Section(string text)
        {
            return new Label
            {
                Text = text, AutoSize = true, Font = new Font("Segoe UI", 9.75f, FontStyle.Bold),
                ForeColor = Theme.Heading, Margin = new Padding(3, 12, 3, 5)
            };
        }

        public static Label Intro(string text)
        {
            return new Label
            {
                Text = text, AutoSize = true, MaximumSize = new Size(W, 0),
                Font = new Font("Segoe UI", 9f, FontStyle.Bold), Margin = new Padding(3, 0, 3, 6)
            };
        }

        public static Label Note(string text)
        {
            return new Label
            {
                Text = text, AutoSize = true, MaximumSize = new Size(W - 12, 0),
                ForeColor = Theme.Gray, Margin = new Padding(6, 2, 3, 6)
            };
        }

        public static CheckBox Check(string text)
        {
            return new CheckBox { Text = text, AutoSize = true, Margin = new Padding(8, 3, 3, 3) };
        }

        // Строка «подпись — контрол».
        public static Panel Row(string label, Control c, int labelWidth = 170)
        {
            var p = new Panel { Size = new Size(W, 30), Margin = new Padding(0) };
            var l = new Label { Text = label, Location = new Point(6, 6), Size = new Size(labelWidth - 10, 18), AutoEllipsis = true };
            c.Location = new Point(labelWidth, 2);
            if (c.Width + labelWidth > W - 6 || c.Width < 40) c.Width = W - 6 - labelWidth;
            p.Controls.Add(l);
            p.Controls.Add(c);
            return p;
        }

        public static LinkLabel Link(string text, string url)
        {
            var l = new LinkLabel
            {
                Text = text, AutoSize = true, LinkColor = Theme.Accent, ActiveLinkColor = Theme.AccentDark,
                LinkBehavior = LinkBehavior.HoverUnderline, Margin = new Padding(6, 2, 3, 4)
            };
            l.LinkClicked += (s, e) => OpenUrl(url);
            return l;
        }

        public static void OpenUrl(string url)
        {
            try { System.Diagnostics.Process.Start(url); } catch { }
        }

        public static FlowLayoutPanel Line(params Control[] controls)
        {
            var p = new FlowLayoutPanel { FlowDirection = FlowDirection.LeftToRight, AutoSize = true, WrapContents = false, Margin = new Padding(0) };
            foreach (var c in controls) p.Controls.Add(c);
            return p;
        }
    }

    // Карточка выбора (режим, сервис перевода).
    class PresetCard : Panel
    {
        readonly Label title, sub;
        bool selected;
        public event EventHandler Picked;

        public PresetCard(string t, string s)
        {
            Size = new Size(182, 76);
            Margin = new Padding(3);
            Cursor = Cursors.Hand;
            BackColor = Color.White;
            DoubleBuffered = true;
            title = new Label
            {
                Text = t, Location = new Point(10, 7), AutoSize = true,
                Font = new Font("Segoe UI", 10.5f, FontStyle.Bold), BackColor = Color.Transparent, Cursor = Cursors.Hand
            };
            sub = new Label
            {
                Text = s, Location = new Point(10, 30), Size = new Size(166, 44),
                ForeColor = Color.FromArgb(95, 95, 100), Font = new Font("Segoe UI", 8.25f),
                BackColor = Color.Transparent, Cursor = Cursors.Hand
            };
            Controls.Add(title);
            Controls.Add(sub);
            EventHandler click = (o, e) => { if (Picked != null) Picked(this, EventArgs.Empty); };
            Click += click;
            title.Click += click;
            sub.Click += click;
        }

        public bool Selected
        {
            get { return selected; }
            set
            {
                selected = value;
                BackColor = value ? Color.FromArgb(234, 241, 255) : Color.White;
                title.ForeColor = value ? Theme.Accent : Color.FromArgb(30, 30, 30);
                Invalidate();
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            var r = new Rectangle(0, 0, Width - 1, Height - 1);
            if (selected) r.Inflate(-1, -1);
            using (var pen = new Pen(selected ? Theme.Accent : Color.FromArgb(205, 208, 215), selected ? 2f : 1f))
                e.Graphics.DrawRectangle(pen, r);
        }
    }

    // Поле для записи сочетания клавиш.
    class HotkeyBox : TextBox
    {
        readonly Timer flashTimer = new Timer { Interval = 1600 };

        public HotkeyBox()
        {
            ReadOnly = true;
            BackColor = SystemColors.Window;
            Cursor = Cursors.Hand;
            ShortcutsEnabled = false;
            flashTimer.Tick += (s, e) => { flashTimer.Stop(); BackColor = SystemColors.Window; };
        }

        public void Flash()
        {
            BackColor = Theme.Flash;
            flashTimer.Stop();
            flashTimer.Start();
        }

        protected override bool IsInputKey(Keys keyData) { return true; }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            Keys key = keyData & Keys.KeyCode;
            if ((keyData & Keys.Modifiers) == 0 && (key == Keys.Back || key == Keys.Delete || key == Keys.Escape))
            {
                Text = "";
                return true;
            }
            string s = Hotkey.Format(keyData);
            if (s != null) Text = s;
            return true;
        }

        protected override void OnKeyDown(KeyEventArgs e) { e.Handled = true; e.SuppressKeyPress = true; }
        protected override void OnKeyPress(KeyPressEventArgs e) { e.Handled = true; }

        protected override void Dispose(bool disposing)
        {
            if (disposing) flashTimer.Dispose();
            base.Dispose(disposing);
        }
    }

    // Инструкция: заголовки, шаги, пункты; {токены} превращаются в ссылки.
    class GuideView : FlowLayoutPanel
    {
        public event Action<string> LinkClicked;
        public Func<string, string> Resolve;
        readonly int width;

        public GuideView(int width)
        {
            this.width = width;
            FlowDirection = FlowDirection.TopDown;
            WrapContents = false;
            AutoSize = true;
            AutoSizeMode = AutoSizeMode.GrowAndShrink;
            BackColor = Theme.GuideBack;
            Padding = new Padding(12, 9, 12, 10);
            Margin = new Padding(3, 0, 3, 2);
            MinimumSize = new Size(width, 0);
            MaximumSize = new Size(width, 0);
        }

        public void Render(List<HelpBlock> blocks)
        {
            SuspendLayout();
            while (Controls.Count > 0)
            {
                var c = Controls[0];
                Controls.RemoveAt(0);
                c.Dispose();
            }
            int step = 0;
            bool first = true;
            foreach (var bl in blocks)
            {
                string prefix = "";
                int indent = 0;
                if (bl.Kind == 'H') step = 0;
                if (bl.Kind == 'S') { step++; prefix = step + ".  "; indent = 10; }
                if (bl.Kind == 'B') { prefix = "•  "; indent = 10; }
                var ll = MakeLabel(prefix + bl.Text);
                int top = 2;
                if (bl.Kind == 'H') { ll.Font = new Font("Segoe UI", 9.5f, FontStyle.Bold); top = first ? 0 : 10; }
                if (bl.Kind == 'W') { ll.ForeColor = Theme.Warn; top = 10; }
                if (bl.Kind == 'G') { ll.ForeColor = Theme.Good; top = 10; }
                if (bl.Kind == 'P' && !first) top = 6;
                ll.Margin = new Padding(indent, top, 0, 2);
                ll.MaximumSize = new Size(width - Padding.Horizontal - indent - 4, 0);
                Controls.Add(ll);
                first = false;
            }
            ResumeLayout(true);
        }

        LinkLabel MakeLabel(string template)
        {
            var ll = new LinkLabel
            {
                AutoSize = true, LinkBehavior = LinkBehavior.HoverUnderline,
                LinkColor = Theme.Accent, ActiveLinkColor = Theme.AccentDark, BackColor = Color.Transparent
            };
            var sb = new StringBuilder();
            var starts = new List<int>();
            var lens = new List<int>();
            var targets = new List<string>();
            int i = 0;
            while (i < template.Length)
            {
                int open = template.IndexOf('{', i);
                int close = open < 0 ? -1 : template.IndexOf('}', open);
                if (open < 0 || close < 0) { sb.Append(template.Substring(i)); break; }
                sb.Append(template.Substring(i, open - i));
                string token = template.Substring(open + 1, close - open - 1);
                string disp = Resolve != null ? Resolve(token) : token;
                starts.Add(sb.Length);
                lens.Add(disp.Length);
                targets.Add(token);
                sb.Append(disp);
                i = close + 1;
            }
            ll.Text = sb.ToString();
            ll.Links.Clear();
            for (int k = 0; k < starts.Count; k++) ll.Links.Add(starts[k], lens[k], targets[k]);
            ll.LinkClicked += (s, e) => { if (LinkClicked != null) LinkClicked(e.Link.LinkData as string); };
            return ll;
        }
    }
}

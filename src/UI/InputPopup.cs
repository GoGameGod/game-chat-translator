using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace ChatTranslator
{
    // Окно ввода поверх игры.
    class InputPopup : Form
    {
        public readonly TextBox Box;
        readonly Label header, result, hint;
        readonly List<string> history = new List<string>();
        readonly float k;
        int histPos = -1;
        InputLanguage prevLang;
        bool closingQuietly;
        string previewSource, previewText;

        static readonly Color HeaderColor = Color.FromArgb(140, 175, 255);

        public bool Busy { get; private set; }
        public IntPtr Target { get; private set; }
        public event Action<string, bool, bool> Submitted;   // текст, «только в буфер», «только показать перевод»
        public event Action Cancelled;
        public event Action EngineToggleRequested;

        int S(int v) { return (int)Math.Round(v * k); }

        public InputPopup()
        {
            k = Native.DpiScale();
            AutoScaleMode = AutoScaleMode.None;
            Font = new Font("Segoe UI", 9f);
            Text = AppInfo.Name;
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            TopMost = true;
            StartPosition = FormStartPosition.Manual;
            BackColor = Color.FromArgb(30, 32, 38);
            KeyPreview = true;

            int w = S(700), pad = S(14), inner = w - 2 * pad;
            header = new Label
            {
                Location = new Point(pad, S(9)), Size = new Size(inner, S(20)),
                ForeColor = HeaderColor, Font = new Font("Segoe UI", 9f, FontStyle.Bold), AutoEllipsis = true
            };
            Box = new TextBox
            {
                Location = new Point(pad, S(33)), Width = inner, Font = new Font("Segoe UI", 13f),
                BackColor = Color.FromArgb(44, 47, 56), ForeColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle, MaxLength = 1500
            };
            result = new Label
            {
                AutoSize = true, MaximumSize = new Size(inner, 0), Location = new Point(pad, 0),
                ForeColor = Color.FromArgb(150, 225, 165), Font = new Font("Segoe UI", 11.5f), Visible = false
            };
            hint = new Label
            {
                Location = new Point(pad, 0), Size = new Size(inner, S(18)),
                ForeColor = Color.FromArgb(120, 128, 145), AutoEllipsis = true
            };
            Controls.Add(header);
            Controls.Add(Box);
            Controls.Add(result);
            Controls.Add(hint);
            ClientSize = new Size(w, S(100));
            Relayout();

            Box.TextChanged += (s, e) =>
            {
                if (result.Visible) result.ForeColor = Box.Text.Trim() == previewSource ? Color.FromArgb(150, 225, 165) : Color.FromArgb(120, 128, 145);
            };

            Deactivate += (s, e) =>
            {
                if (Visible && !Busy && !closingQuietly)
                {
                    RestoreLayout();
                    Hide();
                    if (Cancelled != null) Cancelled();
                }
            };
        }

        void Relayout()
        {
            result.Top = Box.Bottom + S(7);
            hint.Top = (result.Visible ? result.Bottom : Box.Bottom) + S(7);
            ClientSize = new Size(ClientSize.Width, hint.Bottom + S(8));
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            Native.RoundCorners(Handle);
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == Keys.Escape) { Cancel(); return true; }
            if (Busy) return true;
            switch (keyData)
            {
                case Keys.Enter: Submit(false, false); return true;
                case Keys.Shift | Keys.Enter: Submit(true, false); return true;
                case Keys.Tab: Submit(false, true); return true;
                case Keys.F2: if (EngineToggleRequested != null) EngineToggleRequested(); return true;
                case Keys.Up: History(-1); return true;
                case Keys.Down: History(1); return true;
                case Keys.Control | Keys.A: Box.SelectAll(); return true;
                case Keys.Control | Keys.Back: DeleteWord(); return true;
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        void Submit(bool copyOnly, bool preview)
        {
            string t = Box.Text.Trim();
            if (t.Length == 0) { if (!preview) Cancel(); return; }
            if (Submitted != null) Submitted(t, copyOnly, preview);
        }

        void History(int dir)
        {
            if (history.Count == 0) return;
            if (dir < 0)
            {
                if (histPos == -1) histPos = history.Count;
                histPos = Math.Max(0, histPos - 1);
                Box.Text = history[histPos];
            }
            else
            {
                if (histPos == -1) return;
                histPos++;
                if (histPos >= history.Count) { histPos = -1; Box.Text = ""; }
                else Box.Text = history[histPos];
            }
            Box.SelectionStart = Box.Text.Length;
        }

        void DeleteWord()
        {
            if (Box.SelectionLength > 0) { Box.SelectedText = ""; return; }
            int pos = Box.SelectionStart;
            if (pos == 0) return;
            string t = Box.Text;
            int i = pos;
            while (i > 0 && char.IsWhiteSpace(t[i - 1])) i--;
            while (i > 0 && !char.IsWhiteSpace(t[i - 1])) i--;
            Box.Text = t.Substring(0, i) + t.Substring(pos);
            Box.SelectionStart = i;
        }

        public void AddHistory(string t)
        {
            history.Remove(t);
            history.Add(t);
            if (history.Count > 30) history.RemoveAt(0);
        }

        public void ShowFor(IntPtr target, string headerText, string hintText, string myLang, bool switchLayout)
        {
            Target = target;
            header.Text = headerText;
            header.ForeColor = HeaderColor;
            hint.Text = hintText;
            Box.Text = "";
            Box.ReadOnly = false;
            Busy = false;
            histPos = -1;
            previewSource = previewText = null;
            result.Visible = false;
            Relayout();

            Screen scr;
            try { scr = target != IntPtr.Zero ? Screen.FromHandle(target) : Screen.FromPoint(Cursor.Position); }
            catch { scr = Screen.PrimaryScreen; }
            var wa = scr.WorkingArea;
            Location = DocShots.Offscreen ? new Point(-20000, -20000)
                : new Point(wa.Left + (wa.Width - Width) / 2, wa.Bottom - Height - wa.Height / 8);

            Show();
            if (NoActivate) return;
            Activate();
            Native.SetForegroundWindow(Handle);
            Box.Focus();
            if (switchLayout) SwitchLayout(myLang);
        }

        public bool NoActivate { get; set; }
        protected override bool ShowWithoutActivation { get { return NoActivate; } }

        public void SetStatus(string text, Color color)
        {
            header.Text = text;
            header.ForeColor = color;
        }

        public void ResetStatus(string text) { SetStatus(text, HeaderColor); }

        public void SetBusy(bool busy)
        {
            Busy = busy;
            Box.ReadOnly = busy;
        }

        // Показать перевод, не вставляя его (Tab).
        public void ShowPreview(string source, string translated, string note)
        {
            previewSource = source;
            previewText = translated;
            result.Text = "→ " + translated + (note != null ? "\n(" + note + ")" : "");
            result.ForeColor = Color.FromArgb(150, 225, 165);
            result.Visible = true;
            int bottom = Bottom;
            Relayout();
            if (!DocShots.Offscreen) Top = bottom - Height; // растём вверх, чтобы не уехать за край экрана
        }

        public bool TryGetPreview(string text, out string translated)
        {
            translated = previewText;
            return previewText != null && text == previewSource;
        }

        void SwitchLayout(string lang)
        {
            prevLang = null;
            try
            {
                string two = Lang.Tag(lang).ToLowerInvariant();
                var cur = InputLanguage.CurrentInputLanguage;
                if (cur != null && cur.Culture.TwoLetterISOLanguageName == two) return;
                foreach (InputLanguage il in InputLanguage.InstalledInputLanguages)
                    if (il.Culture.TwoLetterISOLanguageName == two)
                    {
                        prevLang = cur;
                        InputLanguage.CurrentInputLanguage = il;
                        break;
                    }
            }
            catch { }
        }

        void RestoreLayout()
        {
            if (prevLang == null) return;
            try { InputLanguage.CurrentInputLanguage = prevLang; } catch { }
            prevLang = null;
        }

        // Закрыть и вернуть фокус в игру/приложение.
        public void CloseAndReturnFocus()
        {
            Busy = false;
            closingQuietly = true;
            RestoreLayout();
            if (Target != IntPtr.Zero && Native.IsWindow(Target)) Native.SetForegroundWindow(Target);
            Hide();
            closingQuietly = false;
        }

        public void Cancel()
        {
            CloseAndReturnFocus();
            if (Cancelled != null) Cancelled();
        }
    }
}

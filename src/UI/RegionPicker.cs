using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Windows.Forms;

namespace ChatTranslator
{
    // Выбор области чата: затемнённый снимок экрана, область выделяется мышкой.
    // Снимок экрана остаётся только в памяти этого окна и никуда не отправляется.
    class RegionPicker : Form
    {
        readonly Bitmap shot, dim;
        readonly Rectangle vs;
        Point start;
        Rectangle sel;
        bool dragging;
        public Rectangle Result { get; private set; }

        public RegionPicker(Rectangle current)
        {
            vs = SystemInformation.VirtualScreen;
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.Manual;
            ShowInTaskbar = false;
            TopMost = true;
            DoubleBuffered = true;
            KeyPreview = true;
            Cursor = Cursors.Cross;
            Bounds = vs;

            shot = new Bitmap(vs.Width, vs.Height, PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(shot)) g.CopyFromScreen(vs.Location, Point.Empty, vs.Size);
            dim = new Bitmap(shot);
            using (var g = Graphics.FromImage(dim))
            using (var b = new SolidBrush(Color.FromArgb(150, 0, 0, 0)))
                g.FillRectangle(b, 0, 0, dim.Width, dim.Height);

            if (current.Width > 0 && current.Height > 0)
                sel = new Rectangle(current.X - vs.X, current.Y - vs.Y, current.Width, current.Height);
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            Activate();
            Native.SetForegroundWindow(Handle);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.DrawImageUnscaled(dim, 0, 0);
            if (sel.Width > 0 && sel.Height > 0)
            {
                g.DrawImage(shot, sel, sel, GraphicsUnit.Pixel);
                using (var pen = new Pen(Theme.Accent, 2f)) g.DrawRectangle(pen, sel);
                string size = sel.Width + " × " + sel.Height;
                using (var f = new Font("Segoe UI", 9f))
                {
                    var sz = g.MeasureString(size, f);
                    float y = sel.Bottom + 4 + sz.Height > Height ? sel.Top - sz.Height - 4 : sel.Bottom + 4;
                    g.FillRectangle(Brushes.Black, sel.Left, y, sz.Width + 6, sz.Height);
                    g.DrawString(size, f, Brushes.White, sel.Left + 3, y);
                }
            }

            // Подсказка вверху основного экрана.
            var prim = Screen.PrimaryScreen.Bounds;
            string hint = L.T("Выдели мышкой область, где в игре чат (только сами сообщения).  Enter — готово  ·  Esc — отмена",
                              "Drag to select the game's chat area (just the messages).  Enter — done  ·  Esc — cancel");
            using (var f = new Font("Segoe UI", 12f, FontStyle.Bold))
            {
                var sz = g.MeasureString(hint, f);
                float x = prim.X - vs.X + (prim.Width - sz.Width) / 2, y = prim.Y - vs.Y + 30;
                using (var b = new SolidBrush(Color.FromArgb(220, 30, 32, 38)))
                    g.FillRectangle(b, x - 16, y - 10, sz.Width + 32, sz.Height + 20);
                g.DrawString(hint, f, Brushes.White, x, y);
            }
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left) return;
            dragging = true;
            start = e.Location;
            sel = new Rectangle(e.Location, Size.Empty);
            Invalidate();
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            if (!dragging) return;
            sel = Rectangle.FromLTRB(Math.Min(start.X, e.X), Math.Min(start.Y, e.Y), Math.Max(start.X, e.X), Math.Max(start.Y, e.Y));
            Invalidate();
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            if (!dragging) return;
            dragging = false;
            if (sel.Width >= 40 && sel.Height >= 15) Accept();
            else Invalidate();
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == Keys.Escape) { DialogResult = DialogResult.Cancel; Close(); return true; }
            if (keyData == Keys.Enter && sel.Width >= 40 && sel.Height >= 15) { Accept(); return true; }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        void Accept()
        {
            Result = new Rectangle(sel.X + vs.X, sel.Y + vs.Y, sel.Width, sel.Height);
            DialogResult = DialogResult.OK;
            Close();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) { shot.Dispose(); dim.Dispose(); }
            base.Dispose(disposing);
        }
    }
}

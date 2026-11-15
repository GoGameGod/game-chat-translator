using System;
using System.Collections.Generic;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using WinOcr = Windows.Media.Ocr;
using WinImg = Windows.Graphics.Imaging;
using WinFound = Windows.Foundation;
using WinStreams = Windows.Storage.Streams;
using WinGlob = Windows.Globalization;
using SD = System.Drawing;

namespace ChatTranslator
{
    // Встроенное распознавание текста Windows (Windows.Media.Ocr). Работает локально, без интернета.
    static class Ocr
    {
        static readonly Dictionary<string, WinOcr.OcrEngine> engines = new Dictionary<string, WinOcr.OcrEngine>();

        // Языки распознавания, установленные в Windows: тег и название.
        public static List<string[]> Languages()
        {
            var list = new List<string[]>();
            try
            {
                foreach (var l in WinOcr.OcrEngine.AvailableRecognizerLanguages)
                    list.Add(new[] { l.LanguageTag, l.DisplayName });
            }
            catch (Exception ex) { Log.Write("ocr languages: " + ex.Message); }
            return list;
        }

        // Выбор языка: явно заданный → язык собеседников → английский → языки профиля Windows.
        static WinOcr.OcrEngine Engine(string tag, string targetLang)
        {
            var tries = new List<string>();
            if (!string.IsNullOrEmpty(tag)) tries.Add(tag);
            if (!string.IsNullOrEmpty(targetLang)) tries.Add(targetLang);
            tries.Add("en-US");
            tries.Add("en");
            lock (engines)
            {
                foreach (var t in tries)
                {
                    WinOcr.OcrEngine e;
                    if (engines.TryGetValue(t, out e)) return e;
                    try
                    {
                        var lang = new WinGlob.Language(t);
                        if (!WinOcr.OcrEngine.IsLanguageSupported(lang))
                        {
                            // «en» → ищем любой установленный «en-*».
                            string two = t.Split('-')[0].ToLowerInvariant();
                            lang = null;
                            foreach (var l in WinOcr.OcrEngine.AvailableRecognizerLanguages)
                                if (l.LanguageTag.Split('-')[0].ToLowerInvariant() == two) { lang = l; break; }
                        }
                        if (lang == null) continue;
                        e = WinOcr.OcrEngine.TryCreateFromLanguage(lang);
                        if (e != null) { engines[t] = e; return e; }
                    }
                    catch { }
                }
                var p = WinOcr.OcrEngine.TryCreateFromUserProfileLanguages();
                if (p == null) throw new Exception(L.T("в Windows не установлен ни один язык распознавания текста (OCR)",
                                                       "no OCR language is installed in Windows"));
                return p;
            }
        }

        public static string EngineLanguage(string tag, string targetLang)
        {
            try { return Engine(tag, targetLang).RecognizerLanguage.DisplayName; } catch { return "?"; }
        }

        // Распознать строки текста на картинке.
        public static async Task<List<string>> Recognize(SD.Bitmap source, string tag, string targetLang)
        {
            var engine = Engine(tag, targetLang);
            SD.Bitmap bmp = Prepare(source);
            try
            {
                var data = bmp.LockBits(new SD.Rectangle(0, 0, bmp.Width, bmp.Height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
                var bytes = new byte[data.Stride * bmp.Height];
                Marshal.Copy(data.Scan0, bytes, 0, bytes.Length);
                bmp.UnlockBits(data);

                var dw = new WinStreams.DataWriter();
                dw.WriteBytes(bytes);
                WinStreams.IBuffer buf = dw.DetachBuffer();
                var sbmp = WinImg.SoftwareBitmap.CreateCopyFromBuffer(buf, WinImg.BitmapPixelFormat.Bgra8, bmp.Width, bmp.Height,
                    WinImg.BitmapAlphaMode.Premultiplied);
                var res = await AsTask(engine.RecognizeAsync(sbmp));
                var lines = new List<string>();
                foreach (var line in res.Lines)
                {
                    string t = (line.Text ?? "").Trim();
                    if (t.Length > 0) lines.Add(t);
                }
                return lines;
            }
            finally { if (!ReferenceEquals(bmp, source)) bmp.Dispose(); }
        }

        // Мелкий игровой шрифт распознаётся лучше, если увеличить картинку.
        static SD.Bitmap Prepare(SD.Bitmap src)
        {
            int max = (int)WinOcr.OcrEngine.MaxImageDimension;
            float scale = src.Height < 500 ? 2f : 1f;
            if (Math.Max(src.Width, src.Height) * scale > max) scale = (float)max / Math.Max(src.Width, src.Height);
            if (Math.Abs(scale - 1f) < 0.01f && src.PixelFormat == PixelFormat.Format32bppArgb) return src;
            int w = Math.Max(1, (int)(src.Width * scale)), h = Math.Max(1, (int)(src.Height * scale));
            var dst = new SD.Bitmap(w, h, PixelFormat.Format32bppArgb);
            using (var g = SD.Graphics.FromImage(dst))
            {
                g.InterpolationMode = SD.Drawing2D.InterpolationMode.HighQualityBicubic;
                g.DrawImage(src, 0, 0, w, h);
            }
            return dst;
        }

        static Task<T> AsTask<T>(WinFound.IAsyncOperation<T> op)
        {
            var tcs = new TaskCompletionSource<T>();
            op.Completed = (o, status) =>
            {
                if (status == WinFound.AsyncStatus.Completed) tcs.TrySetResult(o.GetResults());
                else if (status == WinFound.AsyncStatus.Error) tcs.TrySetException(o.ErrorCode);
                else tcs.TrySetCanceled();
            };
            return tcs.Task;
        }
    }

    // Захват области экрана (как это делают OBS / Discord) — без доступа к процессу игры.
    static class ScreenGrab
    {
        public static SD.Bitmap Capture(SD.Rectangle r)
        {
            var bmp = new SD.Bitmap(r.Width, r.Height, PixelFormat.Format32bppArgb);
            using (var g = SD.Graphics.FromImage(bmp))
                g.CopyFromScreen(r.Location, SD.Point.Empty, r.Size);
            return bmp;
        }

        // Быстрый отпечаток картинки: если чат не изменился — не тратим время на OCR.
        // dark = картинка почти полностью чёрная (игра в эксклюзивном полноэкранном режиме не даёт себя захватить).
        public static ulong Hash(SD.Bitmap bmp, out bool dark)
        {
            var data = bmp.LockBits(new SD.Rectangle(0, 0, bmp.Width, bmp.Height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            var bytes = new byte[data.Stride * bmp.Height];
            Marshal.Copy(data.Scan0, bytes, 0, bytes.Length);
            bmp.UnlockBits(data);
            ulong h = 14695981039346656037UL;
            long sum = 0;
            int n = 0;
            for (int i = 0; i + 3 < bytes.Length; i += 8)
            {
                h = (h ^ bytes[i]) * 1099511628211UL;
                h = (h ^ bytes[i + 1]) * 1099511628211UL;
                h = (h ^ bytes[i + 2]) * 1099511628211UL;
                sum += bytes[i] + bytes[i + 1] + bytes[i + 2];
                n++;
            }
            dark = n > 0 && sum / (n * 3.0) < 3;
            return h;
        }
    }
}

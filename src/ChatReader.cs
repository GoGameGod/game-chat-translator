using System;
using System.Collections.Generic;
using System.Drawing;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ChatTranslator
{
    class ChatEntry
    {
        public string Original;
        public string Translated;
        public DateTime Time;
    }

    // Периодически снимает область чата, распознаёт текст и переводит только новые строки.
    class ChatReader : IDisposable
    {
        readonly Settings S;
        readonly Timer timer = new Timer();
        readonly List<string> seen = new List<string>();
        bool busy, firstScan, darkWarned;
        ulong lastHash;
        int errors;

        public event Action<List<ChatEntry>> Translated;
        public event Action<string, bool> Status; // текст, это ошибка

        public bool Running { get { return timer.Enabled; } }

        public ChatReader(Settings s)
        {
            S = s;
            timer.Tick += (o, e) => Tick();
        }

        public void Start()
        {
            seen.Clear();
            lastHash = 0;
            firstScan = true;
            darkWarned = false;
            errors = 0;
            timer.Interval = Math.Max(1, S.ChatInterval) * 1000;
            timer.Start();
            Tick();
        }

        public void Stop() { timer.Stop(); }

        public void ApplySettings()
        {
            timer.Interval = Math.Max(1, S.ChatInterval) * 1000;
        }

        async void Tick()
        {
            if (busy || !S.HasChatRegion()) return;
            busy = true;
            try
            {
                Bitmap bmp;
                try { bmp = ScreenGrab.Capture(S.ChatRegion()); }
                catch (Exception ex) { Report(L.T("Не удалось снять экран: ", "Screen capture failed: ") + ex.Message, true); return; }

                bool dark;
                ulong hash = ScreenGrab.Hash(bmp, out dark);
                if (hash == lastHash) { bmp.Dispose(); return; } // чат не изменился
                lastHash = hash;
                if (dark)
                {
                    bmp.Dispose();
                    if (!darkWarned)
                        Report(L.T("Область чата чёрная — игра в эксклюзивном полноэкранном режиме не даёт себя снять. Переключи её в «оконный без рамки».",
                                   "The chat area is black — a game in exclusive fullscreen can't be captured. Switch it to borderless windowed."), true);
                    darkWarned = true;
                    return;
                }

                var snapshot = Settings.Clone(S);
                bool first = firstScan;
                firstScan = false;
                List<ChatEntry> entries = null;
                string err = null;
                try { entries = await Task.Run(() => Process(bmp, snapshot, first)); }
                catch (Exception ex) { err = ex is AggregateException ? ex.InnerException.Message : ex.Message; }
                finally { bmp.Dispose(); }

                if (!timer.Enabled) return; // успели выключить
                if (err != null)
                {
                    Log.Write("chat: " + err);
                    if (++errors <= 3 || errors % 10 == 0) Report(L.T("Ошибка: ", "Error: ") + err, true);
                    return;
                }
                errors = 0;
                if (entries != null && entries.Count > 0 && Translated != null) Translated(entries);
            }
            finally { busy = false; }
        }

        void Report(string text, bool error) { if (Status != null) Status(text, error); }

        // Для служебной проверки (--ocr-test): тот же путь, что и при чтении экрана.
        public List<ChatEntry> TestProcess(Bitmap bmp, Settings s, bool first) { return Process(bmp, s, first).Result; }

        async Task<List<ChatEntry>> Process(Bitmap bmp, Settings s, bool first)
        {
            var lines = await Ocr.Recognize(bmp, s.ChatOcrLang, s.TargetLang);

            var fresh = new List<string>();
            foreach (var line in lines)
            {
                string n = Norm(line);
                if (n.Length < 2) continue;
                if (IsSeen(n)) continue;
                Remember(n);
                if (IsMyLanguage(line, s.MyLang)) continue;
                fresh.Add(line);
            }
            if (first && fresh.Count > 3) fresh = fresh.GetRange(fresh.Count - 3, 3); // при включении — только последние строки
            if (fresh.Count == 0) return null;

            // Одним запросом: строки через перевод строки.
            var r = Translator.Translate(s, string.Join("\n", fresh), s.MyLang, s.Engine);
            string[] parts = r.Text.Replace("\r", "").Split('\n');
            var result = new List<ChatEntry>();
            if (parts.Length == fresh.Count)
            {
                for (int i = 0; i < fresh.Count; i++)
                    if (Norm(parts[i]) != Norm(fresh[i]))
                        result.Add(new ChatEntry { Original = fresh[i], Translated = parts[i].Trim(), Time = DateTime.Now });
            }
            else result.Add(new ChatEntry { Original = string.Join(" / ", fresh), Translated = r.Text.Trim(), Time = DateTime.Now });
            return result;
        }

        // ── защита от повторов: OCR часто чуть по-разному читает одну и ту же строку ──
        static string Norm(string s)
        {
            var sb = new StringBuilder(s.Length);
            foreach (char c in s.ToLowerInvariant()) if (char.IsLetterOrDigit(c)) sb.Append(c);
            return sb.ToString();
        }

        bool IsSeen(string n)
        {
            foreach (var x in seen)
            {
                if (x == n) return true;
                if (n.Length >= 6 && x.Length >= 6 && Similar(x, n) >= 0.8) return true;
            }
            return false;
        }

        void Remember(string n)
        {
            seen.Add(n);
            if (seen.Count > 300) seen.RemoveRange(0, 100);
        }

        static double Similar(string a, string b)
        {
            if (Math.Abs(a.Length - b.Length) > Math.Max(a.Length, b.Length) * 0.3) return 0;
            var prev = new int[b.Length + 1];
            var cur = new int[b.Length + 1];
            for (int j = 0; j <= b.Length; j++) prev[j] = j;
            for (int i = 1; i <= a.Length; i++)
            {
                cur[0] = i;
                for (int j = 1; j <= b.Length; j++)
                {
                    int cost = a[i - 1] == b[j - 1] ? 0 : 1;
                    cur[j] = Math.Min(Math.Min(cur[j - 1] + 1, prev[j] + 1), prev[j - 1] + cost);
                }
                var t = prev; prev = cur; cur = t;
            }
            return 1.0 - (double)prev[b.Length] / Math.Max(a.Length, b.Length);
        }

        // Сообщения уже на моём языке не переводим (для кириллических языков — по доле кириллицы).
        static bool IsMyLanguage(string line, string myLang)
        {
            bool cyr = myLang == "ru" || myLang == "uk" || myLang == "be" || myLang == "kk";
            int letters = 0, c = 0, lat = 0;
            foreach (char ch in line)
            {
                if (!char.IsLetter(ch)) continue;
                letters++;
                if (ch >= 'Ѐ' && ch <= 'ӿ') c++;
                else if (ch < 128) lat++;
            }
            if (letters < 3) return true; // одни цифры/символы
            if (cyr) return c > letters * 0.6;
            return false;
        }

        public void Dispose() { timer.Dispose(); }
    }
}

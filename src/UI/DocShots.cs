using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Windows.Forms;

namespace ChatTranslator
{
    // Рендер скриншотов окон для README/проверки — без показа на экране и без захвата экрана.
    // ChatTranslator.exe --render-docs <папка> [ru|en]
    static class DocShots
    {
        public static bool Offscreen;

        public static void Run(string dir, string lang)
        {
            Offscreen = true;
            L.Init(lang);
            Directory.CreateDirectory(dir);
            string p = lang + "-";

            foreach (var preset in new[] { "safe", "fast", "auto" })
            {
                var s = new Settings { Preset = preset, MyLang = lang == "ru" ? "ru" : "en", TargetLang = lang == "ru" ? "en" : "es" };
                using (var f = new SettingsForm(s, "dota2.exe", Theme.MakeIcon(Theme.Accent), SettingsForm.TabMode))
                    Render(f, Path.Combine(dir, p + "mode-" + preset + ".png"));
            }
            foreach (var engine in new[] { "google", "deepl", "ai" })
            {
                var s = new Settings { Engine = engine };
                using (var f = new SettingsForm(s, null, Theme.MakeIcon(Theme.Accent), SettingsForm.TabEngine))
                    Render(f, Path.Combine(dir, p + "engine-" + engine + ".png"));
            }
            var cs = new Settings { ChatX = 20, ChatY = 820, ChatW = 520, ChatH = 160 };
            using (var f = new SettingsForm(cs, null, Theme.MakeIcon(Theme.Accent), SettingsForm.TabChat))
                Render(f, Path.Combine(dir, p + "chat.png"));
            using (var ov = new ChatOverlay())
            {
                ov.Begin(new Rectangle(0, 0, 520, 160), true, Settings.DefChat);
                ov.Add(new System.Collections.Generic.List<ChatEntry> {
                    new ChatEntry { Original = "[All] Enemy: ez mid, ur so bad", Translated = lang == "ru" ? "[Все] Enemy: изи мид, ты такой слабый" : "[All] Enemy: ez mid, ur so bad", Time = DateTime.Now },
                    new ChatEntry { Original = "[Team] xX_Sniper: need heal pls", Translated = lang == "ru" ? "[Команда] xX_Sniper: нужен хил, пожалуйста" : "[Team] xX_Sniper: need heal pls", Time = DateTime.Now },
                    new ChatEntry { Original = "[All] Player1: wp guys, push top now", Translated = lang == "ru" ? "[Все] Player1: хорошо сыграли, пушим верх" : "[All] Player1: well played, push top now", Time = DateTime.Now },
                });
                Render(ov, Path.Combine(dir, p + "overlay.png"));
            }
            using (var f = new SettingsForm(new Settings(), null, Theme.MakeIcon(Theme.Accent), SettingsForm.TabKeys))
                Render(f, Path.Combine(dir, p + "keys.png"));
            using (var f = new SettingsForm(new Settings(), null, Theme.MakeIcon(Theme.Accent), SettingsForm.TabGeneral))
                Render(f, Path.Combine(dir, p + "general.png"));

            using (var pop = new InputPopup())
            {
                pop.NoActivate = true;
                pop.ShowFor(IntPtr.Zero,
                    "RU → EN   ·   Claude   ·   " + L.T("быстрый: вставлю в чат", "fast: I'll paste it into the chat"),
                    L.T("Enter — перевести   ·   Tab — проверить   ·   Shift+Enter — в буфер   ·   F2 — сервис   ·   ↑↓ — история   ·   Esc",
                        "Enter — translate   ·   Tab — preview   ·   Shift+Enter — copy only   ·   F2 — service   ·   ↑↓ — history   ·   Esc"),
                    "ru", false);
                pop.Box.Text = lang == "ru" ? "го в мид, я прикрою, только не тупи" : "go mid, i'll cover you";
                pop.ShowPreview(pop.Box.Text, lang == "ru" ? "go mid, i got you, just don't throw" : "ve al medio, yo te cubro", null);
                pop.SetStatus(L.T("Перевод готов: Enter — вставить. Можно поправить текст и снова нажать Tab.",
                                  "Translation ready: Enter to use it. You can edit the text and press Tab again."), Toast.Ok);
                Render(pop, Path.Combine(dir, p + "popup.png"));
            }
            using (var t = new Toast())
            {
                t.ShowMessage(L.T("Скопировано → Ctrl+V:\n", "Copied → Ctrl+V:\n") + "go mid, i got you, just don't throw", Toast.Ok, 1000, IntPtr.Zero);
                Render(t, Path.Combine(dir, p + "toast.png"));
            }
        }

        static void Render(Form f, string path)
        {
            if (!f.Visible)
            {
                f.StartPosition = FormStartPosition.Manual;
                f.Location = new Point(-20000, -20000);
                var nf = f as INoActivate;
                if (nf != null) nf.NoActivate = true;
                f.Show();
            }
            else f.Location = new Point(-20000, -20000);
            for (int i = 0; i < 5; i++) { Application.DoEvents(); System.Threading.Thread.Sleep(30); }
            using (var bmp = new Bitmap(f.Width, f.Height))
            {
                f.DrawToBitmap(bmp, new Rectangle(0, 0, f.Width, f.Height));
                bmp.Save(path, ImageFormat.Png);
            }
            f.Hide();
        }
    }

    interface INoActivate { bool NoActivate { get; set; } }
}

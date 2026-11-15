using System;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using System.Windows.Forms;

[assembly: System.Reflection.AssemblyTitle("Game Chat Translator")]
[assembly: System.Reflection.AssemblyDescription("Translate your chat messages in games and messengers")]
[assembly: System.Reflection.AssemblyProduct("Game Chat Translator")]
[assembly: System.Reflection.AssemblyCopyright("MIT License")]
[assembly: System.Reflection.AssemblyVersion("1.0.0.0")]
[assembly: System.Reflection.AssemblyFileVersion("1.0.0.0")]
[assembly: System.Reflection.AssemblyInformationalVersion("1.0.0")]

namespace ChatTranslator
{
    static class AppInfo
    {
        public const string Version = "1.0.0";
        public const string Repo = "GoGameGod/game-chat-translator";
        public const string RepoUrl = "https://github.com/" + Repo;
        public static string Name { get { return L.T("Чат-переводчик", "Chat Translator"); } }
    }

    static class Program
    {
        [STAThread]
        static void Main(string[] args)
        {
            try { ServicePointManager.SecurityProtocol = (SecurityProtocolType)(3072 | 12288); }
            catch { try { ServicePointManager.SecurityProtocol = (SecurityProtocolType)3072; } catch { } }

            // Служебная проверка перевода: ChatTranslator.exe --selftest "текст" out.txt
            if (args.Length >= 3 && args[0] == "--selftest")
            {
                string res;
                try { res = Translator.Google(args[1], "en"); } catch (Exception ex) { res = "ERROR: " + ex.Message; }
                File.WriteAllText(args[2], res, Encoding.UTF8);
                return;
            }

            // Рендер скриншотов для документации: ChatTranslator.exe --render-docs <папка> [ru|en]
            if (args.Length >= 2 && args[0] == "--render-docs")
            {
                try { Native.SetProcessDPIAware(); } catch { }
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                DocShots.Run(args[1], args.Length > 2 ? args[2] : "ru");
                return;
            }

            var settings = Settings.Load();
            L.Init(settings.UiLang);

            bool restart = Array.IndexOf(args, "--restart") >= 0;
            int openTab = restart ? SettingsForm.TabGeneral : (Array.IndexOf(args, "--settings") >= 0 ? SettingsForm.TabMode : -1);

            using (var mutex = new Mutex(false, "ChatTranslator.SingleInstance.v1"))
            {
                bool got;
                try { got = mutex.WaitOne(restart ? 8000 : 0); }
                catch (AbandonedMutexException) { got = true; }
                if (!got)
                {
                    MessageBox.Show(L.T("Чат-переводчик уже запущен — его значок в трее (возле часов).",
                                        "Chat Translator is already running — look for its icon in the tray (next to the clock)."),
                        AppInfo.Name, MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }
                try
                {
                    try { Native.SetProcessDPIAware(); } catch { }
                    Application.EnableVisualStyles();
                    Application.SetCompatibleTextRenderingDefault(false);
                    Application.ThreadException += (s, e) => Crash(e.Exception);
                    AppDomain.CurrentDomain.UnhandledException += (s, e) => Crash(e.ExceptionObject as Exception);
                    Application.Run(new TrayApp(settings, openTab));
                }
                finally { mutex.ReleaseMutex(); }
            }
        }

        static void Crash(Exception ex)
        {
            Log.Write("CRASH: " + ex);
            MessageBox.Show(
                L.T("Произошла ошибка: ", "An error occurred: ") + (ex != null ? ex.Message : "?") +
                L.T("\nПодробности — в log.txt в папке настроек.", "\nDetails are in log.txt in the settings folder."),
                AppInfo.Name, MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}

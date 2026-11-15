using System;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Web.Script.Serialization;

namespace ChatTranslator
{
    // Язык интерфейса: русский или английский.
    static class L
    {
        public static bool Ru { get; private set; }

        public static void Init(string uiLang)
        {
            if (uiLang == "ru") Ru = true;
            else if (uiLang == "en") Ru = false;
            else Ru = IsRuCulture();
        }

        public static bool IsRuCulture()
        {
            string two = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
            return two == "ru" || two == "uk" || two == "be" || two == "kk";
        }

        public static string T(string ru, string en) { return Ru ? ru : en; }
    }

    // Языки перевода.
    static class Lang
    {
        // код, название по-английски (для ИИ), по-русски, по-английски (для интерфейса)
        public static readonly string[][] All = new string[][] {
            new [] {"en", "English", "Английский", "English"},
            new [] {"ru", "Russian", "Русский", "Russian"},
            new [] {"uk", "Ukrainian", "Украинский", "Ukrainian"},
            new [] {"de", "German", "Немецкий", "German"},
            new [] {"fr", "French", "Французский", "French"},
            new [] {"es", "Spanish", "Испанский", "Spanish"},
            new [] {"pt", "Portuguese", "Португальский", "Portuguese"},
            new [] {"it", "Italian", "Итальянский", "Italian"},
            new [] {"pl", "Polish", "Польский", "Polish"},
            new [] {"tr", "Turkish", "Турецкий", "Turkish"},
            new [] {"cs", "Czech", "Чешский", "Czech"},
            new [] {"nl", "Dutch", "Нидерландский", "Dutch"},
            new [] {"sv", "Swedish", "Шведский", "Swedish"},
            new [] {"fi", "Finnish", "Финский", "Finnish"},
            new [] {"kk", "Kazakh", "Казахский", "Kazakh"},
            new [] {"be", "Belarusian", "Белорусский", "Belarusian"},
            new [] {"zh-CN", "Chinese (Simplified)", "Китайский (упрощ.)", "Chinese (Simplified)"},
            new [] {"zh-TW", "Chinese (Traditional)", "Китайский (традиц.)", "Chinese (Traditional)"},
            new [] {"ja", "Japanese", "Японский", "Japanese"},
            new [] {"ko", "Korean", "Корейский", "Korean"},
            new [] {"ar", "Arabic", "Арабский", "Arabic"},
            new [] {"hi", "Hindi", "Хинди", "Hindi"},
            new [] {"id", "Indonesian", "Индонезийский", "Indonesian"},
            new [] {"vi", "Vietnamese", "Вьетнамский", "Vietnamese"},
            new [] {"th", "Thai", "Тайский", "Thai"},
        };

        public static string English(string code)
        {
            foreach (var l in All) if (string.Equals(l[0], code, StringComparison.OrdinalIgnoreCase)) return l[1];
            return code;
        }

        public static string Display(int i) { return All[i][0] + " — " + (L.Ru ? All[i][2] : All[i][3]); }

        public static string Tag(string code)
        {
            if (string.IsNullOrEmpty(code)) return "?";
            int i = code.IndexOf('-');
            return (i > 0 ? code.Substring(0, i) : code).ToUpperInvariant();
        }

        public static int IndexOf(string code)
        {
            for (int i = 0; i < All.Length; i++)
                if (string.Equals(All[i][0], code, StringComparison.OrdinalIgnoreCase)) return i;
            return 0;
        }
    }

    public class Settings
    {
        public string UiLang { get; set; }        // "auto" | "ru" | "en"
        public string Preset { get; set; }        // "safe" | "fast" | "auto"
        public string AutoDefault { get; set; }   // режим для программ не из списков (пресет «Авто»)
        public bool AutoSend { get; set; }
        public bool RestoreClipboard { get; set; }
        public string SafeApps { get; set; }
        public string FastApps { get; set; }
        public string Engine { get; set; }        // "google" | "deepl" | "ai"
        public string DeepLKey { get; set; }      // зашифровано DPAPI
        public string AiProvider { get; set; }    // "anthropic" | "openai"
        public string AnthropicKey { get; set; }  // зашифровано DPAPI
        public string AnthropicModel { get; set; }
        public string OpenAiUrl { get; set; }
        public string OpenAiKey { get; set; }     // зашифровано DPAPI
        public string OpenAiModel { get; set; }
        public string AiStyle { get; set; }
        public bool FallbackGoogle { get; set; }
        public string TargetLang { get; set; }
        public string MyLang { get; set; }
        public string HkPopup { get; set; }
        public string HkInline { get; set; }
        public string HkReverse { get; set; }
        public string HkToggle { get; set; }
        public bool Sound { get; set; }
        public bool SwitchLayout { get; set; }
        public bool Enabled { get; set; }

        public const string DefPopup = "Ctrl+Shift+Space", DefInline = "Ctrl+Shift+Enter", DefReverse = "Ctrl+Alt+R";

        public Settings()
        {
            UiLang = "auto";
            Preset = "safe";
            AutoDefault = "safe";
            AutoSend = false;
            RestoreClipboard = true;
            SafeApps = "";
            FastApps = "";
            Engine = "google";
            DeepLKey = "";
            AiProvider = "anthropic";
            AnthropicKey = "";
            AnthropicModel = "claude-opus-5-5";
            OpenAiUrl = "https://api.openai.com/v1";
            OpenAiKey = "";
            OpenAiModel = "";
            AiStyle = "";
            FallbackGoogle = true;
            string sys = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
            MyLang = Lang.IndexOf(sys) > 0 || sys == "en" ? sys : "en";
            TargetLang = MyLang == "en" ? "es" : "en";
            HkPopup = DefPopup;
            HkInline = DefInline;
            HkReverse = DefReverse;
            HkToggle = "";
            Sound = true;
            SwitchLayout = true;
            Enabled = true;
        }

        public static readonly string Dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ChatTranslator");
        public static string FilePath { get { return Path.Combine(Dir, "settings.json"); } }

        public static Settings Load()
        {
            try
            {
                if (File.Exists(FilePath))
                {
                    var s = new JavaScriptSerializer().Deserialize<Settings>(File.ReadAllText(FilePath, Encoding.UTF8));
                    if (s != null) { s.FixInvalid(); return s; }
                }
            }
            catch (Exception ex) { Log.Write("settings load: " + ex.Message); }
            return new Settings();
        }

        public void Save()
        {
            try
            {
                Directory.CreateDirectory(Dir);
                File.WriteAllText(FilePath, new JavaScriptSerializer().Serialize(this), Encoding.UTF8);
            }
            catch (Exception ex) { Log.Write("settings save: " + ex.Message); }
        }

        public static Settings Clone(Settings s)
        {
            var js = new JavaScriptSerializer();
            return js.Deserialize<Settings>(js.Serialize(s));
        }

        void FixInvalid()
        {
            var d = new Settings();
            if (UiLang != "ru" && UiLang != "en") UiLang = "auto";
            if (Preset != "safe" && Preset != "fast" && Preset != "auto") Preset = d.Preset;
            if (AutoDefault != "safe" && AutoDefault != "fast") AutoDefault = d.AutoDefault;
            if (Engine != "google" && Engine != "deepl" && Engine != "ai") Engine = d.Engine;
            if (AiProvider != "anthropic" && AiProvider != "openai") AiProvider = d.AiProvider;
            if (SafeApps == null) SafeApps = "";
            if (FastApps == null) FastApps = "";
            if (DeepLKey == null) DeepLKey = "";
            if (AnthropicKey == null) AnthropicKey = "";
            if (string.IsNullOrWhiteSpace(AnthropicModel)) AnthropicModel = d.AnthropicModel;
            if (OpenAiUrl == null) OpenAiUrl = d.OpenAiUrl;
            if (OpenAiKey == null) OpenAiKey = "";
            if (OpenAiModel == null) OpenAiModel = "";
            if (AiStyle == null) AiStyle = "";
            if (string.IsNullOrEmpty(TargetLang)) TargetLang = d.TargetLang;
            if (string.IsNullOrEmpty(MyLang)) MyLang = d.MyLang;
            if (HkPopup == null) HkPopup = d.HkPopup;
            if (HkInline == null) HkInline = d.HkInline;
            if (HkReverse == null) HkReverse = d.HkReverse;
            if (HkToggle == null) HkToggle = "";
        }

        public string GetDeepLKey() { return Unprotect(DeepLKey); }
        public void SetDeepLKey(string v) { DeepLKey = Protect(v); }
        public string GetAnthropicKey() { return Unprotect(AnthropicKey); }
        public void SetAnthropicKey(string v) { AnthropicKey = Protect(v); }
        public string GetOpenAiKey() { return Unprotect(OpenAiKey); }
        public void SetOpenAiKey(string v) { OpenAiKey = Protect(v); }

        static readonly byte[] Entropy = Encoding.UTF8.GetBytes("ChatTranslator.v1");

        static string Protect(string plain)
        {
            plain = (plain ?? "").Trim();
            if (plain.Length == 0) return "";
            var data = ProtectedData.Protect(Encoding.UTF8.GetBytes(plain), Entropy, DataProtectionScope.CurrentUser);
            return Convert.ToBase64String(data);
        }

        static string Unprotect(string enc)
        {
            if (string.IsNullOrEmpty(enc)) return "";
            try
            {
                return Encoding.UTF8.GetString(ProtectedData.Unprotect(
                    Convert.FromBase64String(enc), Entropy, DataProtectionScope.CurrentUser));
            }
            catch { return ""; }
        }
    }
}

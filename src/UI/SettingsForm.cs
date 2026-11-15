using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ChatTranslator
{
    class SettingsForm : Form, INoActivate
    {
        public const int TabMode = 0, TabEngine = 1, TabKeys = 2, TabGeneral = 3;

        public bool NoActivate { get; set; }
        protected override bool ShowWithoutActivation { get { return NoActivate; } }

        readonly Settings S;
        readonly string lastExe;
        readonly string initialUiLang;
        public bool RestartRequested { get; private set; }

        TabControl tabs;

        // «Режим»
        PresetCard cardSafe, cardFast, cardAuto;
        GuideView guide;
        Label lblModeSection;
        Panel pnlAuto;
        RadioButton rbDefSafe, rbDefFast;
        TextBox tbSafe, tbFast;
        CheckBox chAutoSend, chRestoreClip, chSound;
        string preset = "safe";

        // «Переводчик»
        PresetCard cardGoogle, cardDeepL, cardAi;
        string engine = "google";
        ComboBox cbTarget, cbMy;
        Label lblEngineSection;
        Control pnlGoogle, pnlDeepL, pnlAi;
        TextBox tbDeepL, tbAnthKey, tbOaUrl, tbOaKey, tbOaModel, tbStyle;
        ComboBox cbProvider, cbAnthModel, cbOaService;
        Control pnlAnth, pnlOa;
        LinkLabel lnkOaKey;
        CheckBox chFallback;
        Label lblTest;
        Button btnTest;

        // «Горячие клавиши»
        HotkeyBox hkPopup, hkInline, hkReverse, hkToggle;

        // «Общее»
        ComboBox cbUiLang;
        CheckBox chLayout, chAutostart;
        LinkLabel lblUpdate;

        // Сервисы, совместимые с OpenAI API: название, адрес, где взять ключ.
        static readonly string[][] OaServices = new string[][] {
            new [] {"OpenAI", "https://api.openai.com/v1", "https://platform.openai.com/api-keys"},
            new [] {"OpenRouter", "https://openrouter.ai/api/v1", "https://openrouter.ai/keys"},
            new [] {"DeepSeek", "https://api.deepseek.com/v1", "https://platform.deepseek.com/api_keys"},
            new [] {"Groq", "https://api.groq.com/openai/v1", "https://console.groq.com/keys"},
            new [] {"LM Studio", "http://localhost:1234/v1", ""},
            new [] {"Ollama", "http://localhost:11434/v1", ""},
        };

        public SettingsForm(Settings s, string lastExe, Icon icon, int tab)
        {
            S = s;
            this.lastExe = lastExe;
            initialUiLang = s.UiLang;
            SuspendLayout();
            Text = AppInfo.Name + " — " + L.T("настройки", "settings");
            Font = new Font("Segoe UI", 9f);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            Icon = icon;
            ClientSize = new Size(640, 660);

            tabs = new TabControl { Location = new Point(10, 10), Size = new Size(620, 600) };
            tabs.TabPages.Add(BuildMode());
            tabs.TabPages.Add(BuildEngine());
            tabs.TabPages.Add(BuildKeys());
            tabs.TabPages.Add(BuildGeneral());
            Controls.Add(tabs);

            // Инструкция обновляется сразу при изменении клавиш и галочек.
            EventHandler refresh = (o, e) => RefreshGuide();
            hkPopup.TextChanged += refresh;
            hkInline.TextChanged += refresh;
            hkReverse.TextChanged += refresh;
            chAutoSend.CheckedChanged += refresh;
            chSound.CheckedChanged += refresh;
            rbDefFast.CheckedChanged += refresh;
            cbMy.SelectedIndexChanged += refresh;

            var btnSave = new Button { Text = L.T("Сохранить", "Save"), Location = new Point(430, 620), Size = new Size(95, 30) };
            var btnCancel = new Button { Text = L.T("Отмена", "Cancel"), Location = new Point(535, 620), Size = new Size(95, 30) };
            btnSave.Click += (o, e) => SaveAndClose();
            btnCancel.Click += (o, e) => Close();
            Controls.Add(btnSave);
            Controls.Add(btnCancel);
            CancelButton = btnCancel;

            AutoScaleDimensions = new SizeF(96F, 96F);
            AutoScaleMode = AutoScaleMode.Dpi;
            ResumeLayout(false);
            LoadFrom(S);
            if (tab >= 0 && tab < tabs.TabPages.Count) tabs.SelectedIndex = tab;
        }

        public void SelectTab(int tab)
        {
            if (tab >= 0 && tab < tabs.TabPages.Count) tabs.SelectedIndex = tab;
        }

        // ───────────── Вкладка «Режим» ─────────────
        TabPage BuildMode()
        {
            var p = new TabPage(L.T("Режим", "Mode")) { UseVisualStyleBackColor = true };
            var flow = Ui.Flow();
            p.Controls.Add(flow);

            flow.Controls.Add(Ui.Intro(L.T("Выбери режим — ниже появится инструкция и настройки именно для него:",
                                           "Pick a mode — its guide and settings appear below:")));
            cardSafe = new PresetCard(L.T("Безопасный", "Safe"),
                L.T("Онлайн-игры с античитом. Перевод в буфер — вставляешь сам Ctrl+V", "Online games with anti-cheat. Translation goes to clipboard, you paste it"));
            cardFast = new PresetCard(L.T("Быстрый", "Fast"),
                L.T("Игры без строгого античита, чаты, браузер. Вставляет перевод сам", "Games without strict anti-cheat, chats, browser. Pastes for you"));
            cardAuto = new PresetCard(L.T("Авто", "Auto"),
                L.T("Свой режим для каждой игры и программы — по спискам", "Its own mode for each game and app, by lists"));
            cardSafe.Picked += (o, e) => SelectPreset("safe");
            cardFast.Picked += (o, e) => SelectPreset("fast");
            cardAuto.Picked += (o, e) => SelectPreset("auto");
            flow.Controls.Add(Ui.Line(cardSafe, cardFast, cardAuto));

            flow.Controls.Add(Ui.Section(L.T("Как пользоваться", "How to use")));
            guide = new GuideView(Ui.W);
            guide.Resolve = ResolveToken;
            guide.LinkClicked += OnGuideLink;
            flow.Controls.Add(guide);
            flow.Controls.Add(Ui.Note(L.T("Синие клавиши кликабельны — откроют их настройку.", "Blue hotkeys are clickable — they open their settings.")));

            lblModeSection = Ui.Section("");
            flow.Controls.Add(lblModeSection);

            // Только для «Авто»: списки программ.
            pnlAuto = new Panel { Size = new Size(Ui.W, 186), Margin = new Padding(0) };
            AddLabel(pnlAuto, L.T("Для программ не из списков:", "Apps not in the lists:"), 8, 6, 190);
            rbDefSafe = new RadioButton { Text = L.T("безопасный", "safe"), Location = new Point(200, 4), AutoSize = true };
            rbDefFast = new RadioButton { Text = L.T("быстрый", "fast"), Location = new Point(310, 4), AutoSize = true };
            pnlAuto.Controls.Add(rbDefSafe);
            pnlAuto.Controls.Add(rbDefFast);
            AddLabel(pnlAuto, L.T("Безопасный режим в программах (имена .exe через запятую):", "Safe mode in these apps (.exe names, comma-separated):"), 8, 34, 550);
            tbSafe = new TextBox { Location = new Point(10, 54), Size = new Size(550, 23) };
            pnlAuto.Controls.Add(tbSafe);
            AddLabel(pnlAuto, L.T("Быстрый режим в программах:", "Fast mode in these apps:"), 8, 86, 550);
            tbFast = new TextBox { Location = new Point(10, 106), Size = new Size(550, 23) };
            pnlAuto.Controls.Add(tbFast);
            if (lastExe != null)
            {
                var l = AddLabel(pnlAuto, L.T("Последняя программа: ", "Last app: ") + lastExe, 8, 146, 320);
                l.Font = new Font("Segoe UI", 9f, FontStyle.Bold);
                var b1 = new Button { Text = L.T("→ в безопасные", "→ safe"), Location = new Point(338, 142), Size = new Size(110, 26) };
                var b2 = new Button { Text = L.T("→ в быстрые", "→ fast"), Location = new Point(452, 142), Size = new Size(108, 26) };
                b1.Click += (o, e) => { tbFast.Text = Win.RemoveFrom(tbFast.Text, lastExe); tbSafe.Text = Win.AddTo(tbSafe.Text, lastExe); };
                b2.Click += (o, e) => { tbSafe.Text = Win.RemoveFrom(tbSafe.Text, lastExe); tbFast.Text = Win.AddTo(tbFast.Text, lastExe); };
                pnlAuto.Controls.Add(b1);
                pnlAuto.Controls.Add(b2);
            }
            else
            {
                var l = AddLabel(pnlAuto, L.T("Нажми в игре горячую клавишу переводчика — здесь появится кнопка, чтобы добавить игру в список одним кликом.",
                                              "Press a translator hotkey in a game — a button to add that game in one click will appear here."), 8, 140, 550);
                l.Height = 36;
                l.ForeColor = Theme.Gray;
            }
            flow.Controls.Add(pnlAuto);

            chAutoSend = Ui.Check("");
            chRestoreClip = Ui.Check(L.T("Возвращать мой буфер обмена после вставки", "Restore my clipboard after pasting"));
            chSound = Ui.Check("");
            flow.Controls.Add(chAutoSend);
            flow.Controls.Add(chRestoreClip);
            flow.Controls.Add(chSound);
            return p;
        }

        static Label AddLabel(Control parent, string text, int x, int y, int w)
        {
            var l = new Label { Text = text, Location = new Point(x, y), Size = new Size(w, 18), AutoEllipsis = true };
            parent.Controls.Add(l);
            return l;
        }

        string ResolveToken(string token)
        {
            string v = null;
            if (token == "popup") v = hkPopup.Text;
            else if (token == "inline") v = hkInline.Text;
            else if (token == "reverse") v = hkReverse.Text;
            else return token;
            return string.IsNullOrWhiteSpace(v) ? L.T("[клавиша не задана]", "[hotkey not set]") : v;
        }

        void OnGuideLink(string token)
        {
            HotkeyBox box = token == "popup" ? hkPopup : token == "inline" ? hkInline : token == "reverse" ? hkReverse : null;
            if (box == null) return;
            tabs.SelectedIndex = TabKeys;
            box.Focus();
            box.Flash();
        }

        void SelectPreset(string pr)
        {
            preset = pr;
            cardSafe.Selected = pr == "safe";
            cardFast.Selected = pr == "fast";
            cardAuto.Selected = pr == "auto";
            pnlAuto.Visible = pr == "auto";
            chAutoSend.Visible = pr != "safe";
            chRestoreClip.Visible = pr != "safe";
            chSound.Visible = pr != "fast";
            chAutoSend.Text = pr == "auto"
                ? L.T("Сразу отправлять сообщение (Enter) — в «быстрых» программах", "Send the message right away (Enter) — in \"fast\" apps")
                : L.T("Сразу отправлять сообщение (нажимать Enter после вставки)", "Send the message right away (press Enter after pasting)");
            chSound.Text = pr == "auto"
                ? L.T("Звуковой сигнал, когда перевод готов — в «безопасных» программах", "Beep when the translation is ready — in \"safe\" apps")
                : L.T("Звуковой сигнал, когда перевод готов к вставке", "Beep when the translation is ready to paste");
            lblModeSection.Text = L.T("Настройки режима «", "\"") + Help.Title(pr) + L.T("»", "\" mode settings");
            RefreshGuide();
        }

        void RefreshGuide()
        {
            if (guide == null || hkPopup == null || cbMy == null) return;
            string my = Lang.All[Math.Max(0, cbMy.SelectedIndex)][0];
            guide.Render(Help.For(preset, chAutoSend.Checked, rbDefFast.Checked ? "fast" : "safe", chSound.Checked, my));
        }

        // ───────────── Вкладка «Переводчик» ─────────────
        TabPage BuildEngine()
        {
            var p = new TabPage(L.T("Переводчик", "Translator")) { UseVisualStyleBackColor = true };
            var flow = Ui.Flow();
            p.Controls.Add(flow);

            flow.Controls.Add(Ui.Intro(L.T("Чем переводить:", "Translate with:")));
            cardGoogle = new PresetCard("Google", L.T("Бесплатно, без ключа. Работает сразу", "Free, no key. Works right away"));
            cardDeepL = new PresetCard("DeepL", L.T("Качественный перевод. Бесплатный ключ: 500 000 символов в месяц", "High quality. Free key: 500,000 characters a month"));
            cardAi = new PresetCard(L.T("ИИ", "AI"), L.T("Понимает сленг и пишет как живой игрок. Нужен API-ключ", "Gets slang, writes like a real player. Needs an API key"));
            cardGoogle.Picked += (o, e) => SelectEngine("google");
            cardDeepL.Picked += (o, e) => SelectEngine("deepl");
            cardAi.Picked += (o, e) => SelectEngine("ai");
            flow.Controls.Add(Ui.Line(cardGoogle, cardDeepL, cardAi));

            flow.Controls.Add(Ui.Section(L.T("Языки", "Languages")));
            cbTarget = new ComboBox { Width = 300, DropDownStyle = ComboBoxStyle.DropDownList, MaxDropDownItems = 15 };
            cbMy = new ComboBox { Width = 300, DropDownStyle = ComboBoxStyle.DropDownList, MaxDropDownItems = 15 };
            for (int i = 0; i < Lang.All.Length; i++) { cbTarget.Items.Add(Lang.Display(i)); cbMy.Items.Add(Lang.Display(i)); }
            flow.Controls.Add(Ui.Row(L.T("Переводить на:", "Translate into:"), cbTarget, 250));
            flow.Controls.Add(Ui.Row(L.T("Мой язык (на него — чужие сообщения):", "My language (for incoming messages):"), cbMy, 250));
            flow.Controls.Add(Ui.Note(L.T("Язык собеседников можно быстро сменить в меню значка в трее.", "You can quickly switch the target language from the tray icon menu.")));

            lblEngineSection = Ui.Section("");
            flow.Controls.Add(lblEngineSection);

            // Google
            pnlGoogle = Ui.Note(L.T("Ничего настраивать не нужно. Это бесплатный веб-доступ Google Translate: сленг иногда переводится дословно, а при очень частом использовании Google может ненадолго ограничить запросы.",
                                    "Nothing to set up. This is the free web access to Google Translate: slang is sometimes translated literally, and with very heavy use Google may briefly rate-limit requests."));
            flow.Controls.Add(pnlGoogle);

            // DeepL
            var dl = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, AutoSize = true, WrapContents = false, Margin = new Padding(0) };
            tbDeepL = new TextBox { Width = 330, UseSystemPasswordChar = true };
            dl.Controls.Add(Ui.Row(L.T("Ключ DeepL API:", "DeepL API key:"), tbDeepL));
            dl.Controls.Add(Ui.Link(L.T("Получить бесплатный ключ (DeepL API Free) →", "Get a free key (DeepL API Free) →"), "https://www.deepl.com/pro-api"));
            dl.Controls.Add(Ui.Note(L.T("Бесплатный ключ заканчивается на «:fx». Если какой-то язык DeepL не поддерживает — выручит Google (галочка ниже).",
                                        "Free keys end with \":fx\". If DeepL doesn't support a language, Google steps in (checkbox below).")));
            pnlDeepL = dl;
            flow.Controls.Add(dl);

            // ИИ
            var ai = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, AutoSize = true, WrapContents = false, Margin = new Padding(0) };
            cbProvider = new ComboBox { Width = 380, DropDownStyle = ComboBoxStyle.DropDownList };
            cbProvider.Items.Add("Anthropic (Claude)");
            cbProvider.Items.Add(L.T("OpenAI-совместимый (OpenAI, OpenRouter, DeepSeek, Groq, локальные)", "OpenAI-compatible (OpenAI, OpenRouter, DeepSeek, Groq, local)"));
            ai.Controls.Add(Ui.Row(L.T("Провайдер:", "Provider:"), cbProvider));

            var anth = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, AutoSize = true, WrapContents = false, Margin = new Padding(0) };
            tbAnthKey = new TextBox { Width = 380, UseSystemPasswordChar = true };
            cbAnthModel = new ComboBox { Width = 380, DropDownStyle = ComboBoxStyle.DropDown };
            cbAnthModel.Items.AddRange(new object[] { "claude-opus-5-5", "claude-sonnet-5-5", "claude-haiku-5-5" });
            anth.Controls.Add(Ui.Row(L.T("API-ключ:", "API key:"), tbAnthKey));
            anth.Controls.Add(Ui.Link(L.T("Получить ключ Anthropic →", "Get an Anthropic key →"), "https://console.anthropic.com/settings/keys"));
            anth.Controls.Add(Ui.Row(L.T("Модель:", "Model:"), cbAnthModel));
            anth.Controls.Add(Ui.Note(L.T("claude-opus-5-5 — самая умная. Для коротких сообщений хватит claude-haiku-5-5: она в десятки раз дешевле.",
                                          "claude-opus-5-5 is the smartest. For short chat messages claude-haiku-5-5 is plenty and dozens of times cheaper.")));
            pnlAnth = anth;
            ai.Controls.Add(anth);

            var oa = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, AutoSize = true, WrapContents = false, Margin = new Padding(0) };
            cbOaService = new ComboBox { Width = 380, DropDownStyle = ComboBoxStyle.DropDownList };
            foreach (var sv in OaServices)
                cbOaService.Items.Add(sv[0] + (sv[2].Length == 0 ? L.T(" (локально)", " (local)") : ""));
            cbOaService.Items.Add(L.T("Другой", "Other"));
            tbOaUrl = new TextBox { Width = 380 };
            tbOaKey = new TextBox { Width = 380, UseSystemPasswordChar = true };
            tbOaModel = new TextBox { Width = 380 };
            lnkOaKey = Ui.Link(L.T("Получить ключ →", "Get a key →"), "");
            lnkOaKey.LinkClicked += (o, e) =>
            {
                int i = cbOaService.SelectedIndex;
                if (i >= 0 && i < OaServices.Length && OaServices[i][2].Length > 0) Ui.OpenUrl(OaServices[i][2]);
            };
            oa.Controls.Add(Ui.Row(L.T("Сервис:", "Service:"), cbOaService));
            oa.Controls.Add(Ui.Row(L.T("Адрес API:", "API address:"), tbOaUrl));
            oa.Controls.Add(Ui.Row(L.T("API-ключ:", "API key:"), tbOaKey));
            oa.Controls.Add(lnkOaKey);
            oa.Controls.Add(Ui.Row(L.T("Модель:", "Model:"), tbOaModel));
            oa.Controls.Add(Ui.Note(L.T("Название модели смотри в документации сервиса (например, у DeepSeek — deepseek-chat). Для локальных LM Studio / Ollama ключ не нужен.",
                                        "Find the model name in the service docs (e.g. deepseek-chat for DeepSeek). Local LM Studio / Ollama need no key.")));
            pnlOa = oa;
            ai.Controls.Add(oa);
            cbOaService.SelectedIndexChanged += (o, e) =>
            {
                int i = cbOaService.SelectedIndex;
                if (i >= 0 && i < OaServices.Length) tbOaUrl.Text = OaServices[i][1];
                lnkOaKey.Visible = i >= 0 && i < OaServices.Length && OaServices[i][2].Length > 0;
            };
            cbProvider.SelectedIndexChanged += (o, e) =>
            {
                pnlAnth.Visible = cbProvider.SelectedIndex == 0;
                pnlOa.Visible = cbProvider.SelectedIndex == 1;
            };

            tbStyle = new TextBox { Width = 380 };
            ai.Controls.Add(Ui.Row(L.T("Стиль (необязательно):", "Style (optional):"), tbStyle));
            ai.Controls.Add(Ui.Note(L.T("Например: «пиши без заглавных букв, как в доте» или «вежливо, без сленга».",
                                        "For example: \"write in lowercase like in dota\" or \"polite, no slang\".")));
            pnlAi = ai;
            flow.Controls.Add(ai);

            chFallback = Ui.Check(L.T("Если сервис не ответил — перевести через Google", "If the service fails, translate with Google"));
            flow.Controls.Add(chFallback);

            flow.Controls.Add(Ui.Section(L.T("Проверка", "Test")));
            btnTest = new Button { Text = L.T("Проверить перевод", "Test translation"), Size = new Size(150, 28), Margin = new Padding(6, 2, 8, 2) };
            lblTest = new Label { AutoSize = true, MaximumSize = new Size(390, 0), ForeColor = Theme.Gray, Margin = new Padding(0, 7, 0, 0) };
            btnTest.Click += (o, e) => RunTest();
            flow.Controls.Add(Ui.Line(btnTest, lblTest));
            return p;
        }

        void SelectEngine(string e)
        {
            engine = e;
            cardGoogle.Selected = e == "google";
            cardDeepL.Selected = e == "deepl";
            cardAi.Selected = e == "ai";
            pnlGoogle.Visible = e == "google";
            pnlDeepL.Visible = e == "deepl";
            pnlAi.Visible = e == "ai";
            chFallback.Visible = e != "google";
            lblEngineSection.Text = L.T("Настройки: ", "Settings: ") + (e == "deepl" ? "DeepL" : e == "ai" ? L.T("ИИ", "AI") : "Google");
        }

        async void RunTest()
        {
            var tmp = Settings.Clone(S);
            ApplyTo(tmp);
            string sample = tmp.MyLang == "ru" ? "Привет! Го в мид, я прикрою, только не тупи" : "Hi! Let's go mid, I've got your back";
            btnTest.Enabled = false;
            lblTest.ForeColor = Theme.Gray;
            lblTest.Text = L.T("Перевожу: ", "Translating: ") + "«" + sample + "»…";
            string res;
            Color color;
            try
            {
                var r = await Task.Run(() => Translator.Translate(tmp, sample, tmp.TargetLang, tmp.Engine));
                res = "✓ " + r.Text + (r.Note != null ? "\n(" + r.Note + ")" : "");
                color = r.Note != null ? Theme.Warn : Theme.Good;
            }
            catch (Exception ex)
            {
                res = L.T("Ошибка: ", "Error: ") + ex.Message;
                color = Theme.Bad;
            }
            if (IsDisposed) return;
            lblTest.ForeColor = color;
            lblTest.Text = res;
            btnTest.Enabled = true;
        }

        // ───────────── Вкладка «Горячие клавиши» ─────────────
        TabPage BuildKeys()
        {
            var p = new TabPage(L.T("Горячие клавиши", "Hotkeys")) { UseVisualStyleBackColor = true };
            var flow = Ui.Flow();
            p.Controls.Add(flow);
            flow.Controls.Add(Ui.Intro(L.T("Кликни в поле и нажми нужное сочетание. Backspace — очистить (отключить).",
                                           "Click a field and press the combination. Backspace clears (disables) it.")));

            hkPopup = KeyRow(flow, L.T("Окно ввода", "Input box"),
                L.T("Поверх игры появляется поле: пишешь сообщение → Enter → перевод.", "A box pops up over the game: type your message → Enter → translation."));
            hkInline = KeyRow(flow, L.T("Перевести текст из чата", "Translate the chat field"),
                L.T("Быстрый режим: пишешь прямо в чате игры и жмёшь это вместо Enter. Безопасный: сам вырежи текст (Ctrl+A, Ctrl+X) и нажми — перевод окажется в буфере.",
                    "Fast mode: type right in the game chat and press this instead of Enter. Safe mode: cut the text yourself (Ctrl+A, Ctrl+X), press it — the translation lands in the clipboard."));
            hkReverse = KeyRow(flow, L.T("Перевести чужое сообщение", "Translate incoming message"),
                L.T("Скопируй чужое сообщение (Ctrl+C) и нажми — перевод появится на экране. В быстром режиме выделенный текст копируется сам.",
                    "Copy someone's message (Ctrl+C) and press it — the translation appears on screen. In fast mode the selected text is copied for you."));
            hkToggle = KeyRow(flow, L.T("Включить / выключить", "Turn on / off"),
                L.T("Работает, даже когда переводчик выключен. Можно не задавать — вкл/выкл есть по клику на значок в трее.",
                    "Works even when the translator is off. Optional — clicking the tray icon toggles it too."));

            var btnDef = new Button { Text = L.T("Вернуть клавиши по умолчанию", "Restore default hotkeys"), AutoSize = true, Margin = new Padding(6, 10, 3, 3) };
            btnDef.Click += (o, e) =>
            {
                hkPopup.Text = Settings.DefPopup;
                hkInline.Text = Settings.DefInline;
                hkReverse.Text = Settings.DefReverse;
                hkToggle.Text = "";
            };
            flow.Controls.Add(btnDef);

            flow.Controls.Add(Ui.Section(L.T("Полноэкранные игры", "Fullscreen games")));
            flow.Controls.Add(Ui.Note(L.T("Если игра в полноэкранном режиме сворачивается, когда появляется окно ввода, — переключи её в «оконный без рамки» (borderless) или пользуйся клавишей «Перевести текст из чата»: ей окно не нужно.",
                                          "If a fullscreen game minimizes when the input box appears, switch the game to borderless windowed, or use \"Translate the chat field\" — it needs no window.")));
            return p;
        }

        HotkeyBox KeyRow(FlowLayoutPanel flow, string title, string desc)
        {
            var hb = new HotkeyBox { Width = 230 };
            var row = Ui.Row(title, hb, 330);
            row.Controls[0].Font = new Font("Segoe UI", 9f, FontStyle.Bold);
            row.Margin = new Padding(0, 8, 0, 0);
            flow.Controls.Add(row);
            flow.Controls.Add(Ui.Note(desc));
            return hb;
        }

        // ───────────── Вкладка «Общее» ─────────────
        TabPage BuildGeneral()
        {
            var p = new TabPage(L.T("Общее", "General")) { UseVisualStyleBackColor = true };
            var flow = Ui.Flow();
            p.Controls.Add(flow);

            flow.Controls.Add(Ui.Section(L.T("Программа", "App")));
            cbUiLang = new ComboBox { Width = 250, DropDownStyle = ComboBoxStyle.DropDownList };
            cbUiLang.Items.AddRange(new object[] { L.T("Авто (как в Windows)", "Auto (same as Windows)"), "Русский", "English" });
            flow.Controls.Add(Ui.Row(L.T("Язык интерфейса:", "Interface language:"), cbUiLang));
            flow.Controls.Add(Ui.Note(L.T("После сохранения программа перезапустится на новом языке.", "After saving, the app restarts in the new language.")));
            chLayout = Ui.Check(L.T("Переключать раскладку на мой язык в окне ввода (и обратно после)", "Switch keyboard layout to my language in the input box (and back after)"));
            chAutostart = Ui.Check(L.T("Запускать вместе с Windows", "Start with Windows"));
            flow.Controls.Add(chLayout);
            flow.Controls.Add(chAutostart);

            flow.Controls.Add(Ui.Section(L.T("Приватность", "Privacy")));
            flow.Controls.Add(Ui.Note(L.T("Текст сообщений отправляется только в выбранный сервис перевода. Ключи хранятся зашифрованными (Windows DPAPI) — прочитать их может только твоя учётная запись Windows. Программа ничего не собирает и сама никуда не ходит, кроме переводчика и проверки обновлений по кнопке.",
                                          "Message text is sent only to the translation service you picked. Keys are stored encrypted (Windows DPAPI) — only your Windows account can read them. The app collects nothing and talks to nothing except the translator and the update check (on click).")));

            flow.Controls.Add(Ui.Section(L.T("О программе", "About")));
            flow.Controls.Add(new Label
            {
                Text = AppInfo.Name + " " + AppInfo.Version + L.T(" — бесплатно, с открытым кодом (лицензия MIT)", " — free and open source (MIT license)"),
                AutoSize = true, Margin = new Padding(6, 2, 3, 2)
            });
            flow.Controls.Add(Ui.Link(AppInfo.RepoUrl, AppInfo.RepoUrl));
            flow.Controls.Add(Ui.Link(L.T("Сообщить об ошибке или предложить идею →", "Report a bug or suggest an idea →"), AppInfo.RepoUrl + "/issues"));

            var btnUpd = new Button { Text = L.T("Проверить обновления", "Check for updates"), AutoSize = true, Margin = new Padding(6, 8, 8, 3) };
            lblUpdate = new LinkLabel { AutoSize = true, Margin = new Padding(0, 13, 0, 0), LinkColor = Theme.Accent, LinkBehavior = LinkBehavior.HoverUnderline };
            lblUpdate.Links.Clear();
            lblUpdate.LinkClicked += (o, e) => Ui.OpenUrl(e.Link.LinkData as string);
            btnUpd.Click += (o, e) => CheckUpdates(btnUpd);
            flow.Controls.Add(Ui.Line(btnUpd, lblUpdate));

            var btnFolder = new Button { Text = L.T("Открыть папку настроек и лога", "Open settings & log folder"), AutoSize = true, Margin = new Padding(6, 4, 8, 3) };
            btnFolder.Click += (o, e) =>
            {
                try { Directory.CreateDirectory(Settings.Dir); Process.Start("explorer.exe", "\"" + Settings.Dir + "\""); } catch { }
            };
            var btnReset = new Button { Text = L.T("Сбросить все настройки", "Reset all settings"), AutoSize = true, Margin = new Padding(0, 4, 3, 3) };
            btnReset.Click += (o, e) =>
            {
                if (MessageBox.Show(L.T("Вернуть все настройки по умолчанию (ключи тоже очистятся)? Изменения применятся, когда нажмёшь «Сохранить».",
                                        "Reset all settings to defaults (keys will be cleared too)? Changes apply when you click Save."),
                        AppInfo.Name, MessageBoxButtons.OKCancel, MessageBoxIcon.Question) == DialogResult.OK)
                {
                    var d = new Settings();
                    d.UiLang = S.UiLang;
                    LoadFrom(d);
                }
            };
            flow.Controls.Add(Ui.Line(btnFolder, btnReset));
            return p;
        }

        async void CheckUpdates(Button btn)
        {
            btn.Enabled = false;
            lblUpdate.Links.Clear();
            lblUpdate.LinkArea = new LinkArea(0, 0);
            lblUpdate.Text = L.T("Проверяю…", "Checking…");
            string text;
            string url = null;
            try
            {
                var rel = await Task.Run(() => Translator.LatestRelease());
                Version latest, mine = new Version(AppInfo.Version);
                if (rel == null || !Version.TryParse(rel[0], out latest)) text = L.T("Не удалось разобрать ответ GitHub", "Couldn't read the GitHub response");
                else if (latest > mine) { text = L.T("Доступна версия ", "Version ") + rel[0] + L.T(" — скачать", " is available — download"); url = rel[1]; }
                else text = L.T("У тебя последняя версия ✓", "You're up to date ✓");
            }
            catch (HttpError he)
            {
                text = he.Status == 404 ? L.T("Опубликованных версий пока нет", "No releases published yet") : L.T("Ошибка: ", "Error: ") + he.Message;
            }
            catch (Exception ex) { text = L.T("Ошибка: ", "Error: ") + ex.Message; }
            if (IsDisposed) return;
            lblUpdate.Text = text;
            lblUpdate.Links.Clear();
            if (url != null) lblUpdate.Links.Add(0, text.Length, url);
            btn.Enabled = true;
        }

        // ───────────── Загрузка / сохранение ─────────────
        void LoadFrom(Settings s)
        {
            rbDefSafe.Checked = s.AutoDefault != "fast";
            rbDefFast.Checked = s.AutoDefault == "fast";
            chAutoSend.Checked = s.AutoSend;
            chRestoreClip.Checked = s.RestoreClipboard;
            chSound.Checked = s.Sound;
            tbSafe.Text = s.SafeApps;
            tbFast.Text = s.FastApps;

            cbTarget.SelectedIndex = Lang.IndexOf(s.TargetLang);
            cbMy.SelectedIndex = Lang.IndexOf(s.MyLang);
            tbDeepL.Text = s.GetDeepLKey();
            cbProvider.SelectedIndex = s.AiProvider == "openai" ? 1 : 0;
            tbAnthKey.Text = s.GetAnthropicKey();
            cbAnthModel.Text = s.AnthropicModel;
            int svc = OaServices.Length;
            for (int i = 0; i < OaServices.Length; i++)
                if (string.Equals(OaServices[i][1], (s.OpenAiUrl ?? "").Trim().TrimEnd('/'), StringComparison.OrdinalIgnoreCase)) svc = i;
            cbOaService.SelectedIndex = svc;
            tbOaUrl.Text = s.OpenAiUrl;
            tbOaKey.Text = s.GetOpenAiKey();
            tbOaModel.Text = s.OpenAiModel;
            tbStyle.Text = s.AiStyle;
            chFallback.Checked = s.FallbackGoogle;

            hkPopup.Text = s.HkPopup;
            hkInline.Text = s.HkInline;
            hkReverse.Text = s.HkReverse;
            hkToggle.Text = s.HkToggle;

            cbUiLang.SelectedIndex = s.UiLang == "ru" ? 1 : s.UiLang == "en" ? 2 : 0;
            chLayout.Checked = s.SwitchLayout;
            chAutostart.Checked = AutoStart.Get();

            SelectEngine(s.Engine);
            SelectPreset(s.Preset);
        }

        void ApplyTo(Settings s)
        {
            s.Preset = preset;
            s.AutoDefault = rbDefFast.Checked ? "fast" : "safe";
            s.AutoSend = chAutoSend.Checked;
            s.RestoreClipboard = chRestoreClip.Checked;
            s.Sound = chSound.Checked;
            s.SafeApps = tbSafe.Text.Trim();
            s.FastApps = tbFast.Text.Trim();

            s.Engine = engine;
            s.TargetLang = Lang.All[Math.Max(0, cbTarget.SelectedIndex)][0];
            s.MyLang = Lang.All[Math.Max(0, cbMy.SelectedIndex)][0];
            s.SetDeepLKey(tbDeepL.Text);
            s.AiProvider = cbProvider.SelectedIndex == 1 ? "openai" : "anthropic";
            s.SetAnthropicKey(tbAnthKey.Text);
            s.AnthropicModel = cbAnthModel.Text.Trim();
            s.OpenAiUrl = tbOaUrl.Text.Trim();
            s.SetOpenAiKey(tbOaKey.Text);
            s.OpenAiModel = tbOaModel.Text.Trim();
            s.AiStyle = tbStyle.Text.Trim();
            s.FallbackGoogle = chFallback.Checked;

            s.HkPopup = hkPopup.Text;
            s.HkInline = hkInline.Text;
            s.HkReverse = hkReverse.Text;
            s.HkToggle = hkToggle.Text;

            s.UiLang = cbUiLang.SelectedIndex == 1 ? "ru" : cbUiLang.SelectedIndex == 2 ? "en" : "auto";
            s.SwitchLayout = chLayout.Checked;
        }

        void SaveAndClose()
        {
            // Одинаковые сочетания на разных действиях.
            var seen = new Dictionary<string, HotkeyBox>(StringComparer.OrdinalIgnoreCase);
            foreach (var hb in new[] { hkPopup, hkInline, hkReverse, hkToggle })
            {
                string v = hb.Text.Trim();
                if (v.Length == 0) continue;
                if (seen.ContainsKey(v))
                {
                    tabs.SelectedIndex = TabKeys;
                    hb.Focus();
                    hb.Flash();
                    MessageBox.Show(L.T("Сочетание ", "The combination ") + v + L.T(" назначено на два действия. Выбери другое.", " is assigned to two actions. Pick another one."),
                        AppInfo.Name, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                seen[v] = hb;
            }

            var tmp = Settings.Clone(S);
            ApplyTo(tmp);
            if (!Translator.IsConfigured(tmp, tmp.Engine))
            {
                var ans = MessageBox.Show(
                    L.T("Для выбранного сервиса не указан ключ (или адрес/модель). Пока не укажешь — ", "The selected service has no key (or address/model). Until you add it, ") +
                    (tmp.FallbackGoogle ? L.T("будет переводить Google.", "Google will translate instead.") : L.T("перевод работать не будет.", "translation won't work.")) +
                    L.T("\n\nВсё равно сохранить?", "\n\nSave anyway?"),
                    AppInfo.Name, MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
                if (ans != DialogResult.Yes) { tabs.SelectedIndex = TabEngine; return; }
            }

            ApplyTo(S);
            S.Save();
            try { AutoStart.Set(chAutostart.Checked); } catch (Exception ex) { Log.Write("autostart: " + ex.Message); }
            RestartRequested = S.UiLang != initialUiLang;
            Close();
        }
    }
}

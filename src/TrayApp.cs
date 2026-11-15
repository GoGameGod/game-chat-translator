using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Media;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ChatTranslator
{
    class TrayApp : ApplicationContext
    {
        const int HK_POPUP = 1, HK_INLINE = 2, HK_REVERSE = 3, HK_TOGGLE = 4;

        readonly Settings S;
        readonly NotifyIcon tray;
        readonly HotkeyHost hk;
        readonly InputPopup popup;
        readonly Toast toast;
        readonly Icon icoOn, icoOff;
        SettingsForm settingsForm;
        IntPtr target;
        bool popupSafe;
        string lastExe;
        int reqId;
        bool working;

        public TrayApp(Settings settings, int openTab)
        {
            S = settings;
            bool firstRun = !File.Exists(Settings.FilePath);
            icoOn = Theme.MakeIcon(Theme.Accent);
            icoOff = Theme.MakeIcon(Color.FromArgb(125, 125, 125));
            AutoStart.Refresh();

            hk = new HotkeyHost();
            hk.Pressed += OnHotkey;

            popup = new InputPopup();
            popup.Submitted += OnPopupSubmitted;
            popup.Cancelled += () => { reqId++; };
            popup.EngineToggleRequested += CycleEngine;

            toast = new Toast();

            tray = new NotifyIcon { Visible = true, ContextMenuStrip = new ContextMenuStrip() };
            tray.ContextMenuStrip.Opening += (s, e) => { BuildMenu(); e.Cancel = false; };
            tray.MouseClick += (s, e) => { if (e.Button == MouseButtons.Left) SetEnabled(!S.Enabled); };

            RegisterAll();
            UpdateTray();

            if (firstRun)
            {
                S.Save();
                OpenSettings(SettingsForm.TabMode);
            }
            else if (openTab >= 0) OpenSettings(openTab);
            else if (S.Enabled)
                toast.ShowMessage(L.T("Чат-переводчик запущен · окно ввода: ", "Chat Translator is running · input box: ") + S.HkPopup,
                    Toast.Info, 3000, IntPtr.Zero);
        }

        // ── горячие клавиши ──
        void RegisterAll()
        {
            hk.UnregisterAll();
            var failed = new List<string>();
            if (S.Enabled)
            {
                TryReg(HK_POPUP, S.HkPopup, failed);
                TryReg(HK_INLINE, S.HkInline, failed);
                TryReg(HK_REVERSE, S.HkReverse, failed);
            }
            TryReg(HK_TOGGLE, S.HkToggle, failed);
            if (failed.Count > 0)
                toast.ShowMessage(L.T("Не удалось занять: ", "Couldn't register: ") + string.Join(", ", failed) +
                    L.T("\nЭти сочетания заняты другой программой — поменяй их в настройках.", "\nThese combinations are taken by another app — change them in settings."),
                    Toast.Err, 7000, IntPtr.Zero);
        }

        void TryReg(int id, string combo, List<string> failed)
        {
            if (string.IsNullOrWhiteSpace(combo)) return;
            if (!hk.Register(id, combo)) failed.Add(combo);
        }

        void OnHotkey(int id)
        {
            if (id == HK_TOGGLE) { SetEnabled(!S.Enabled); return; }
            if (!S.Enabled) return;

            IntPtr fg = Native.GetForegroundWindow();
            if (popup.Visible && fg == popup.Handle) { if (id == HK_POPUP) popup.Cancel(); return; }
            if (working && id != HK_POPUP) return;

            target = fg;
            string exe = Win.ExeOf(fg);
            if (exe != null) lastExe = exe;
            bool safe = IsSafe(exe);

            if (id == HK_POPUP) OpenPopup(safe);
            else if (id == HK_INLINE) Inline(safe);
            else if (id == HK_REVERSE) Reverse(safe);
        }

        bool IsSafe(string exe)
        {
            if (S.Preset == "safe") return true;
            if (S.Preset == "fast") return false;
            if (Win.InList(S.SafeApps, exe)) return true;
            if (Win.InList(S.FastApps, exe)) return false;
            return S.AutoDefault != "fast";
        }

        string EngineName() { return Translator.EngineName(S, S.Engine); }

        string PopupHeader()
        {
            string mode = target == IntPtr.Zero || popupSafe
                ? L.T("безопасный: перевод в буфер, вставишь сам Ctrl+V", "safe: translation goes to clipboard, paste with Ctrl+V")
                : (S.AutoSend ? L.T("быстрый: вставлю и отправлю", "fast: I'll paste and send it")
                              : L.T("быстрый: вставлю в чат", "fast: I'll paste it into the chat"));
            return Lang.Tag(S.MyLang) + " → " + Lang.Tag(S.TargetLang) + "   ·   " + EngineName() + "   ·   " + mode;
        }

        void CycleEngine()
        {
            string[] order = { "google", "deepl", "ai" };
            int cur = Array.IndexOf(order, S.Engine);
            for (int i = 1; i <= order.Length; i++)
            {
                string next = order[(cur + i) % order.Length];
                if (next == S.Engine) break;
                if (Translator.IsConfigured(S, next))
                {
                    S.Engine = next;
                    SaveSettings();
                    popup.ResetStatus(PopupHeader());
                    return;
                }
            }
            popup.SetStatus(L.T("Других сервисов нет — подключи DeepL или ИИ в настройках (вкладка «Переводчик»)",
                                "No other services — set up DeepL or AI in settings (Translator tab)"), Toast.Err);
        }

        // ── окно ввода ──
        void OpenPopup(bool safe)
        {
            popupSafe = safe;
            reqId++;
            popup.ShowFor(target, PopupHeader(),
                L.T("Enter — перевести   ·   Tab — проверить   ·   Shift+Enter — в буфер   ·   F2 — сервис   ·   ↑↓ — история   ·   Esc",
                    "Enter — translate   ·   Tab — preview   ·   Shift+Enter — copy only   ·   F2 — service   ·   ↑↓ — history   ·   Esc"),
                S.MyLang, S.SwitchLayout);
        }

        async void OnPopupSubmitted(string text, bool copyOnly, bool preview)
        {
            int my = ++reqId;
            TranslateResult r = null;
            string err = null;
            string cached;
            if (!preview && popup.TryGetPreview(text, out cached))
            {
                r = new TranslateResult { Text = cached };
            }
            else
            {
                popup.SetBusy(true);
                popup.SetStatus(L.T("Перевожу через ", "Translating with ") + EngineName() + "…", Toast.Info);
                try { r = await TranslateAsync(text, S.TargetLang); }
                catch (Exception ex) { err = ex.Message; Log.Write("translate: " + ex.Message); }
                if (my != reqId || !popup.Visible) return; // отменили
                popup.SetBusy(false);
                if (err != null)
                {
                    popup.SetStatus(L.T("Ошибка: ", "Error: ") + err, Toast.Err);
                    if (S.Sound) SystemSounds.Hand.Play();
                    return;
                }
            }

            if (preview)
            {
                popup.ShowPreview(text, r.Text, r.Note);
                popup.SetStatus(L.T("Перевод готов: Enter — вставить. Можно поправить текст и снова нажать Tab.",
                                    "Translation ready: Enter to use it. You can edit the text and press Tab again."), Toast.Ok);
                return;
            }

            popup.AddHistory(text);
            IntPtr tgt = popup.Target;
            bool paste = !popupSafe && !copyOnly && tgt != IntPtr.Zero;
            string prevClip = paste && S.RestoreClipboard ? Clip.GetText() : null;
            Clip.SetText(r.Text);
            popup.CloseAndReturnFocus();

            if (paste)
            {
                await Task.Delay(120);
                if (Native.GetForegroundWindow() != tgt)
                {
                    toast.ShowMessage(L.T("Окно сменилось — перевод в буфере, вставь Ctrl+V:\n", "The window changed — the translation is in the clipboard, paste with Ctrl+V:\n") + r.Text,
                        Toast.Info, Toast.TimeFor(r.Text), tgt);
                    return;
                }
                Native.SendCombo(Native.VK_CONTROL, Native.VK_V);
                if (S.AutoSend) { await Task.Delay(70); Native.SendCombo(Native.VK_RETURN); }
                if (r.Note != null) toast.ShowMessage(r.Note, Toast.Err, 4000, tgt);
                await RestoreClipboardLater(prevClip);
            }
            else
            {
                toast.ShowMessage(L.T("Скопировано → Ctrl+V:\n", "Copied → Ctrl+V:\n") + r.Text + (r.Note != null ? "\n(" + r.Note + ")" : ""),
                    Toast.Ok, Toast.TimeFor(r.Text), tgt);
                if (S.Sound) SystemSounds.Asterisk.Play();
            }
        }

        async Task RestoreClipboardLater(string prev)
        {
            if (prev == null) return;
            await Task.Delay(400);
            Clip.SetText(prev);
        }

        // ── перевод текста из поля чата / из буфера ──
        async void Inline(bool safe)
        {
            working = true;
            IntPtr tgt = target;
            try
            {
                string src;
                string prevClip = null;
                if (safe)
                {
                    src = Clip.GetText();
                    if (string.IsNullOrWhiteSpace(src))
                    {
                        toast.ShowMessage(L.T("Буфер пуст. В безопасном режиме: выдели текст в чате (Ctrl+A), вырежи (Ctrl+X) и нажми ",
                                              "The clipboard is empty. In safe mode: select the chat text (Ctrl+A), cut it (Ctrl+X) and press ") + S.HkInline,
                            Toast.Err, 6000, tgt);
                        if (S.Sound) SystemSounds.Hand.Play();
                        return;
                    }
                }
                else
                {
                    if (!await Win.WaitModifiersReleased(2000))
                    {
                        toast.ShowMessage(L.T("Отпусти Ctrl/Shift/Alt и попробуй снова.", "Release Ctrl/Shift/Alt and try again."), Toast.Err, 3000, tgt);
                        return;
                    }
                    if (Native.GetForegroundWindow() != tgt) return;
                    if (S.RestoreClipboard) prevClip = Clip.GetText();
                    Clip.Clear();
                    Native.SendCombo(Native.VK_CONTROL, Native.VK_A);
                    await Task.Delay(40);
                    Native.SendCombo(Native.VK_CONTROL, Native.VK_C);
                    src = null;
                    for (int i = 0; i < 20 && string.IsNullOrEmpty(src); i++) { await Task.Delay(25); src = Clip.GetText(); }
                    if (string.IsNullOrWhiteSpace(src))
                    {
                        if (prevClip != null) Clip.SetText(prevClip);
                        toast.ShowMessage(L.T("Не получилось забрать текст из поля чата.\nВ этой игре пользуйся окном ввода: ",
                                              "Couldn't grab the text from the chat field.\nUse the input box in this game: ") + S.HkPopup,
                            Toast.Err, 5000, tgt);
                        return;
                    }
                }

                src = src.Trim();
                if (src.Length > 1500) src = src.Substring(0, 1500);
                TranslateResult r = null;
                string err = null;
                try { r = await TranslateAsync(src, S.TargetLang); }
                catch (Exception ex) { err = ex.Message; Log.Write("translate: " + ex.Message); }
                if (err != null)
                {
                    toast.ShowMessage(L.T("Ошибка перевода: ", "Translation error: ") + err, Toast.Err, 6000, tgt);
                    if (S.Sound) SystemSounds.Hand.Play();
                    return;
                }

                Clip.SetText(r.Text);
                if (safe)
                {
                    toast.ShowMessage(L.T("Готово → Ctrl+V:\n", "Done → Ctrl+V:\n") + r.Text + (r.Note != null ? "\n(" + r.Note + ")" : ""),
                        Toast.Ok, Toast.TimeFor(r.Text), tgt);
                    if (S.Sound) SystemSounds.Asterisk.Play();
                    return;
                }

                if (Native.GetForegroundWindow() != tgt)
                {
                    toast.ShowMessage(L.T("Окно сменилось — перевод в буфере, вставь Ctrl+V:\n", "The window changed — the translation is in the clipboard, paste with Ctrl+V:\n") + r.Text,
                        Toast.Info, Toast.TimeFor(r.Text), tgt);
                    return;
                }
                await Task.Delay(30);
                Native.SendCombo(Native.VK_CONTROL, Native.VK_A);
                await Task.Delay(40);
                Native.SendCombo(Native.VK_CONTROL, Native.VK_V);
                if (S.AutoSend) { await Task.Delay(70); Native.SendCombo(Native.VK_RETURN); }
                if (r.Note != null) toast.ShowMessage(r.Note, Toast.Err, 4000, tgt);
                await RestoreClipboardLater(prevClip);
            }
            finally { working = false; }
        }

        // ── перевод чужого сообщения на мой язык ──
        async void Reverse(bool safe)
        {
            working = true;
            IntPtr tgt = target;
            try
            {
                string src = null;
                if (!safe && await Win.WaitModifiersReleased(2000) && Native.GetForegroundWindow() == tgt)
                {
                    string prev = Clip.GetText();
                    Clip.Clear();
                    Native.SendCombo(Native.VK_CONTROL, Native.VK_C);
                    for (int i = 0; i < 12 && string.IsNullOrEmpty(src); i++) { await Task.Delay(25); src = Clip.GetText(); }
                    if (string.IsNullOrWhiteSpace(src)) src = prev;
                    if (prev != null && (S.RestoreClipboard || string.IsNullOrWhiteSpace(Clip.GetText()))) Clip.SetText(prev);
                }
                if (string.IsNullOrWhiteSpace(src)) src = Clip.GetText();
                if (string.IsNullOrWhiteSpace(src))
                {
                    toast.ShowMessage(L.T("Скопируй сообщение (Ctrl+C) и нажми ", "Copy the message (Ctrl+C) and press ") + S.HkReverse + L.T(" ещё раз.", " again."),
                        Toast.Err, 4000, tgt);
                    return;
                }
                src = src.Trim();
                if (src.Length > 3000) src = src.Substring(0, 3000);

                TranslateResult r = null;
                string err = null;
                try { r = await TranslateAsync(src, S.MyLang); }
                catch (Exception ex) { err = ex.Message; Log.Write("translate: " + ex.Message); }
                if (err != null) { toast.ShowMessage(L.T("Ошибка перевода: ", "Translation error: ") + err, Toast.Err, 6000, tgt); return; }
                toast.ShowMessage(r.Text + (r.Note != null ? "\n(" + r.Note + ")" : ""), Toast.Info, Toast.TimeFor(r.Text) + 2000, tgt);
            }
            finally { working = false; }
        }

        Task<TranslateResult> TranslateAsync(string text, string lang)
        {
            var snapshot = Settings.Clone(S);
            return Task.Run(() => Translator.Translate(snapshot, text, lang, snapshot.Engine));
        }

        // ── трей ──
        void SetEnabled(bool on)
        {
            S.Enabled = on;
            SaveSettings();
            RegisterAll();
            if (!on && popup.Visible) popup.Cancel();
            toast.ShowMessage(on ? L.T("Переводчик включён", "Translator is on")
                                 : L.T("Переводчик выключен — горячие клавиши свободны", "Translator is off — hotkeys are released"),
                on ? Toast.Ok : Toast.Info, 2000, IntPtr.Zero);
        }

        void SetPreset(string pr)
        {
            S.Preset = pr;
            SaveSettings();
            toast.ShowMessage(Help.Toast(pr) + L.T("\nИнструкция: правый клик по значку → «Как пользоваться»", "\nGuide: right-click the icon → \"How to use\""),
                Toast.Info, 4000, IntPtr.Zero);
        }

        // Закрепить режим за программой (true — безопасный, false — быстрый, null — убрать из списков).
        void AssignApp(string exe, bool? safe)
        {
            S.SafeApps = Win.RemoveFrom(S.SafeApps, exe);
            S.FastApps = Win.RemoveFrom(S.FastApps, exe);
            if (safe == true) S.SafeApps = Win.AddTo(S.SafeApps, exe);
            if (safe == false) S.FastApps = Win.AddTo(S.FastApps, exe);
            bool switched = safe.HasValue && S.Preset != "auto";
            if (switched) S.Preset = "auto";
            SaveSettings();
            string what = safe == true ? L.T("безопасный режим", "safe mode")
                        : safe == false ? L.T("быстрый режим", "fast mode")
                        : L.T("режим по умолчанию", "default mode");
            toast.ShowMessage(exe + " → " + what +
                (switched ? L.T("\nВключён режим «Авто»: теперь режим выбирается по программе.", "\n\"Auto\" mode is on: the mode now depends on the app.") : ""),
                Toast.Ok, 4500, IntPtr.Zero);
        }

        void SaveSettings()
        {
            S.Save();
            UpdateTray();
        }

        void UpdateTray()
        {
            tray.Icon = S.Enabled ? icoOn : icoOff;
            string t = AppInfo.Name + ": " + (S.Enabled ? L.T("вкл", "on") : L.T("выкл", "off")) + " · "
                + Help.Title(S.Preset).ToLowerInvariant() + " · " + EngineName();
            tray.Text = t.Length > 63 ? t.Substring(0, 63) : t;
        }

        void BuildMenu()
        {
            var m = tray.ContextMenuStrip;
            m.Items.Clear();
            m.Items.Add(new ToolStripMenuItem(AppInfo.Name + " " + AppInfo.Version) { Enabled = false });
            m.Items.Add(new ToolStripSeparator());
            AddItem(m.Items, L.T("Включён (или клик по значку)", "Enabled (or click the icon)"), S.Enabled, null, () => SetEnabled(!S.Enabled));
            m.Items.Add(new ToolStripSeparator());

            AddItem(m.Items, L.T("Режим: Безопасный (для античитов)", "Mode: Safe (for anti-cheat games)"), S.Preset == "safe", null, () => SetPreset("safe"));
            AddItem(m.Items, L.T("Режим: Быстрый (вставляет сам)", "Mode: Fast (pastes for you)"), S.Preset == "fast", null, () => SetPreset("fast"));
            AddItem(m.Items, L.T("Режим: Авто (по программе)", "Mode: Auto (per app)"), S.Preset == "auto", null, () => SetPreset("auto"));
            if (S.Preset != "safe")
                AddItem(m.Items, L.T("Сразу отправлять Enter после вставки", "Press Enter after pasting"), S.AutoSend, null, () => { S.AutoSend = !S.AutoSend; SaveSettings(); });
            if (lastExe != null)
            {
                string exe = lastExe;
                bool inSafe = Win.InList(S.SafeApps, exe), inFast = Win.InList(S.FastApps, exe);
                var sub = new ToolStripMenuItem(L.T("Последняя программа: ", "Last app: ") + exe);
                AddItem(sub.DropDownItems, L.T("Всегда безопасный режим", "Always safe mode"), inSafe, null, () => AssignApp(exe, true));
                AddItem(sub.DropDownItems, L.T("Всегда быстрый режим", "Always fast mode"), inFast, null, () => AssignApp(exe, false));
                var rm = AddItem(sub.DropDownItems, L.T("Убрать из списков", "Remove from lists"), false, null, () => AssignApp(exe, null));
                rm.Enabled = inSafe || inFast;
                m.Items.Add(sub);
            }
            m.Items.Add(new ToolStripSeparator());

            var eng = new ToolStripMenuItem(L.T("Сервис: ", "Service: ") + EngineName());
            foreach (var e in new[] { "google", "deepl", "ai" })
            {
                string en = e;
                bool ok = Translator.IsConfigured(S, en);
                string name = en == "google" ? "Google" : en == "deepl" ? "DeepL" : L.T("ИИ", "AI") + " (" + Translator.EngineName(S, "ai") + ")";
                var it = AddItem(eng.DropDownItems, name + (ok ? "" : L.T(" — не настроен", " — not set up")), S.Engine == en, null, () =>
                {
                    if (!Translator.IsConfigured(S, en)) { OpenSettings(SettingsForm.TabEngine); return; }
                    S.Engine = en;
                    SaveSettings();
                });
            }
            m.Items.Add(eng);

            var tl = new ToolStripMenuItem(L.T("Переводить на: ", "Translate into: ") + Lang.Tag(S.TargetLang));
            for (int i = 0; i < Lang.All.Length; i++)
            {
                string code = Lang.All[i][0];
                AddItem(tl.DropDownItems, Lang.Display(i), S.TargetLang == code, null, () => { S.TargetLang = code; SaveSettings(); });
            }
            m.Items.Add(tl);
            m.Items.Add(new ToolStripSeparator());

            AddItem(m.Items, L.T("Открыть окно ввода", "Open input box"), false, S.HkPopup, () =>
            {
                if (!S.Enabled) return;
                target = IntPtr.Zero;
                OpenPopup(true);
            });
            AddItem(m.Items, L.T("Как пользоваться", "How to use"), false, null, () => OpenSettings(SettingsForm.TabMode));
            AddItem(m.Items, L.T("Настройки…", "Settings…"), false, null, () => OpenSettings(SettingsForm.TabEngine));
            m.Items.Add(new ToolStripSeparator());
            AddItem(m.Items, L.T("Выход", "Exit"), false, null, ExitApp);
        }

        static ToolStripMenuItem AddItem(ToolStripItemCollection items, string text, bool chk, string shortcut, Action a)
        {
            var it = new ToolStripMenuItem(text) { Checked = chk };
            if (!string.IsNullOrEmpty(shortcut)) it.ShortcutKeyDisplayString = shortcut;
            it.Click += (s, e) => a();
            items.Add(it);
            return it;
        }

        void OpenSettings(int tab)
        {
            if (settingsForm != null && !settingsForm.IsDisposed)
            {
                settingsForm.SelectTab(tab);
                settingsForm.Activate();
                return;
            }
            hk.UnregisterAll(); // чтобы можно было записать сочетания в поля
            settingsForm = new SettingsForm(S, lastExe, icoOn, tab);
            settingsForm.FormClosed += (s, e) =>
            {
                bool restart = settingsForm.RestartRequested;
                settingsForm = null;
                if (restart) { Restart(); return; }
                RegisterAll();
                UpdateTray();
            };
            settingsForm.Show();
            settingsForm.Activate();
        }

        void Restart()
        {
            try { Process.Start(Application.ExecutablePath, "--restart"); }
            catch (Exception ex) { Log.Write("restart: " + ex.Message); }
            ExitApp();
        }

        void ExitApp()
        {
            hk.Dispose();
            tray.Visible = false;
            tray.Dispose();
            ExitThread();
        }
    }
}

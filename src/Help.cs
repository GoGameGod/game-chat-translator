using System;
using System.Collections.Generic;

namespace ChatTranslator
{
    // Блок инструкции. Kind: H — заголовок, S — шаг (нумеруется), B — пункт списка,
    // P — абзац, W — предупреждение, G — «почему безопасно».
    // В тексте {popup}, {inline}, {reverse} — ссылки на горячие клавиши, {engine} — ссылка на вкладку «Переводчик».
    class HelpBlock
    {
        public readonly char Kind;
        public readonly string Text;
        public HelpBlock(char kind, string text) { Kind = kind; Text = text; }
    }

    static class Help
    {
        public static string Title(string preset)
        {
            if (preset == "fast") return L.T("Быстрый", "Fast");
            if (preset == "auto") return L.T("Авто", "Auto");
            return L.T("Безопасный", "Safe");
        }

        public static string Toast(string preset)
        {
            if (preset == "fast") return L.T("Режим «Быстрый»: перевод вставляется в чат сам", "\"Fast\" mode: the translation is pasted into the chat for you");
            if (preset == "auto") return L.T("Режим «Авто»: режим выбирается по программе", "\"Auto\" mode: the mode depends on the app");
            return L.T("Режим «Безопасный»: перевод в буфер, вставляешь сам Ctrl+V", "\"Safe\" mode: the translation goes to the clipboard, paste it with Ctrl+V");
        }

        // Инструкция к чтению чата. Токены: {chat}, {chatregion} — клавиши, {pick} — выбрать область, {engine} — вкладка «Переводчик».
        public static List<HelpBlock> Chat()
        {
            var b = new List<HelpBlock>();
            Action<char, string, string> A = (k, ru, en) => b.Add(new HelpBlock(k, L.T(ru, en)));
            A('P', "Программа сама читает чат игры с экрана и показывает перевод новых сообщений рядом с ним.",
                   "The app reads the game chat from the screen and shows translations of new messages next to it.");
            A('H', "Как включить", "How to turn it on");
            A('S', "Переключи игру в режим «оконный без рамки» (borderless): в эксклюзивном полноэкранном режиме Windows не может снять картинку игры.",
                   "Switch the game to borderless windowed: Windows can't capture a game in exclusive fullscreen.");
            A('S', "В игре нажми {chat}. В первый раз экран затемнится — выдели мышкой область, где идут сообщения чата. Можно и заранее: {pick}.",
                   "In the game press {chat}. The first time, the screen dims — drag over the area where chat messages appear. Or do it now: {pick}.");
            A('S', "Над чатом появится панель с переводом новых сообщений. Выключить — снова {chat}.",
                   "A panel with translations of new messages appears above the chat. Press {chat} again to turn it off.");
            A('H', "Советы", "Tips");
            A('B', "Выделяй только сами сообщения — без поля ввода, кнопок и мини-карты: так меньше мусора.",
                   "Select only the messages — no input field, buttons or minimap: less noise.");
            A('B', "Сообщения уже на твоём языке не переводятся, каждая строка переводится один раз.",
                   "Messages already in your language are skipped; each line is translated once.");
            A('B', "Если текст распознаётся с ошибками — выбери ниже язык чата (язык, на котором пишут собеседники).",
                   "If text is misread, pick the chat language below (the language other players write in).");
            A('B', "Переводит тот же сервис, что и твои сообщения ({engine}).",
                   "Translation uses the same service as your messages ({engine}).");
            A('G', "Безопасно: программа только снимает картинку выбранной области экрана (как OBS или Discord при трансляции) и распознаёт текст средствами Windows прямо на компьютере. В игру она не лезет и клавиши не нажимает.",
                   "Safe: the app only takes a picture of the selected screen area (like OBS or Discord screen sharing) and reads the text with Windows' built-in OCR, locally. It never touches the game and never presses keys.");
            return b;
        }

        static string Mine(string myLang)
        {
            if (L.Ru) return myLang == "ru" ? "по-русски" : "на своём языке";
            return myLang == "en" ? "in English" : "in your language";
        }

        public static List<HelpBlock> For(string preset, bool autoSend, string autoDefault, bool sound, string myLang)
        {
            var b = new List<HelpBlock>();
            Action<char, string, string> A = (k, ru, en) => b.Add(new HelpBlock(k, L.T(ru, en)));
            string mine = Mine(myLang);

            if (preset == "fast")
            {
                A('H', "Способ 1 — прямо в чате (самый быстрый)", "Method 1 — right in the chat (fastest)");
                A('S', "Пиши сообщение в чате игры " + mine + ".", "Type your message in the game chat " + mine + ".");
                A('S', "Вместо Enter нажми {inline} — текст заменится переводом" + (autoSend ? " и сразу отправится." : "."),
                       "Instead of Enter, press {inline} — the text is replaced with the translation" + (autoSend ? " and sent right away." : "."));
                if (!autoSend) A('S', "Проверь перевод и нажми Enter.", "Check the translation and press Enter.");

                A('H', "Способ 2 — окно ввода", "Method 2 — input box");
                A('S', "Открой чат в игре и нажми {popup}.", "Open the game chat and press {popup}.");
                A('S', "Напиши сообщение и нажми Enter — перевод сам вставится в чат" + (autoSend ? " и отправится." : "."),
                       "Type your message and press Enter — the translation is pasted into the chat" + (autoSend ? " and sent." : "."));
                A('P', "Хочешь сначала посмотреть перевод — нажми Tab вместо Enter.", "Want to check the translation first? Press Tab instead of Enter.");

                A('H', "Перевести чужое сообщение", "Translate someone else's message");
                A('S', "Выдели сообщение мышкой и нажми {reverse} — перевод появится на экране.",
                       "Select the message with the mouse and press {reverse} — the translation appears on screen.");

                A('W', "Важно: программа сама нажимает Ctrl+A, Ctrl+C, Ctrl+V" + (autoSend ? " и Enter" : "") +
                       " — как обычная клавиатура. Строгий античит может принять это за макрос. Для таких игр выбери «Безопасный» или «Авто».",
                       "Note: the app presses Ctrl+A, Ctrl+C, Ctrl+V" + (autoSend ? " and Enter" : "") +
                       " for you, like a regular keyboard. A strict anti-cheat may treat this as a macro. For such games pick \"Safe\" or \"Auto\".");
                A('P', "Если в игре чат не понимает Ctrl+A / Ctrl+C — пользуйся окном ввода.",
                       "If the game chat doesn't support Ctrl+A / Ctrl+C, use the input box.");
            }
            else if (preset == "auto")
            {
                bool fast = autoDefault == "fast";
                A('P', "Режим выбирается сам — по программе, в которой ты нажал горячую клавишу:",
                       "The mode is picked automatically, based on the app where you pressed the hotkey:");
                A('B', "программы из списка «безопасный» — перевод в буфер, вставляешь сам (Ctrl+V);",
                       "apps in the \"safe\" list — the translation goes to the clipboard, you paste it (Ctrl+V);");
                A('B', "программы из списка «быстрый» — перевод вставляется в чат сам;",
                       "apps in the \"fast\" list — the translation is pasted into the chat for you;");
                A('B', "все остальные — " + (fast ? "быстрый" : "безопасный") + " режим.",
                       "all other apps — " + (fast ? "fast" : "safe") + " mode.");
                A('P', "Какой режим сработал, видно в заголовке окна ввода.", "The input box title shows which mode is active.");

                A('H', "Как добавить игру в список", "How to add a game to a list");
                A('S', "Зайди в игру и нажми {reverse} (появится подсказка — это нормально).",
                       "Go to the game and press {reverse} (a hint will pop up — that's fine).");
                A('S', "Правый клик по значку Tr в трее → «Последняя программа» → выбери режим. Или ниже — кнопками «→ в безопасные» / «→ в быстрые».",
                       "Right-click the Tr tray icon → \"Last app\" → pick a mode. Or use the \"→ safe\" / \"→ fast\" buttons below.");

                A('H', "Горячие клавиши", "Hotkeys");
                A('B', "{popup} — окно ввода", "{popup} — input box");
                A('B', "{inline} — перевести текст из поля чата", "{inline} — translate the text in the chat field");
                A('B', "{reverse} — перевести чужое сообщение", "{reverse} — translate someone else's message");
            }
            else
            {
                A('H', "Способ 1 — окно ввода", "Method 1 — input box");
                A('P', "Если игра в оконном или безрамочном режиме.", "If the game runs windowed or borderless.");
                A('S', "Открой чат в игре и нажми {popup} — появится поле ввода.",
                       "Open the game chat and press {popup} — an input box appears.");
                A('S', "Напиши сообщение " + mine + " и нажми Enter. " +
                       (sound ? "Прозвучит сигнал — перевод в буфере." : "Внизу экрана появится перевод — он уже в буфере."),
                       "Type your message " + mine + " and press Enter. " +
                       (sound ? "You'll hear a beep — the translation is in the clipboard." : "The translation shows at the bottom — it's already in the clipboard."));
                A('S', "В чате игры нажми Ctrl+V, затем Enter.", "In the game chat press Ctrl+V, then Enter.");

                A('H', "Способ 2 — без окна (для полноэкранных игр)", "Method 2 — no window (for fullscreen games)");
                A('S', "Напиши сообщение в чате игры " + mine + ".", "Type your message in the game chat " + mine + ".");
                A('S', "Вырежи его: Ctrl+A, затем Ctrl+X.", "Cut it: Ctrl+A, then Ctrl+X.");
                A('S', "Нажми {inline}, дождись " + (sound ? "сигнала" : "перевода внизу экрана") + ", затем Ctrl+V и Enter.",
                       "Press {inline}, wait for " + (sound ? "the beep" : "the translation at the bottom") + ", then Ctrl+V and Enter.");

                A('H', "Перевести чужое сообщение", "Translate someone else's message");
                A('S', "Скопируй сообщение (Ctrl+C) и нажми {reverse} — перевод появится на экране.",
                       "Copy the message (Ctrl+C) and press {reverse} — the translation appears on screen.");

                A('G', "Почему безопасно: программа не нажимает клавиши за тебя и не обращается к игре — только меняет текст в буфере обмена, как обычный менеджер буфера.",
                       "Why it's safe: the app never presses keys for you and never touches the game — it only changes the text in the clipboard, like any clipboard manager.");
            }
            return b;
        }
    }
}

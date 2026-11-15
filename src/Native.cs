using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Win32;

namespace ChatTranslator
{
    static class Native
    {
        public const int WM_HOTKEY = 0x0312;
        public const uint MOD_ALT = 1, MOD_CONTROL = 2, MOD_SHIFT = 4, MOD_WIN = 8, MOD_NOREPEAT = 0x4000;
        public const ushort VK_CONTROL = 0x11, VK_A = 0x41, VK_C = 0x43, VK_V = 0x56, VK_RETURN = 0x0D;

        [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
        [DllImport("user32.dll", SetLastError = true)] public static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);
        [DllImport("user32.dll")] public static extern bool UnregisterHotKey(IntPtr hWnd, int id);
        [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hWnd);
        [DllImport("user32.dll")] public static extern bool IsWindow(IntPtr hWnd);
        [DllImport("user32.dll")] public static extern short GetAsyncKeyState(int vKey);
        [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint pid);
        [DllImport("user32.dll")] public static extern uint MapVirtualKey(uint uCode, uint uMapType);
        [DllImport("user32.dll", SetLastError = true)] public static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);
        [DllImport("user32.dll")] public static extern bool DestroyIcon(IntPtr hIcon);
        [DllImport("dwmapi.dll")] public static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

        [StructLayout(LayoutKind.Sequential)]
        public struct INPUT { public uint type; public InputUnion u; }

        [StructLayout(LayoutKind.Explicit)]
        public struct InputUnion
        {
            [FieldOffset(0)] public MOUSEINPUT mi;
            [FieldOffset(0)] public KEYBDINPUT ki;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct MOUSEINPUT { public int dx, dy; public uint mouseData, dwFlags, time; public IntPtr dwExtraInfo; }

        [StructLayout(LayoutKind.Sequential)]
        public struct KEYBDINPUT { public ushort wVk, wScan; public uint dwFlags, time; public IntPtr dwExtraInfo; }

        static INPUT Key(ushort vk, bool up)
        {
            var i = new INPUT { type = 1 };
            i.u.ki.wVk = vk;
            i.u.ki.wScan = (ushort)MapVirtualKey(vk, 0);
            i.u.ki.dwFlags = up ? 2u : 0u;
            return i;
        }

        // Нажать сочетание (например Ctrl+V). Используется ТОЛЬКО в быстром режиме.
        public static void SendCombo(params ushort[] keys)
        {
            var list = new List<INPUT>();
            foreach (var k in keys) list.Add(Key(k, false));
            for (int i = keys.Length - 1; i >= 0; i--) list.Add(Key(keys[i], true));
            SendInput((uint)list.Count, list.ToArray(), Marshal.SizeOf(typeof(INPUT)));
        }

        public static bool IsDown(int vk) { return (GetAsyncKeyState(vk) & 0x8000) != 0; }

        public static void RoundCorners(IntPtr hwnd)
        {
            try { int v = 2; DwmSetWindowAttribute(hwnd, 33, ref v, 4); } catch { }
        }

        public static float DpiScale()
        {
            try { using (var g = System.Drawing.Graphics.FromHwnd(IntPtr.Zero)) return g.DpiX / 96f; }
            catch { return 1f; }
        }
    }

    static class Win
    {
        static readonly uint MyPid = (uint)Process.GetCurrentProcess().Id;

        // Имя exe активного окна. Без OpenProcess на чужой процесс: берём из общего списка процессов,
        // чтобы никак не «трогать» игру и её античит.
        public static string ExeOf(IntPtr hwnd)
        {
            if (hwnd == IntPtr.Zero) return null;
            uint pid;
            Native.GetWindowThreadProcessId(hwnd, out pid);
            if (pid == 0 || pid == MyPid) return null;
            string name = null;
            Process[] all = null;
            try
            {
                all = Process.GetProcesses();
                foreach (var p in all) if ((uint)p.Id == pid) { name = p.ProcessName + ".exe"; break; }
            }
            catch { }
            finally { if (all != null) foreach (var p in all) p.Dispose(); }
            return name;
        }

        static string[] Split(string list)
        {
            return (list ?? "").Split(new[] { ',', ';', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
        }

        public static bool InList(string list, string exe)
        {
            if (string.IsNullOrEmpty(exe)) return false;
            string e = Norm(exe);
            foreach (var part in Split(list)) if (Norm(part) == e) return true;
            return false;
        }

        public static string AddTo(string list, string exe)
        {
            if (InList(list, exe)) return list;
            string cur = (list ?? "").Trim().TrimEnd(',', ';').Trim();
            return cur.Length > 0 ? cur + ", " + exe : exe;
        }

        public static string RemoveFrom(string list, string exe)
        {
            var keep = new List<string>();
            foreach (var part in Split(list))
                if (part.Trim().Length > 0 && Norm(part) != Norm(exe)) keep.Add(part.Trim());
            return string.Join(", ", keep);
        }

        static string Norm(string s)
        {
            s = s.Trim().ToLowerInvariant();
            if (s.EndsWith(".exe")) s = s.Substring(0, s.Length - 4);
            return s;
        }

        public static async Task<bool> WaitModifiersReleased(int timeoutMs)
        {
            var sw = Stopwatch.StartNew();
            while (sw.ElapsedMilliseconds < timeoutMs)
            {
                if (!Native.IsDown(0x10) && !Native.IsDown(0x11) && !Native.IsDown(0x12)
                    && !Native.IsDown(0x5B) && !Native.IsDown(0x5C)) return true;
                await Task.Delay(15);
            }
            return false;
        }
    }

    static class Clip
    {
        public static bool SetText(string t)
        {
            if (string.IsNullOrEmpty(t)) return false;
            try { Clipboard.SetDataObject(t, true, 10, 40); return true; } catch { return false; }
        }

        public static string GetText()
        {
            for (int i = 0; i < 5; i++)
            {
                try { return Clipboard.ContainsText() ? Clipboard.GetText() : null; }
                catch { Thread.Sleep(30); }
            }
            return null;
        }

        public static void Clear()
        {
            for (int i = 0; i < 5; i++)
            {
                try { Clipboard.Clear(); return; } catch { Thread.Sleep(30); }
            }
        }
    }

    static class AutoStart
    {
        const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        const string Name = "ChatTranslator";
        static string Command { get { return "\"" + Application.ExecutablePath + "\""; } }

        static string Value()
        {
            try { using (var k = Registry.CurrentUser.OpenSubKey(RunKey)) return k == null ? null : k.GetValue(Name) as string; }
            catch { return null; }
        }

        public static bool Get() { return Value() != null; }

        public static void Set(bool on)
        {
            using (var k = Registry.CurrentUser.CreateSubKey(RunKey))
            {
                if (on) k.SetValue(Name, Command);
                else k.DeleteValue(Name, false);
            }
        }

        // Если программу перенесли в другую папку — обновить путь автозапуска.
        public static void Refresh()
        {
            string v = Value();
            if (v != null && !string.Equals(v, Command, StringComparison.OrdinalIgnoreCase))
                try { Set(true); } catch { }
        }
    }

    // Горячие клавиши через RegisterHotKey — штатный механизм Windows (так же делают Discord, OBS, ShareX).
    // Никаких глобальных хуков клавиатуры и перехвата ввода.
    static class Hotkey
    {
        public static bool TryParse(string s, out uint mods, out uint vk)
        {
            mods = 0; vk = 0;
            if (string.IsNullOrWhiteSpace(s)) return false;
            foreach (var raw in s.Split('+'))
            {
                string p = raw.Trim();
                if (p.Length == 0) continue;
                string l = p.ToLowerInvariant();
                if (l == "ctrl" || l == "control") mods |= Native.MOD_CONTROL;
                else if (l == "alt") mods |= Native.MOD_ALT;
                else if (l == "shift") mods |= Native.MOD_SHIFT;
                else if (l == "win") mods |= Native.MOD_WIN;
                else
                {
                    Keys k;
                    if (l == "enter") k = Keys.Enter;
                    else if (!Enum.TryParse<Keys>(p, true, out k)) return false;
                    vk = (uint)k;
                }
            }
            return vk != 0;
        }

        public static string Format(Keys keyData)
        {
            Keys key = keyData & Keys.KeyCode;
            if (key == Keys.None || key == Keys.ShiftKey || key == Keys.ControlKey || key == Keys.Menu
                || key == Keys.LWin || key == Keys.RWin || key == Keys.LShiftKey || key == Keys.RShiftKey
                || key == Keys.LControlKey || key == Keys.RControlKey || key == Keys.LMenu || key == Keys.RMenu
                || key == Keys.Capital) return null;
            var parts = new List<string>();
            if ((keyData & Keys.Control) != 0) parts.Add("Ctrl");
            if ((keyData & Keys.Alt) != 0) parts.Add("Alt");
            if ((keyData & Keys.Shift) != 0) parts.Add("Shift");
            parts.Add(key == Keys.Return ? "Enter" : key.ToString());
            return string.Join("+", parts);
        }
    }

    class HotkeyHost : NativeWindow, IDisposable
    {
        public event Action<int> Pressed;
        readonly HashSet<int> ids = new HashSet<int>();

        public HotkeyHost()
        {
            var cp = new CreateParams();
            cp.Parent = new IntPtr(-3); // message-only окно
            CreateHandle(cp);
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == Native.WM_HOTKEY && Pressed != null) Pressed(m.WParam.ToInt32());
            base.WndProc(ref m);
        }

        public bool Register(int id, string combo)
        {
            Unregister(id);
            uint mods, vk;
            if (!Hotkey.TryParse(combo, out mods, out vk)) return false;
            if (!Native.RegisterHotKey(Handle, id, mods | Native.MOD_NOREPEAT, vk)) return false;
            ids.Add(id);
            return true;
        }

        public void Unregister(int id)
        {
            Native.UnregisterHotKey(Handle, id);
            ids.Remove(id);
        }

        public void UnregisterAll()
        {
            foreach (var id in new List<int>(ids)) Unregister(id);
        }

        public void Dispose()
        {
            UnregisterAll();
            DestroyHandle();
        }
    }

    // Лог ошибок (без текстов сообщений и ключей) — пригодится для баг-репортов.
    static class Log
    {
        static readonly object Sync = new object();

        public static string FilePath { get { return Path.Combine(Settings.Dir, "log.txt"); } }

        public static void Write(string msg)
        {
            try
            {
                lock (Sync)
                {
                    Directory.CreateDirectory(Settings.Dir);
                    var fi = new FileInfo(FilePath);
                    if (fi.Exists && fi.Length > 256 * 1024) fi.Delete();
                    File.AppendAllText(FilePath, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "  " + msg + Environment.NewLine, Encoding.UTF8);
                }
            }
            catch { }
        }
    }
}

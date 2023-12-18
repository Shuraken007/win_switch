using System;
using System.Runtime.InteropServices;
using System.Text;
using System.Globalization;
using Microsoft.Win32;
using System.Diagnostics;

namespace WinSwitchLayout
{
    class InvalidLangException : Exception
    {
        public InvalidLangException(string str) : base(str) { }
        public override string ToString()
        {
            return Message; // if want to short output
        }
    }

    public class KeyboardLayoutHelper
    {
        static StringBuilder Input = new StringBuilder(9);

        [DllImport("user32.dll")]
        static extern uint GetKeyboardLayoutList(uint nBuff, [Out] IntPtr[]? lpList);
        [DllImport("user32.dll")]
        static extern bool GetKeyboardLayoutName([Out] StringBuilder pwszKLID);
        [DllImport("user32.dll")]
        static extern IntPtr GetKeyboardLayout(uint idThread);
        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")]
        static extern uint GetWindowThreadProcessId(IntPtr hWnd, IntPtr ProcessId);
        [DllImport("user32.dll")]
        public static extern int ActivateKeyboardLayout(int HKL, int flags);
        [DllImport("user32.dll")]
        private static extern bool PostMessage(int hhwnd, uint msg, uint wparam, IntPtr lparam);
        [DllImport("user32.dll")]
        private static extern IntPtr LoadKeyboardLayout(string pwszKLID, uint Flags);

        private static uint WM_INPUTLANGCHANGEREQUEST = 0x0050;
        private static int HWND_BROADCAST = 0xffff;

        public enum KLF : uint
        {
            ACTIVATE = 0x00000001,
            SUBSTITUTE_OK = 0x00000002,
        }

        public static IntPtr GetCurrentLayoutHandle()
        {
            return GetKeyboardLayout(GetWindowThreadProcessId(GetForegroundWindow(), IntPtr.Zero));
        }

        public static string GetLang()
        {
            IntPtr handle = GetCurrentLayoutHandle();
            String Name = CultureInfo.GetCultureInfo((short)handle).Name;
            return Name;
        }

        private const string KeyboardLayoutsRegistryPath = @"SYSTEM\CurrentControlSet\Control\Keyboard Layouts";

        public static int HIWORD(int n)
            => (n >> 16) & 0xffff;

        public static int LOWORD(int n)
            => n & 0xffff;

        public static string HandleToLayoutId(int handle)
        {
            // There is no good way to do this in Windows. GetKeyboardLayoutName does what we want, but only for the
            // current input language; setting and resetting the current input language would generate spurious
            // InputLanguageChanged events. Try to extract needed information manually.

            // High word of HKL contains a device handle to the physical layout of the keyboard but exact format of this
            // handle is not documented. For older keyboard layouts device handle seems contains keyboard layout
            // identifier.
            int device = HIWORD(handle);

            // But for newer keyboard layouts device handle contains special layout id if its high nibble is 0xF. This
            // id may be used to search for keyboard layout under registry.
            //
            // NOTE: this logic may break in future versions of Windows since it is not documented.
            if ((device & 0xF000) == 0xF000)
            {
                // Extract special layout id from the device handle
                int layoutId = device & 0x0FFF;

                using RegistryKey? key = Registry.LocalMachine.OpenSubKey(KeyboardLayoutsRegistryPath);
                if (key is not null)
                {
                    // Match keyboard layout by layout id
                    foreach (string subKeyName in key.GetSubKeyNames())
                    {
                        using RegistryKey? subKey = key.OpenSubKey(subKeyName);
                        if (subKey is not null
                            && subKey.GetValue("Layout Id") is string subKeyLayoutId
                            && Convert.ToInt32(subKeyLayoutId, 16) == layoutId)
                        {
                            Debug.Assert(subKeyName.Length == 8, $"unexpected key length in registry: {subKey.Name}");
                            return subKeyName.ToUpperInvariant();
                        }
                    }
                }
            }
            else
            {
                // Use input language only if keyboard layout language is not available. This is crucial in cases when
                // keyboard is installed more than once or under different languages. For example when French keyboard
                // is installed under US input language we need to return French keyboard identifier.
                if (device == 0)
                {
                    // According to the GetKeyboardLayout API function docs low word of HKL contains input language.
                    device = LOWORD(handle);
                }
            }

            return device.ToString("X8");
        }

        public static List<KeyboardLayout> GetKeyboardLayoutList()
        {
            var size = GetKeyboardLayoutList(0, null);
            var layoutHandles = new IntPtr[size];
            GetKeyboardLayoutList(size, layoutHandles);

            return layoutHandles.Select(h => new KeyboardLayout()
            {
                Handle = h,
                layoutId = HandleToLayoutId((int)h),
                Name = CultureInfo.GetCultureInfo((short)h).Name,
                shortName = CultureInfo.GetCultureInfo((short)h).Name.Split('-')[1].ToLower(),
            }).ToList();
        }

        public static string GetList()
        {
            var layoutList = GetKeyboardLayoutList();
            var parsedList = layoutList.Select(keyboardLayout =>
            {
                return String.Format("{0} {1}",
                    keyboardLayout.Name,
                    keyboardLayout.layoutId.TrimStart('0'));
            });
            return String.Join(", ", parsedList);
        }

        public static string Xkb_Switch_List()
        {
            var layoutList = GetKeyboardLayoutList();
            var parsedList = layoutList.Select(keyboardLayout =>
            {
                return keyboardLayout.shortName;
            });
            return String.Join(" ", parsedList);
        }

        public static bool SetKeyboardLayout(string layoutId)
        {
            return PostMessage(HWND_BROADCAST,
                (uint)WM_INPUTLANGCHANGEREQUEST,
                0,
                LoadKeyboardLayout(layoutId, (uint)(KLF.SUBSTITUTE_OK | KLF.ACTIVATE)));
        }

        public static string SetLang(string identifier)
        {
            KeyboardLayout selected = default;
            bool is_known_lang = false;

            var layoutList = GetKeyboardLayoutList();
            foreach (KeyboardLayout k in layoutList)
            {
                if (k.Name.Equals(identifier) || k.layoutId.Contains(identifier))
                {
                    selected = k;
                    is_known_lang = true;
                    break;
                }
            }

            if (!is_known_lang)
            {
                string list = GetList();
                string errMsg = String.Format(
                    "unknown language id {0} \n use one of: {1}",
                    identifier, list
                );

                throw new InvalidLangException(errMsg);
            }

            string layoutId = selected.layoutId;
            SetKeyboardLayout(layoutId);
            return GetLang();
        }

        public static string Xkb_Switch_getXkbLayout()
        {
            IntPtr handle = GetCurrentLayoutHandle();
            return CultureInfo.GetCultureInfo((short)handle).Name.Split('-')[1].ToLower();
        }

        public static void Xkb_Switch_setXkbLayout(string langShortName)
        {
            string layoutId = "";
            bool is_known_lang = false;

            if (langShortName == "us")
            {
                layoutId = "00000409";
                is_known_lang = true;
            }
            else if (langShortName == "ru")
            {
                layoutId = "00000419";
                is_known_lang = true;
            }

            if (!is_known_lang)
            {
                var layoutList = GetKeyboardLayoutList();
                foreach (KeyboardLayout k in layoutList)
                {
                    if (k.shortName.Equals(langShortName))
                    {
                        layoutId = k.layoutId;
                        is_known_lang = true;
                        break;
                    }
                }
            }

            if (!is_known_lang)
            {
                string list = GetList();
                string errMsg = String.Format(
                    "unknown language id {0} \n use one of: {1}",
                    langShortName, list
                );

                throw new InvalidLangException(errMsg);
            }

            SetKeyboardLayout(layoutId);
        }

    }
}
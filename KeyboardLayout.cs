using System;

namespace WinSwitchLayout
{
    public struct KeyboardLayout
    {
        public IntPtr Handle { get; set; }
        public string layoutId { get; set; }
        public string Name { get; set; }
    }
}

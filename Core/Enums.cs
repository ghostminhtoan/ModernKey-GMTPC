using System;

namespace ModernKey.Core
{
    public enum InputMethod
    {
        Telex = 0,
        Vni = 1,
        SimpleTelex = 2,
        TuBinhTran = 3,
        Custom = 4
    }

    public enum Charset
    {
        Unicode = 0,
        TCVN3 = 1,
        VniWindows = 2,
        UnicodeCompound = 3
    }

    public enum SwitchKeyMode
    {
        CtrlShift = 0,
        AltZ = 1,
        WinSpace = 2,
        CtrlSpace = 3,
        AltShift = 4
    }

    public enum TypingState
    {
        English = 0,
        Vietnamese = 1
    }
}

using System.Runtime.InteropServices;

namespace Oberon.Extended;

// Fallback for Xbox pads that Windows exposes through XInput but SDL doesn't map.
// No graphics window, focus dependency, or third-party controller driver required.
public enum XboxInputMode { Automatic, PreferXInput, SdlOnly }

[StructLayout(LayoutKind.Sequential)]
public struct XGamepad
{
    public ushort Buttons;
    public byte LeftTrigger, RightTrigger;
    public short LX, LY, RX, RY;
}

[StructLayout(LayoutKind.Sequential)]
public struct XState
{
    public uint PacketNumber;
    public XGamepad Pad;
}

[StructLayout(LayoutKind.Sequential)]
internal struct XVibration
{
    internal ushort Left, Right;
}

[StructLayout(LayoutKind.Sequential)]
internal struct XBattery
{
    internal byte Type, Level;
}

public static class XInput
{
    private const uint Disconnected = 1167;
    private static int library = 0; // 0 unprobed, 1 XInput1_4, 2 XInput9_1_0, -1 missing.
    private static readonly object ProbeLock = new();
    public static string Driver => Volatile.Read(ref library) switch
    {
        1 => "XInput 1.4", 2 => "XInput 9.1.0", -1 => "XInput indisponível", _ => "XInput não verificado"
    };

    [DllImport("xinput1_4.dll", EntryPoint="XInputGetState", ExactSpelling=true)]
    private static extern uint Get14(uint player, out XState state);
    [DllImport("xinput9_1_0.dll", EntryPoint="XInputGetState", ExactSpelling=true)]
    private static extern uint Get91(uint player, out XState state);
    [DllImport("xinput1_4.dll", EntryPoint="XInputSetState", ExactSpelling=true)]
    private static extern uint Set14(uint player, ref XVibration vibration);
    [DllImport("xinput9_1_0.dll", EntryPoint="XInputSetState", ExactSpelling=true)]
    private static extern uint Set91(uint player, ref XVibration vibration);
    [DllImport("xinput1_4.dll", EntryPoint="XInputGetBatteryInformation", ExactSpelling=true)]
    private static extern uint Battery14(uint player, byte deviceType, out XBattery battery);

    public static bool TryGetState(int player, out XState state)
    {
        state = default;
        if (!OperatingSystem.IsWindows()) return false;
        int choice = Volatile.Read(ref library);
        if (choice == 0)
        {
            lock (ProbeLock)
            {
                if (library == 0)
                {
                    // Probe on user 0: an unplugged gamepad still returns a valid Win32 status.
                    try { Get14(0, out _); library = 1; }
                    catch (DllNotFoundException) { }
                    catch (EntryPointNotFoundException) { }
                    if (library == 0)
                    {
                        try { Get91(0, out _); library = 2; }
                        catch (DllNotFoundException) { }
                        catch (EntryPointNotFoundException) { }
                    }
                    if (library == 0) library = -1;
                }
                choice = library;
            }
        }
        if (choice < 0) return false;
        try { return (choice == 1 ? Get14((uint)player, out state) : Get91((uint)player, out state)) == 0; }
        catch (DllNotFoundException) { Volatile.Write(ref library, -1); return false; }
        catch (EntryPointNotFoundException) { Volatile.Write(ref library, -1); return false; }
    }

    public static void SetVibration(int player, byte left, byte right)
    {
        if (Volatile.Read(ref library) <= 0) return;
        var value = new XVibration { Left = (ushort)(left * 257), Right = (ushort)(right * 257) };
        try
        {
            if (library == 1) Set14((uint)player, ref value);
            else Set91((uint)player, ref value);
        }
        catch (DllNotFoundException) { }
        catch (EntryPointNotFoundException) { }
    }

    public static string GetPower(int player)
    {
        if (Volatile.Read(ref library) != 1) return "Desconhecida";
        try
        {
            if (Battery14((uint)player, 0, out var b) != 0) return "Desconhecida";
            return b.Type switch
            {
                1 => "Via cabo",
                0 => "Não informado pelo controle",
                _ => b.Level switch
                {
                    0 => "Vazia", 1 => "Baixa", 2 => "Média", 3 => "Cheia",
                    _ => "Desconhecida"
                }
            };
        }
        catch (EntryPointNotFoundException) { return "Desconhecida"; }
        catch (DllNotFoundException) { return "Desconhecida"; }
    }

    public static PadInput ToPad(XState state)
    {
        ushort b = state.Pad.Buttons;
        uint buttons = 0;
        void Press(int index, ushort mask) { if ((b & mask) != 0) buttons |= 1u << index; }
        Press(0, 0x1000); Press(1, 0x2000); Press(2, 0x4000); Press(3, 0x8000);
        Press(4, 0x0020); Press(6, 0x0010); Press(7, 0x0040); Press(8, 0x0080);
        Press(9, 0x0100); Press(10, 0x0200);
        Press(11, 0x0001); Press(12, 0x0002); Press(13, 0x0004); Press(14, 0x0008);
        // XInput's positive Y points up, SDL's positive Y points down.
        static short Flip(short value) => (short)Math.Clamp(-(int)value, -32767, 32767);
        static short Trigger(byte value) => (short)(value * 32767 / 255);
        return new PadInput(state.Pad.LX, Flip(state.Pad.LY), state.Pad.RX, Flip(state.Pad.RY),
            Trigger(state.Pad.LeftTrigger), Trigger(state.Pad.RightTrigger), buttons);
    }
}

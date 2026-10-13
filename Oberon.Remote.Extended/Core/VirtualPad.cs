using System.Runtime.InteropServices;

namespace Oberon.Extended;

// Polls Windows virtual keys instead of installing keyboard hooks or injecting input.
// IMPORTANT: opt-in only. When enabled, desktop keys can control the connected Xbox.
// Turn it off before typing. This is not an Xbox keyboard/mouse driver.
public sealed class VirtualPad
{
    private const int VK_LBUTTON = 0x01, VK_RBUTTON = 0x02;
    private const int VK_LEFT = 0x25, VK_UP = 0x26, VK_RIGHT = 0x27, VK_DOWN = 0x28;
    private const int VK_SPACE = 0x20, VK_RETURN = 0x0D, VK_TAB = 0x09;
    private const int VK_BACK = 0x08;
    private const int VK_SHIFT = 0x10;
    private const int VK_CONTROL = 0x11;
    private const int VK_F8 = 0x77;
    private VirtualKeyMapping mapping = VirtualKeyMapping.Default;
    public VirtualKeyMapping Mapping { get => Volatile.Read(ref mapping); set => Volatile.Write(ref mapping, value ?? VirtualKeyMapping.Default); }
    private bool previousF8;
    private bool armed;
    private bool hasCursor;
    private WinPoint lastCursor;
    public bool Armed => armed;
    public void Disarm() { armed = false; hasCursor = false; }
    public bool CheckToggle()
    {
        if (!OperatingSystem.IsWindows()) return false;
        bool pressed = Pressed(VK_F8);
        bool toggled = pressed && !previousF8;
        previousF8 = pressed;
        if (toggled) { armed = !armed; hasCursor = false; }
        return toggled;
    }
    public PadInput Read(bool pointer)
    {
        if (!OperatingSystem.IsWindows() || !armed) return default;
        var keys = Mapping;
        short Stick(int horizontal) => (short)Math.Clamp(horizontal * 32767, -32767, 32767);
        int lx = (Pressed(keys.MoveRight) ? 1 : 0) - (Pressed(keys.MoveLeft) ? 1 : 0);
        int ly = (Pressed(keys.MoveDown) ? 1 : 0) - (Pressed(keys.MoveUp) ? 1 : 0);
        int rx = (Pressed(keys.LookRight) ? 1 : 0) - (Pressed(keys.LookLeft) ? 1 : 0);
        int ry = (Pressed(keys.LookDown) ? 1 : 0) - (Pressed(keys.LookUp) ? 1 : 0);
        // Optional relative desktop pointer movement; does not grab/lock the cursor.
        short pointerX = 0, pointerY = 0;
        if (pointer && GetCursorPos(out var position))
        {
            if (hasCursor)
            {
                pointerX = DeltaToAxis(position.X - lastCursor.X);
                pointerY = DeltaToAxis(position.Y - lastCursor.Y);
            }
            lastCursor = position;
            hasCursor = true;
        }
        uint buttons = 0;
        void Button(int bit, int key) { if (Pressed(key)) buttons |= 1u << bit; }
        Button(0, keys.A); Button(1, keys.B); Button(2, keys.X); Button(3, keys.Y);
        Button(4, keys.Back); Button(6, keys.Start);
        Button(9, keys.LB); Button(10, keys.RB);
        Button(11, keys.DpadUp); Button(12, keys.DpadDown);
        Button(13, keys.DpadLeft); Button(14, keys.DpadRight);
        Button(7, keys.LStick); Button(8, keys.RStick); Button(5, keys.Guide);
        static short Trig(int key) => Pressed(key) ? (short)32767 : (short)0;
        return new PadInput(Stick(lx), Stick(ly), pointer ? pointerX : Stick(rx), pointer ? pointerY : Stick(ry),
            Trig(keys.LT), Trig(keys.RT), buttons);
    }
    public static short DeltaToAxis(int delta) =>
        (short)Math.Clamp((long)delta * 1800, -32767, 32767);
    private static bool Pressed(int key) => (GetAsyncKeyState(key) & 0x8000) != 0;
    [StructLayout(LayoutKind.Sequential)]
    private struct WinPoint { public int X, Y; }
    [DllImport("user32.dll", ExactSpelling=true)]
    private static extern short GetAsyncKeyState(int key);
    [DllImport("user32.dll", ExactSpelling=true)]
    private static extern bool GetCursorPos(out WinPoint point);
}

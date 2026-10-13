using System.Buffers.Binary;

namespace Oberon.Extended;

public sealed record InputOptions(int PollHz = 120, double Deadzone = .10,
    bool SwapAB = false, bool SwapXY = false, bool InvertLeftY = false,
    bool InvertRightY = false, bool Vibration = true,
    bool TurboA = false, bool TurboB = false, int TurboHz = 8,
    double StickSensitivity = 1.0);

// SDL button order. Axes use SDL convention (up is negative).
public readonly record struct PadInput(short LX, short LY, short RX, short RY,
    short LT, short RT, uint Buttons);

// Optional local accessibility / rapid-fire transforms. No memory modification, online bypass or Xbox changes.
public static class InputTweaks
{
    public static PadInput Apply(PadInput input, InputOptions options, long nowTicks, long frequency)
    {
        double sensitivity = Math.Clamp(options.StickSensitivity, 0.50, 1.50);
        short Scale(short s) => (short)Math.Clamp(Math.Round(s * sensitivity), -32767, 32767);
        uint buttons = input.Buttons;
        int rate = Math.Clamp(options.TurboHz, 2, 15);
        bool on = frequency <= 0 || (nowTicks / Math.Max(1, frequency / (rate * 2))) % 2 == 0;
        if (!on)
        {
            if (options.TurboA) buttons &= ~1u;
            if (options.TurboB) buttons &= ~2u;
        }
        return input with { LX = Scale(input.LX), LY = Scale(input.LY),
            RX = Scale(input.RX), RY = Scale(input.RY), Buttons = buttons };
    }
}

public static class Protocol
{
    public const int PacketSize = 100;
    public static void WritePad(Span<byte> packet, int slot, PadInput input, InputOptions options)
    {
        var target = packet.Slice(slot * 25, 25);
        target.Clear(); target[0] = 0xFF;
        var (lx, ly) = FilterStick(input.LX, input.LY, options.Deadzone);
        var (rx, ry) = FilterStick(input.RX, input.RY, options.Deadzone);
        Write(target, 1, lx); Write(target, 3, options.InvertLeftY ? (short)-ly : ly);
        Write(target, 5, rx); Write(target, 7, options.InvertRightY ? (short)-ry : ry);
        Write(target, 9, Math.Max((short)0, input.LT));
        Write(target, 11, Math.Max((short)0, input.RT));
        bool B(int index) => (input.Buttons & (1u << index)) != 0;
        Set(target, 13, 7, B(options.SwapAB ? 1 : 0));
        Set(target, 13, 6, B(options.SwapAB ? 0 : 1));
        Set(target, 13, 5, B(options.SwapXY ? 3 : 2));
        Set(target, 13, 4, B(options.SwapXY ? 2 : 3));
        Set(target, 13, 3, B(9)); Set(target, 13, 2, B(10));
        Set(target, 13, 1, B(6)); Set(target, 13, 0, B(4));
        Set(target, 14, 6, B(7)); Set(target, 14, 5, B(8));
        Set(target, 14, 4, B(11)); Set(target, 14, 3, B(12));
        Set(target, 14, 2, B(13)); Set(target, 14, 1, B(14));
        Set(target, 14, 0, B(5));
    }

    public static byte[] NeutralPacket() { var p = new byte[PacketSize]; p[0] = 0xFF; return p; }
    public static (short X, short Y) FilterStick(short x, short y, double deadzone)
    {
        double nx = Math.Max(-1, x / 32767d), ny = Math.Max(-1, y / 32767d);
        double length = Math.Sqrt(nx * nx + ny * ny);
        double dz = Math.Clamp(deadzone, 0, .35);
        if (length <= dz) return (0, 0);
        double magnitude = Math.Min(1, (length - dz) / (1 - dz));
        return ((short)Math.Round(nx / length * magnitude * 32767),
                (short)Math.Round(ny / length * magnitude * 32767));
    }
    private static void Write(Span<byte> p, int index, short value) => BinaryPrimitives.WriteInt16LittleEndian(p[index..], value);
    private static void Set(Span<byte> p, int index, int bit, bool pressed) { if (pressed) p[index] |= (byte)(1 << bit); }
}

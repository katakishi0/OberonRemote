using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Oberon.Extended;

public sealed record PadView(string Name, bool Connected, PadInput Input,
    string Family = "Genérico", string Power = "Desconhecida", string Source = "SDL");

// The input worker never depends on which UI page has focus. SDL is preferred;
// native Windows XInput is a fallback when SDL does not expose Xbox controllers.
public sealed class InputEngine : IDisposable
{
    private readonly RemoteServer server;
    private readonly VirtualPad virtualPad = new();
    private int virtualEnabled, pointerEnabled, captureEnabled = 1;
    private int keyboardArmed;
    public bool VirtualEnabled { get => Volatile.Read(ref virtualEnabled) != 0; set { Volatile.Write(ref virtualEnabled, value ? 1 : 0); if (!value) Volatile.Write(ref keyboardArmed, 0); } }
    public bool MouseAim { get => Volatile.Read(ref pointerEnabled) != 0; set => Volatile.Write(ref pointerEnabled, value ? 1 : 0); }
    public bool VirtualArmed => Volatile.Read(ref keyboardArmed) != 0;
    public VirtualKeyMapping VirtualMapping { get => virtualPad.Mapping; set => virtualPad.Mapping = value; }
    public bool CaptureEnabled { get => Volatile.Read(ref captureEnabled) != 0; set => Volatile.Write(ref captureEnabled, value ? 1 : 0); }
    private int requestDisarm;
    public void DisarmVirtual() => Volatile.Write(ref requestDisarm, 1);
    private readonly CancellationTokenSource shutdown = new();
    private readonly ConcurrentQueue<int> tests = new();
    private readonly ConcurrentQueue<string> mappingLines = new();
    private readonly IntPtr[] pads = new IntPtr[4];
    private readonly int[] ids = [-1, -1, -1, -1];
    private readonly string[] names = new string[4];
    private readonly string[] families = new string[4];
    private readonly string[] powers = new string[4];
    private readonly string[] xPower = new string[4];
    private readonly bool[] xWasConnected = new bool[4];
    private readonly byte[,] xLastRumble = new byte[4, 2];
    private readonly long[] xRumbleAt = new long[4];
    private readonly long[] testUntil = new long[4];
    private readonly byte[] lastRumble = new byte[16];
    private readonly long[] rumbleSent = new long[4];

    private Thread? worker;
    private InputOptions options = new();
    private int mode;
    private PadView[] views = Enumerable.Range(0, 4)
        .Select(_ => new PadView("Aguardando controle", false, default)).ToArray();
    private string? error;
    private string diagnostics = "Inicializando captura de controles...";
    private string previousDiscovery = "";
    private int running;
    private int availableXInputControllers;
    public int AvailableXInputControllers => Volatile.Read(ref availableXInputControllers);

    public Action<string>? Log { get; set; }
    public bool IsRunning => Volatile.Read(ref running) == 1;
    public string? Error => Volatile.Read(ref error);
    public string Diagnostics => Volatile.Read(ref diagnostics);
    public PadView[] Views => Volatile.Read(ref views);
    public InputOptions Options
    {
        get => Volatile.Read(ref options);
        set => Volatile.Write(ref options, value);
    }
    public XboxInputMode XboxBackend
    {
        get => (XboxInputMode)Volatile.Read(ref mode);
        set => Volatile.Write(ref mode, (int)value);
    }
    public InputEngine(RemoteServer server) => this.server = server;
    public void Start()
    {
        if (worker != null) return;
        worker = new Thread(Run) { IsBackground = true, Name = "Oberon Extended controller capture" };
        worker.Start();
    }
    public void TestVibration(int slot)
    {
        if (slot is >= 0 and < 4) tests.Enqueue(slot);
    }
    public void ImportMappings(string path)
    {
        int count = 0;
        foreach (string line in File.ReadLines(path))
        {
            string value = line.Trim();
            if (value.Length is > 0 and < 2048 && !value.StartsWith('#') && count++ < 4096)
                mappingLines.Enqueue(value);
        }
        Log?.Invoke("Mapeamentos SDL na fila: " + count);
    }
    private static string Family(int type) => type switch
    {
        1 or 2 => "Xbox",
        3 or 4 or 7 => "PlayStation",
        5 or 11 or 12 or 13 => "Nintendo Switch",
        8 => "Luna", 9 => "Stadia", 10 => "NVIDIA", _ => "Genérico"
    };
    private static string Power(int level) => level switch
    {
        0 => "Vazia", 1 => "Baixa", 2 => "Média", 3 => "Cheia",
        4 => "Via cabo", _ => "Desconhecida"
    };
    private static bool IsXbox(string? family, string? name) =>
        family == "Xbox" ||
        (name?.Contains("Xbox", StringComparison.OrdinalIgnoreCase) ?? false) ||
        (name?.Contains("XInput", StringComparison.OrdinalIgnoreCase) ?? false);

    private void Run()
    {
        bool sdlReady = false;
        try
        {
            try
            {
                Sdl.SDL_SetHint("SDL_JOYSTICK_ALLOW_BACKGROUND_EVENTS", "1");
                int result = Sdl.SDL_Init(0x200 | 0x2000 | 0x4000);
                if (result != 0) Log?.Invoke("SDL não inicializou: " + Sdl.Error + ". Tentando XInput.");
                else
                {
                    sdlReady = true;
                    Log?.Invoke("SDL iniciado com captura em segundo plano.");
                }
            }
            catch (DllNotFoundException ex) { Log?.Invoke("SDL ausente: " + ex.Message + ". Tentando XInput."); }
            catch (EntryPointNotFoundException ex) { Log?.Invoke("SDL incompatível: " + ex.Message + ". Tentando XInput."); }
            Volatile.Write(ref running, 1);

            long nextScan = 0;
            int lastSdlCount = -1;
            while (!shutdown.IsCancellationRequested)
            {
                long now = Stopwatch.GetTimestamp();
                int sdlCount = 0;
                if (sdlReady)
                {
                    // Bounded pump: a flood of hotplug events should never stall controller sampling.
                    for (int n = 0; n < 256 && Sdl.SDL_PollEvent(out _) != 0; n++) { }
                    Sdl.SDL_GameControllerUpdate();
                    while (mappingLines.TryDequeue(out string? line))
                        if (Sdl.SDL_GameControllerAddMapping(line) < 0)
                            Log?.Invoke("Mapeamento SDL rejeitado: " + Sdl.Error);
                    for (int i = 0; i < 4; i++)
                    {
                        if (pads[i] == IntPtr.Zero || Sdl.SDL_GameControllerGetAttached(pads[i]) != 0) continue;
                        Sdl.SDL_GameControllerClose(pads[i]);
                        pads[i] = IntPtr.Zero;
                        ids[i] = -1;
                        Log?.Invoke("Controle SDL desconectado: " + (i + 1));
                    }
                }

                XboxInputMode backend = XboxBackend;
                var xStates = new XState[4];
                var xAvailable = new bool[4];
                // Detect XInput hardware even in SDL-only mode for diagnostics and
                // optional user-approved recovery. Input remains SDL-only until selected.
                for (int i = 0; i < 4; i++)
                    xAvailable[i] = XInput.TryGetState(i, out xStates[i]);
                Volatile.Write(ref availableXInputControllers, xAvailable.Count(x => x));

                if (now >= nextScan)
                {
                    if (sdlReady)
                    {
                        sdlCount = Sdl.SDL_NumJoysticks();
                        Scan(sdlCount);
                        for (int i = 0; i < 4; i++)
                            powers[i] = pads[i] == IntPtr.Zero ? "Desconhecida" :
                                Power(Sdl.SDL_JoystickCurrentPowerLevel(Sdl.SDL_GameControllerGetJoystick(pads[i])));
                        if (lastSdlCount != sdlCount)
                        {
                            Log?.Invoke("SDL: " + sdlCount + " joystick(s), " +
                                pads.Count(p => p != IntPtr.Zero) + " mapeado(s).");
                            lastSdlCount = sdlCount;
                        }
                    }
                    for (int i = 0; i < 4; i++)
                        xPower[i] = xAvailable[i] ? XInput.GetPower(i) : "Desconhecida";
                    UpdateDiscovery(sdlReady, sdlCount, xAvailable, backend);
                    nextScan = now + Stopwatch.Frequency;
                }

                while (tests.TryDequeue(out int testSlot))
                    testUntil[testSlot] = now + Stopwatch.Frequency / 2;

                var current = Options;
                var rumble = server.Rumble;
                var packet = Protocol.NeutralPacket();
                var snapshot = new PadView[4];
                bool[] occupied = new bool[4];
                bool hasSdlXbox = false;
                bool preferNative = backend == XboxInputMode.PreferXInput && xAvailable.Any(x => x);
                if (!VirtualEnabled || Interlocked.Exchange(ref requestDisarm, 0) != 0) { virtualPad.Disarm(); Volatile.Write(ref keyboardArmed, 0); }
                else if (virtualPad.CheckToggle())
                {
                    Volatile.Write(ref keyboardArmed, virtualPad.Armed ? 1 : 0);
                    Log?.Invoke("Controle virtual: " + (virtualPad.Armed ? "armado (F8 desativa)" : "desarmado"));
                }
                for (int i = 0; i < 4; i++)
                    if (pads[i] != IntPtr.Zero && IsXbox(families[i], names[i]))
                        hasSdlXbox = true;

                if (sdlReady)
                    for (int i = 0; i < 4; i++)
                    {
                        if (pads[i] == IntPtr.Zero ||
                            (preferNative && IsXbox(families[i], names[i]))) continue;
                        // Keep the existing SDL slot for predictable multi-controller behavior.
                        occupied[i] = true;
                        uint buttons = 0;
                        for (int b = 0; b < 15; b++)
                            if (Sdl.SDL_GameControllerGetButton(pads[i], b) != 0)
                                buttons |= 1u << b;
                        var input = new PadInput(Axis(i, 0), Axis(i, 1), Axis(i, 2),
                            Axis(i, 3), Axis(i, 4), Axis(i, 5), buttons);
                        if (CaptureEnabled)
                            Protocol.WritePad(packet, i,
                                InputTweaks.Apply(input, current, now, Stopwatch.Frequency), current);
                        snapshot[i] = new(names[i], true, input, families[i], powers[i], "SDL");
                        ApplyRumble(i, rumble, current.Vibration && CaptureEnabled, now);
                    }

                // SDL and XInput often enumerate the very same Xbox device. Auto mode
                // suppresses the native duplicate when SDL already has an Xbox pad.
                bool nativeAllowed = backend != XboxInputMode.SdlOnly &&
                    (preferNative || !hasSdlXbox);
                for (int index = 0; index < 4; index++)
                {
                    if (!xAvailable[index] || !nativeAllowed)
                    {
                        if (xWasConnected[index]) XInput.SetVibration(index, 0, 0);
                        xWasConnected[index] = false;
                        continue;
                    }
                    int target = Array.IndexOf(occupied, false);
                    if (target < 0) break;
                    occupied[target] = true;
                    xWasConnected[index] = true;
                    var input = XInput.ToPad(xStates[index]);
                    if (CaptureEnabled)
                        Protocol.WritePad(packet, target,
                            InputTweaks.Apply(input, current, now, Stopwatch.Frequency), current);
                    snapshot[target] = new("Xbox Wireless Controller", true, input,
                        "Xbox", xPower[index] ?? "Desconhecida", "XInput");
                    ApplyXInputRumble(index, target, rumble, current.Vibration && CaptureEnabled, now);
                }

                if (VirtualEnabled && virtualPad.Armed)
                {
                    int virtualSlot = Array.IndexOf(occupied, false);
                    if (virtualSlot >= 0)
                    {
                        var input = virtualPad.Read(MouseAim);
                        if (CaptureEnabled)
                            Protocol.WritePad(packet, virtualSlot,
                                InputTweaks.Apply(input, current, now, Stopwatch.Frequency), current);
                        snapshot[virtualSlot] = new("Teclado e mouse", true, input,
                            "Virtual", "Sem bateria", "Windows");
                    }
                }
                for (int i = 0; i < 4; i++)
                    snapshot[i] ??= new PadView("Aguardando controle", false, default);
                Volatile.Write(ref views, snapshot);
                server.Publish(packet);
                int hz = current.PollHz is 60 or 120 or 250 ? current.PollHz : 120;
                double remaining = 1000d / hz - Stopwatch.GetElapsedTime(now).TotalMilliseconds;
                if (remaining > 0) shutdown.Token.WaitHandle.WaitOne(TimeSpan.FromMilliseconds(remaining));
            }
        }
        catch (Exception ex)
        {
            Volatile.Write(ref error, ex.Message);
            Log?.Invoke("Falha de captura: " + ex);
        }
        finally
        {
            Volatile.Write(ref running, 0);
            server.Publish(Protocol.NeutralPacket());
            for (int i = 0; i < 4; i++) XInput.SetVibration(i, 0, 0);
            if (sdlReady)
            {
                foreach (var pad in pads)
                {
                    if (pad == IntPtr.Zero) continue;
                    try
                    {
                        Sdl.SDL_GameControllerRumble(pad, 0, 0, 0);
                        Sdl.SDL_GameControllerClose(pad);
                    }
                    catch { }
                }
                Sdl.SDL_Quit();
            }
        }
    }
    private void UpdateDiscovery(bool sdlReady, int count, bool[] xPads, XboxInputMode backend)
    {
        string deviceSummary = "";
        if (sdlReady)
        {
            for (int n = 0; n < count && n < 16; n++)
            {
                string name = Marshal.PtrToStringUTF8(Sdl.SDL_JoystickNameForIndex(n)) ?? "Sem nome";
                bool mapped = Sdl.SDL_IsGameController(n) != 0;
                deviceSummary += Environment.NewLine + "SDL " + n + ": " + name +
                    (mapped ? " [mapeado]" : " [sem mapeamento]");
            }
        }
        string detected = string.Join(", ", xPads.Select((yes, i) => yes ? (i + 1).ToString() : "")
            .Where(s => s.Length != 0));
        var summary = "SDL: " + (sdlReady ? count + " joystick(s)" : "indisponível") +
            " | " + XInput.Driver + ": " + (detected.Length == 0 ? "nenhum" : detected) +
            " | Modo: " + backend + deviceSummary;
        Volatile.Write(ref diagnostics, summary);
        if (summary != previousDiscovery)
        {
            Log?.Invoke("Detecção: " + summary.Replace(Environment.NewLine, " | "));
            previousDiscovery = summary;
        }
    }
    private short Axis(int slot, int axis) => Sdl.SDL_GameControllerGetAxis(pads[slot], axis);
    private void Scan(int total)
    {
        for (int index = 0; index < total; index++)
        {
            int id = Sdl.SDL_JoystickGetDeviceInstanceID(index);
            if (id < 0 || ids.Contains(id) || Sdl.SDL_IsGameController(index) == 0) continue;
            int slot = Array.IndexOf(pads, IntPtr.Zero);
            if (slot < 0) return;
            IntPtr pad = Sdl.SDL_GameControllerOpen(index);
            if (pad == IntPtr.Zero) { Log?.Invoke("SDL_GameControllerOpen: " + Sdl.Error); continue; }
            pads[slot] = pad;
            ids[slot] = id;
            names[slot] = Marshal.PtrToStringUTF8(Sdl.SDL_GameControllerName(pad)) ?? "Controle SDL";
            families[slot] = Family(Sdl.SDL_GameControllerGetType(pad));
            if (IsXbox(families[slot], names[slot])) families[slot] = "Xbox";
            powers[slot] = "Desconhecida";
            Array.Clear(lastRumble, slot * 4, 4);
            rumbleSent[slot] = 0;
            Log?.Invoke("Controle SDL " + (slot + 1) + ": " + names[slot]);
        }
    }
    private void ApplyRumble(int slot, byte[] values, bool enabled, long now)
    {
        int offset = slot * 4;
        byte low = enabled ? values[offset] : (byte)0;
        byte high = enabled ? values[offset + 1] : (byte)0;
        byte left = enabled ? values[offset + 2] : (byte)0;
        byte right = enabled ? values[offset + 3] : (byte)0;
        bool test = enabled && now < testUntil[slot];
        if (test) low = high = 100;
        bool changed = lastRumble[offset] != low || lastRumble[offset + 1] != high ||
            lastRumble[offset + 2] != left || lastRumble[offset + 3] != right;
        if (!changed && (low | high | left | right) == 0) return;
        if (!changed && Stopwatch.GetElapsedTime(rumbleSent[slot]).TotalMilliseconds < 250) return;
        int result = Sdl.SDL_GameControllerRumble(pads[slot], (ushort)(low * 257), (ushort)(high * 257), 400);
        Sdl.SDL_GameControllerRumbleTriggers(pads[slot], (ushort)(left * 257), (ushort)(right * 257), 400);
        if (test && changed && result != 0) Log?.Invoke("SDL: vibração não suportada pelo driver.");
        lastRumble[offset] = low; lastRumble[offset + 1] = high;
        lastRumble[offset + 2] = left; lastRumble[offset + 3] = right;
        rumbleSent[slot] = now;
    }
    private void ApplyXInputRumble(int controller, int slot, byte[] rumble, bool enabled, long now)
    {
        int offset = slot * 4;
        byte left = enabled ? rumble[offset] : (byte)0;
        byte right = enabled ? rumble[offset + 1] : (byte)0;
        if (enabled && now < testUntil[slot]) left = right = 100;
        bool changed = xLastRumble[controller, 0] != left || xLastRumble[controller, 1] != right;
        if (!changed && (left | right) == 0) return;
        if (!changed && Stopwatch.GetElapsedTime(xRumbleAt[controller]).TotalMilliseconds < 250) return;
        XInput.SetVibration(controller, left, right);
        xLastRumble[controller, 0] = left;
        xLastRumble[controller, 1] = right;
        xRumbleAt[controller] = now;
    }
    public void Dispose()
    {
        shutdown.Cancel();
        worker?.Join();
        shutdown.Dispose();
    }
}

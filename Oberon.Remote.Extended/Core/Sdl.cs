using System.Runtime.InteropServices;

namespace Oberon.Extended;

internal static class Sdl
{
    private const string Lib = "SDL2";
    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] internal static extern int SDL_Init(uint flags);
    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] internal static extern void SDL_Quit();
    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] internal static extern int SDL_SetHint([MarshalAs(UnmanagedType.LPUTF8Str)] string name, [MarshalAs(UnmanagedType.LPUTF8Str)] string value);
    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] internal static extern IntPtr SDL_GetError();
    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] internal static extern int SDL_NumJoysticks();
    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] internal static extern IntPtr SDL_JoystickNameForIndex(int index);
    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] internal static extern int SDL_GameControllerGetType(IntPtr pad);
    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] internal static extern int SDL_GameControllerAddMapping([MarshalAs(UnmanagedType.LPUTF8Str)] string mapping);
    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] internal static extern int SDL_JoystickCurrentPowerLevel(IntPtr stick);
    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] internal static extern IntPtr SDL_GameControllerGetJoystick(IntPtr pad);
    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] internal static extern int SDL_IsGameController(int index);
    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] internal static extern int SDL_JoystickGetDeviceInstanceID(int index);
    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] internal static extern IntPtr SDL_GameControllerOpen(int index);
    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] internal static extern void SDL_GameControllerClose(IntPtr pad);
    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] internal static extern int SDL_GameControllerGetAttached(IntPtr pad);
    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] internal static extern IntPtr SDL_GameControllerName(IntPtr pad);
    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] internal static extern short SDL_GameControllerGetAxis(IntPtr pad, int axis);
    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] internal static extern byte SDL_GameControllerGetButton(IntPtr pad, int button);
    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] internal static extern int SDL_GameControllerRumble(IntPtr pad, ushort low, ushort high, uint duration);
    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] internal static extern int SDL_GameControllerRumbleTriggers(IntPtr pad, ushort left, ushort right, uint duration);
    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] internal static extern void SDL_GameControllerUpdate();
    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] internal static extern int SDL_PollEvent(out Event data);
    [StructLayout(LayoutKind.Explicit, Size = 56)] internal struct Event { [FieldOffset(0)] public uint Type; }
    internal static string Error => Marshal.PtrToStringUTF8(SDL_GetError()) ?? "Erro SDL";
}

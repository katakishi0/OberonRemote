using System.Text.Json;

namespace Oberon.Extended;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        using var instance = new Mutex(true, "OberonExtendedDesktopSingleInstance", out bool first);
        if (!first) { MessageBox.Show("O Oberon Extended já está aberto. Confira o ícone na bandeja.", "Oberon Extended"); return; }
        ApplicationConfiguration.Initialize();
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        Application.ThreadException += (_, e) => MessageBox.Show(e.Exception.Message, "Oberon Extended — erro");
        var window=new MainWindow();
        if(args.Length==2&&args[0]=="--render-preview")
        {
            var timer=new System.Windows.Forms.Timer {Interval=1200};
            window.Shown+=(_,_)=>timer.Start();
            timer.Tick+=(_,_)=>{timer.Stop();try{window.SavePreviewAndClose(Path.GetFullPath(args[1]));}catch(Exception e){File.WriteAllText(args[1]+".error.txt",e.ToString());Environment.Exit(1);}finally{timer.Dispose();}};
        }
        Application.Run(window);
    }
}

internal sealed record AppSettings(InputOptions Input, bool CloseToTray = true, bool StartMinimized = false,
    string ThemeName = "Xbox Green", string AccentHex = "#7FEF98", bool Animations = true,
    bool PinAddress = false, string PinnedIp = "",
    bool CheckUpdatesAtStartup = false,
    XboxInputMode XboxMode = XboxInputMode.Automatic,
    bool ShowBattery = false, bool ShowFamily = false, bool ShowRate = false,
    bool ShowHints = false, bool ShowControllerCards = true,
    bool VirtualKeyboard = false, bool VirtualMouseAim = false,
    string ControllerName1 = "", string ControllerName2 = "",
    string ControllerName3 = "", string ControllerName4 = "",
    VirtualKeyMapping? VirtualKeys = null, bool SuppressXInputSuggestion = false)
{
    internal static readonly string DirectoryPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "OberonExtended");
    internal static string FilePath => Path.Combine(DirectoryPath, "settings.json");
    internal static AppSettings Load()
    {
        try
        {
            var s = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(FilePath));
            if (s?.Input is null) return new AppSettings(new InputOptions());
            return s with { Input = s.Input with { Deadzone = Math.Clamp(s.Input.Deadzone, 0, .35),
                PollHz = s.Input.PollHz is 60 or 120 or 250 ? s.Input.PollHz : 120,
                StickSensitivity = Math.Clamp(s.Input.StickSensitivity, 0.5, 1.5),
                TurboHz = Math.Clamp(s.Input.TurboHz, 2, 15) } };
        }
        catch { return new AppSettings(new InputOptions()); }
    }
    internal void Save()
    {
        Directory.CreateDirectory(DirectoryPath);
        File.WriteAllText(FilePath + ".tmp", JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
        File.Move(FilePath + ".tmp", FilePath, true);
    }
}

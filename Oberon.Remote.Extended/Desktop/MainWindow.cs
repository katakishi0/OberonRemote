using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;

namespace Oberon.Extended;

internal sealed partial class MainWindow : Form
{
    private readonly RemoteServer server = new();
    private readonly InputEngine engine;
    private AppSettings settings = AppSettings.Load();
    private readonly ConcurrentQueue<string> incomingLogs = new();
    private readonly List<string> logs = [];
    private readonly Panel content = new() { Dock = DockStyle.Fill, Padding = new Padding(26,24,14,20) };
    private readonly System.Windows.Forms.Timer refresh = new() { Interval = 50 };
    private readonly NotifyIcon tray;
    private readonly Icon trayIcon;
    private readonly Label status = Theme.Label("INICIANDO",10,true,Theme.Green);
    private readonly Button pause = Theme.Button("Pausar comandos");
    private readonly Button start = Theme.Button("Parar servidor");
    private readonly StatCard controlsStat = new() { Caption="CONTROLES", Detail="Até quatro dispositivos" };
    private readonly StatCard networkStat = new() { Caption="CONEXÃO", Detail="WebSocket local · 26401" };
    private readonly StatCard rateStat = new() { Caption="RESPOSTAS / SEGUNDO", Detail="Não representa latência" };
    private readonly PadCard[] cards = Enumerable.Range(0,4).Select(i=>new PadCard { Slot=i }).ToArray();
    private readonly Dictionary<string,Button> navigation = [];
    private readonly Panel dashboard;
    private TableLayoutPanel? dashboardStats;
    private TableLayoutPanel? dashboardGrid;
    private Control? dashboardPadGrid;
    private Control? dashboardCardCaption;
    private Control? dashboardHint;
    private Control? dashboardSubtitle;
    private Panel? dashboardRoot;
    private readonly TextBox inputDiagnostic = new()
    {
        Multiline=true, ReadOnly=true, Height=75, Width=735,
        BackColor=Theme.Surface, ForeColor=Theme.Muted,
        BorderStyle=BorderStyle.None, Font=Theme.Font(9)
    };
    private readonly Panel preferences;
    private readonly Panel diagnostics;
    private readonly Panel help;
    private readonly TextBox logBox = new() { Multiline=true, ReadOnly=true, ScrollBars=ScrollBars.Vertical, Dock=DockStyle.Fill,
        BackColor=Theme.Surface, ForeColor=Theme.Muted, BorderStyle=BorderStyle.FixedSingle, Font=Theme.Font(10) };
    private readonly DarkComboBox addresses = new() { DropDownStyle=ComboBoxStyle.DropDownList, Width=250, Font=Theme.Font(13,true),
        BackColor=Theme.Surface, ForeColor=Theme.Green, FlatStyle=FlatStyle.Flat, Margin=new Padding(0,0,12,0) };
    private bool exiting;
    private long lastResponses;
    private long lastSample=Stopwatch.GetTimestamp();
    private string page="Painel";

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        WindowsTheme.SetDarkTitle(this);
    }

    internal MainWindow()
    {
        Text="Oberon Extended · Desktop"; ClientSize=new Size(1180,810); MinimumSize=new Size(1000,700);
        StartPosition=FormStartPosition.CenterScreen; BackColor=Theme.Background; ForeColor=Theme.White;
        Font=Theme.Font(10); AutoScaleMode=AutoScaleMode.Dpi; DoubleBuffered=true;
        trayIcon=MakeIcon(); Icon=trayIcon;
        engine=new InputEngine(server) { Options=settings.Input, XboxBackend=settings.XboxMode, 
            VirtualEnabled=settings.VirtualKeyboard, MouseAim=settings.VirtualMouseAim,
            VirtualMapping=settings.VirtualKeys ?? VirtualKeyMapping.Default, Log=AddLog };
        server.Log=AddLog;

        var layout=new TableLayoutPanel { Dock=DockStyle.Fill, ColumnCount=2, RowCount=2, BackColor=Theme.Background };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,205));layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute,84));layout.RowStyles.Add(new RowStyle(SizeType.Percent,100));
        Controls.Add(layout);
        var header=new Panel { Dock=DockStyle.Fill, Padding=new Padding(28,19,28,12) };
        var brand = new FlowLayoutPanel
        {
            Dock=DockStyle.Left,Width=450,Height=48,WrapContents=false,
            FlowDirection=FlowDirection.LeftToRight,Margin=Padding.Empty,Padding=Padding.Empty
        };
        var brandOberon=Theme.Label("OBERON",19,true,Theme.White);
        brandOberon.Margin=new Padding(0,0,8,0);
        var brandExtended=Theme.Label("EXTENDED",19,true,Theme.Green);
        brandExtended.Margin=Padding.Empty;
        brand.Controls.Add(brandOberon);
        brand.Controls.Add(brandExtended);
        header.Controls.Add(brand);
        var right=new FlowLayoutPanel { Dock=DockStyle.Right, Width=460, FlowDirection=FlowDirection.RightToLeft, Padding=new Padding(0,8,0,0) };
        var minimize=Theme.Button("Modo bandeja");minimize.Click+=(_,_)=>HideToTray(); right.Controls.Add(minimize);
        status.Margin=new Padding(12,12,20,0);right.Controls.Add(status);header.Controls.Add(right);
        layout.Controls.Add(header,0,0);layout.SetColumnSpan(header,2);
        var side=new FlowLayoutPanel { Dock=DockStyle.Fill, FlowDirection=FlowDirection.TopDown, WrapContents=false, Padding=new Padding(20,25,8,12),BackColor=Theme.Surface };
        foreach(var name in new[]{"Painel","Configurações","Diagnóstico"})
        { var b=Theme.Button(name);b.Width=166;b.Height=41;b.TextAlign=ContentAlignment.MiddleLeft;b.Padding=new Padding(10,0,0,0);b.Click+=(_,_)=>ShowPage(name);navigation[name]=b;side.Controls.Add(b); }
        SetupExtendedNavigation(side);
        var badge=Theme.Label("v1.0.0-preview.7\n\nSDL2 / XInput",9,false,Theme.Muted);badge.Margin=new Padding(10,15,0,0);side.Controls.Add(badge);
        layout.Controls.Add(side,0,1);layout.Controls.Add(content,1,1);
        dashboard=BuildDashboard();preferences=BuildPreferences();diagnostics=BuildDiagnostics();help=BuildHelp();
        foreach(var p in new[]{dashboard,preferences,diagnostics,help}){p.Dock=DockStyle.Fill;p.Visible=false;content.Controls.Add(p);}
        var menu=new ContextMenuStrip();menu.Items.Add("Abrir Oberon Extended",null,(_,_)=>RestoreWindow());
        menu.Items.Add("Pausar / retomar comandos",null,(_,_)=>TogglePause());menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Sair",null,(_,_)=>{exiting=true;Close();});
        tray=new NotifyIcon { Text="Oberon Extended — captura em segundo plano",Icon=trayIcon,Visible=true,ContextMenuStrip=menu };
        tray.DoubleClick+=(_,_)=>RestoreWindow();
        refresh.Tick+=(_,_)=>RefreshStatus();
        Shown+=(_,_)=>{WindowsTheme.SetDarkTitle(this);WindowsTheme.ApplyNativeDarkTheme(this);RefreshAddresses();StartServer();
            try
            {
                string mappings=Path.Combine(AppSettings.DirectoryPath,"controller-mappings.txt");
                if(File.Exists(mappings))engine.ImportMappings(mappings);
            }
            catch(Exception ex){AddLog(ex.Message);}
            engine.Start();refresh.Start();ShowPage("Painel");if(settings.CheckUpdatesAtStartup)_=CheckUpdatesAsync();if(settings.StartMinimized)HideToTray();};
        FormClosing+=OnClosing;
        InitializeExtended();
        ApplyUiVisibility();
    }

    private Panel BuildDashboard()
    {
        var root=new DarkScrollPanel { AutoScroll=true, AutoScrollMinSize=new Size(760,760) };
        dashboardRoot=root;
        var grid=new TableLayoutPanel { Dock=DockStyle.Top,Height=760,ColumnCount=1,RowCount=5 };
        dashboardGrid=grid;
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
        grid.RowStyles.Add(new RowStyle(SizeType.Absolute,146));grid.RowStyles.Add(new RowStyle(SizeType.Absolute,114));
        grid.RowStyles.Add(new RowStyle(SizeType.Absolute,42));grid.RowStyles.Add(new RowStyle(SizeType.Percent,100));grid.RowStyles.Add(new RowStyle(SizeType.Absolute,34));
        root.Controls.Add(grid);
        var hero=new Panel {Dock=DockStyle.Fill};
        var title=Theme.Label("Seu controle. Mais liberdade.",24,true);title.Location=new Point(0,0);hero.Controls.Add(title);
        var sub=Theme.Label("Conecte ao Xbox e continue usando o PC com a janela minimizada.",10,false,Theme.Muted);
        sub.Location=new Point(2,47);hero.Controls.Add(sub);dashboardSubtitle=sub;
        var ipRow=new FlowLayoutPanel { Location=new Point(2,85),Width=860,Height=48,WrapContents=false };
        var copy=Theme.Button("Copiar IP",true);copy.Width=110;copy.Click+=(_,_)=>{if(addresses.SelectedItem is string ip)Clipboard.SetText(ip);};
        pause.Click+=(_,_)=>TogglePause();
        start.Click+=(_,_)=>ToggleConnection();
        ipRow.Controls.Add(addresses);ipRow.Controls.Add(copy);ipRow.Controls.Add(start);hero.Controls.Add(ipRow);grid.Controls.Add(hero,0,0);
        var stats=new TableLayoutPanel {Dock=DockStyle.Fill,ColumnCount=3,RowCount=1,Padding=new Padding(0,0,0,12)};
        dashboardStats=stats;
        for(int i=0;i<3;i++)stats.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100f/3));
        stats.Controls.Add(controlsStat,0,0);stats.Controls.Add(networkStat,1,0);stats.Controls.Add(rateStat,2,0);grid.Controls.Add(stats,0,1);
        var caption=Theme.Label("DISPOSITIVOS  /  SINAL AO VIVO",10,true,Theme.Muted);
        caption.Padding=new Padding(0,11,0,0);grid.Controls.Add(caption,0,2);dashboardCardCaption=caption;
        var pads=new TableLayoutPanel {Dock=DockStyle.Fill,ColumnCount=2,RowCount=2};
        dashboardPadGrid=pads;
        for(int i=0;i<2;i++){pads.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,50));pads.RowStyles.Add(new RowStyle(SizeType.Percent,50));}
        for(int i=0;i<4;i++)pads.Controls.Add(cards[i],i%2,i/2);grid.Controls.Add(pads,0,3);
        var hint=Theme.Label("USB ou Bluetooth  •  Os indicadores mostram a entrada física, antes dos ajustes.",9,false,Theme.Muted);
        grid.Controls.Add(hint,0,4);dashboardHint=hint;
        return root;
    }
    private Panel BuildPreferences()
    {
        var root=new DarkScrollPanel {AutoScroll=true};
        var flow=new FlowLayoutPanel {Dock=DockStyle.Top,AutoSize=true,FlowDirection=FlowDirection.TopDown,WrapContents=false,Padding=new Padding(0,0,12,24)};root.Controls.Add(flow);
        flow.Controls.Add(Theme.Label("Do seu jeito.",24,true));flow.Controls.Add(Theme.Label("Ajustes globais para os quatro controles. Salvos automaticamente.",10,false,Theme.Muted));
        flow.Controls.Add(Theme.Label("LEITURA DOS CONTROLES",10,true,Theme.Green));
        var rate=new DarkComboBox {DropDownStyle=ComboBoxStyle.DropDownList,Width=470,Font=Theme.Font(11),BackColor=Theme.Surface,ForeColor=Theme.White};
        rate.Items.AddRange(["60 Hz — economia","120 Hz — equilibrado","250 Hz — leitura mais frequente"]);
        rate.SelectedIndex=settings.Input.PollHz==60?0:settings.Input.PollHz==250?2:1;
        rate.SelectedIndexChanged+=(_,_)=>UpdateInput(settings.Input with {PollHz=new[]{60,120,250}[rate.SelectedIndex]});flow.Controls.Add(rate);
        flow.Controls.Add(Theme.Label("Taxa alvo local; a taxa real depende do Windows e das solicitações do Xbox.",9,false,Theme.Muted));
        var deadzoneLabel=Theme.Label($"Zona morta radial: {settings.Input.Deadzone:P0}",11,true);flow.Controls.Add(deadzoneLabel);
        var deadzone=new DarkSlider {Minimum=0,Maximum=35,TickFrequency=5,Value=(int)Math.Round(settings.Input.Deadzone*100),Width=470,Height=45,BackColor=Theme.Background};
        deadzone.ValueChanged+=(_,_)=>{deadzoneLabel.Text=$"Zona morta radial: {deadzone.Value}%";UpdateInput(settings.Input with {Deadzone=deadzone.Value/100d});};flow.Controls.Add(deadzone);
        flow.Controls.Add(Theme.Label("Reduz movimentos involuntários próximos ao centro dos analógicos.",9,false,Theme.Muted));
        AddCheck(flow,"Trocar A ↔ B",settings.Input.SwapAB,v=>UpdateInput(settings.Input with {SwapAB=v}));
        AddCheck(flow,"Trocar X ↔ Y",settings.Input.SwapXY,v=>UpdateInput(settings.Input with {SwapXY=v}));
        AddCheck(flow,"Inverter eixo Y do analógico esquerdo",settings.Input.InvertLeftY,v=>UpdateInput(settings.Input with {InvertLeftY=v}));
        AddCheck(flow,"Inverter eixo Y do analógico direito",settings.Input.InvertRightY,v=>UpdateInput(settings.Input with {InvertRightY=v}));
        AddCheck(flow,"Permitir vibração",settings.Input.Vibration,v=>UpdateInput(settings.Input with {Vibration=v}));
        var tests=new FlowLayoutPanel {Width=730,Height=55,WrapContents=false};
        for(int i=0;i<4;i++){int slot=i;var b=Theme.Button($"Vibrar controle {i+1}");b.Width=164;b.Click+=(_,_)=>{if(!engine.Views[slot].Connected){MessageBox.Show("Conecte esse controle primeiro.","Teste de vibração");return;}engine.TestVibration(slot);};tests.Controls.Add(b);}flow.Controls.Add(tests);
        flow.Controls.Add(Theme.Label("O teste é local. A vibração enviada pelo Xbox depende do app e do controle.",9,false,Theme.Muted));
        flow.Controls.Add(Theme.Label("INTERFACE E VISUALIZAÇÃO",10,true,Theme.Green));
        AddCheck(flow,"Mostrar nível de bateria quando disponível",settings.ShowBattery,v=>{settings=settings with {ShowBattery=v};SaveSettings();ApplyUiVisibility();});
        AddCheck(flow,"Mostrar família e origem dos controles",settings.ShowFamily,v=>{settings=settings with {ShowFamily=v};SaveSettings();ApplyUiVisibility();});
        AddCheck(flow,"Mostrar respostas por segundo",settings.ShowRate,v=>{settings=settings with {ShowRate=v};SaveSettings();ApplyUiVisibility();});
        AddCheck(flow,"Mostrar dicas e descrições",settings.ShowHints,v=>{settings=settings with {ShowHints=v};SaveSettings();ApplyUiVisibility();});
        AddCheck(flow,"Mostrar cartões detalhados dos controles",settings.ShowControllerCards,v=>{settings=settings with {ShowControllerCards=v};SaveSettings();ApplyUiVisibility();});
        flow.Controls.Add(Theme.Label("A bateria pode mostrar Baixa, Média, Cheia, Via cabo ou Desconhecida. O SDL/XInput nem sempre informa percentual exato.",9,false,Theme.Muted));
        flow.Controls.Add(Theme.Label("JANELA E SEGUNDO PLANO",10,true,Theme.Green));
        AddCheck(flow,"Ao fechar a janela, manter na bandeja",settings.CloseToTray,v=>{settings=settings with {CloseToTray=v};SaveSettings();});
        AddCheck(flow,"Abrir minimizado na próxima execução",settings.StartMinimized,v=>{settings=settings with {StartMinimized=v};SaveSettings();});
        flow.Controls.Add(Theme.Label("A captura continua ao mudar de janela. Para encerrar: bandeja → Sair.",9,false,Theme.Muted));
        var reset=Theme.Button("Restaurar configurações padrão");
        reset.Width=310;
        reset.Click+=(_,_)=>RestoreDefaults();
        flow.Controls.Add(Theme.Label("REDEFINIR O APLICATIVO",10,true,Theme.Green));
        flow.Controls.Add(Theme.Label("Restaura os ajustes da interface e dos controles. O aplicativo fechará para você abri-lo novamente.",9,false,Theme.Muted));
        flow.Controls.Add(reset);
        return root;
    }
    private Panel BuildDiagnostics()
    {
        var root=new Panel();var top=new FlowLayoutPanel {Dock=DockStyle.Top,Height=255,FlowDirection=FlowDirection.TopDown,WrapContents=false};
        top.Controls.Add(Theme.Label("Tudo às claras.",24,true));top.Controls.Add(Theme.Label("Conexões, detecção de controles e erros — sem coleta remota de dados.",10,false,Theme.Muted));
        var actions=new FlowLayoutPanel {Width=780,Height=50};var export=Theme.Button("Exportar diagnóstico",true);export.Width=190;
        export.Click+=(_,_)=>Export();var clear=Theme.Button("Limpar registros");clear.Click+=(_,_)=>{logs.Clear();logBox.Clear();};
        var scan=Theme.Button("Atualizar IPs");scan.Click+=(_,_)=>RefreshAddresses();actions.Controls.Add(export);actions.Controls.Add(clear);actions.Controls.Add(scan);
        top.Controls.Add(actions);
        top.Controls.Add(Theme.Label("DETECÇÃO SDL E XINPUT",10,true,Theme.Green));
        top.Controls.Add(inputDiagnostic);
        root.Controls.Add(logBox);root.Controls.Add(top);return root;
    }
    private Panel BuildHelp()
    {
        var root=new DarkScrollPanel { AutoScrollMinSize=new Size(760,850) };
        var flow=new FlowLayoutPanel
        {
            Dock=DockStyle.Top,AutoSize=true,FlowDirection=FlowDirection.TopDown,
            WrapContents=false,Padding=new Padding(0,0,25,35)
        };
        root.Controls.Add(flow);
        flow.Controls.Add(Theme.Label("Ajuda e conexão.",24,true));
        flow.Controls.Add(Theme.Label("PASSO A PASSO PARA CONECTAR",11,true,Theme.Green));
        foreach(var step in new[]
        {
            "1. Ligue o PC e conecte um controle compatível por USB ou Bluetooth.",
            "2. Abra o Oberon Extended. No Painel, copie o endereço IP exibido.",
            "3. Ligue o Xbox e abra o aplicativo Oberon Remote Input.",
            "4. Informe o IP do PC no aplicativo Xbox e faça a conexão.",
            "5. Mantenha o PC e o Xbox na mesma rede local.",
            "6. Se aparecer o Firewall do Windows, permita na rede privada.",
            "7. Você pode minimizar o Oberon Extended para a bandeja do Windows."
        })
        {
            var label=Theme.Label(step,11,false,Theme.White);
            label.Margin=new Padding(0,10,0,8);
            flow.Controls.Add(label);
        }
        flow.Controls.Add(Theme.Label("DÚVIDAS FREQUENTES",11,true,Theme.Green));
        foreach(var (title,description) in new (string,string)[]
        {
            ("SDL ou XInput?",
             "SDL funciona com diferentes famílias de controles. XInput é geralmente a melhor alternativa para controles Xbox e modelos compatíveis. Se o SDL não detectar seu controle, o aplicativo pode recomendar XInput."),
            ("O que significam 60 Hz, 120 Hz e 250 Hz?",
             "São frequências de leitura dos controles no PC. Não aumentam os FPS dos jogos ou a velocidade do Wi-Fi."),
            ("A bateria aparece como desconhecida.",
             "Alguns controles, principalmente modelos paralelos, não enviam dados de bateria. Isso não significa que a bateria esteja vazia."),
            ("Como funciona o controle por teclado e mouse?",
             "A função fica em Experimental. Você pode personalizar as teclas e usar F8 para ligar ou desligar o controle virtual. Desligue com F8 antes de digitar normalmente."),
            ("O que é o modo bandeja?",
             "O aplicativo permanece executando em segundo plano até você escolher Sair no ícone da bandeja."),
            ("O que faz a opção de IP fixo?",
             "Ela limita a escuta a um IP local do PC. Para impedir que o roteador mude o endereço, configure uma reserva DHCP no roteador.")
        })
        {
            flow.Controls.Add(Theme.Label(title,11,true,Theme.White));
            var descriptionLabel=Theme.Label(description,10,false,Theme.Muted);
            descriptionLabel.MaximumSize=new Size(705,0);
            descriptionLabel.Margin=new Padding(0,1,0,14);
            flow.Controls.Add(descriptionLabel);
        }
        return root;
    }
    private static void AddCheck(FlowLayoutPanel panel,string text,bool initial,Action<bool> action)
    {var c=new DarkCheckBox {Text=text,Checked=initial,Width=710,Height=34,AutoSize=false,ForeColor=Theme.White,Font=Theme.Font(11),Margin=new Padding(0,3,0,5)};c.CheckedChanged+=(_,_)=>action(c.Checked);panel.Controls.Add(c);}
    private void UpdateInput(InputOptions value) {settings=settings with {Input=value};engine.Options=value;SaveSettings();}
    private void SaveSettings() {try{settings.Save();}catch(Exception e){AddLog("Não foi possível salvar ajustes: "+e.Message);}}
    private void StartServer()
    {try{server.Start(26401,settings.PinAddress?settings.PinnedIp:"0.0.0.0");engine.CaptureEnabled=true;}catch(Exception e){AddLog("Não foi possível iniciar servidor: "+e.Message);MessageBox.Show("Não foi possível abrir a porta 26401. Feche o Oberon antigo e tente novamente.\n\n"+e.Message,"Oberon Extended");}}
    private void TogglePause() {server.Paused=!server.Paused;AddLog(server.Paused?"Comandos pausados; enviando estado neutro.":"Comandos retomados.");RefreshStatus();}
    private void ShowPage(string name)
    {
        page=name;dashboard.Visible=name=="Painel";preferences.Visible=name=="Configurações";diagnostics.Visible=name=="Diagnóstico";help.Visible=name=="Ajuda";ShowExtendedPage(name);
        foreach(var (key,b) in navigation){b.BackColor=key==name?Theme.Green:Theme.Surface;b.ForeColor=key==name?Theme.Background:Theme.Muted;}
        refresh.Interval=name=="Painel"&&Visible?50:250;
    }
    private void RefreshAddresses()
    {
        var selected=addresses.SelectedItem as string;addresses.Items.Clear();
        try
        {
            foreach(var ip in NetworkInterface.GetAllNetworkInterfaces().Where(n=>n.OperationalStatus==OperationalStatus.Up&&n.NetworkInterfaceType!=NetworkInterfaceType.Loopback)
                .SelectMany(n=>n.GetIPProperties().UnicastAddresses).Where(a=>a.Address.AddressFamily==AddressFamily.InterNetwork).Select(a=>a.Address.ToString()).Distinct())addresses.Items.Add(ip);
        }
        catch(Exception e){AddLog(e.Message);}
        if(addresses.Items.Count==0)addresses.Items.Add("Sem IPv4 disponível");
        string? preferred=settings.PinAddress?settings.PinnedIp:selected;
        addresses.SelectedIndex=preferred!=null&&addresses.Items.Contains(preferred)?addresses.Items.IndexOf(preferred):0;
        UpdatePinnedAddresses();
    }
    private void RefreshStatus()
    {
        while(incomingLogs.TryDequeue(out var entry)){logs.Add(entry);if(logs.Count>400)logs.RemoveAt(0);}
        if(page=="Diagnóstico"&&Visible)
        {
            if(inputDiagnostic.Text!=engine.Diagnostics)inputDiagnostic.Text=engine.Diagnostics;
            var text=string.Join(Environment.NewLine,logs);if(logBox.Text!=text){logBox.Text=text;logBox.SelectionStart=logBox.TextLength;logBox.ScrollToCaret();}}
        string state=engine.Error!=null?"ERRO NA CAPTURA":!server.Running?"SERVIDOR PARADO":server.Paused?"COMANDOS PAUSADOS":server.Clients>0&&server.RequestAgeSeconds<2?"XBOX CONECTADO":"AGUARDANDO XBOX";
        status.Text=state;pause.Text=server.Paused?"Retomar comandos":"Pausar comandos";
        start.Text=server.Running?"Desconectar":"Conectar";
        var current=engine.Views;
        if(Visible&&page=="Painel")
        {
            controlsStat.Value=$"{current.Count(p=>p.Connected)} / 4";networkStat.Value=server.Clients>0&&server.RequestAgeSeconds<2?"Ativa":server.Running?"Pronta":"Parada";
            for(int i=0;i<4;i++)
            {
                cards[i].State=current[i];
                cards[i].ShowBattery=settings.ShowBattery;
                cards[i].ShowFamily=settings.ShowFamily;
                if(settings.ShowControllerCards)cards[i].Invalidate();
            }
            controlsStat.Invalidate();networkStat.Invalidate();
        }
        UpdateControllerStatus();
        SuggestXInputIfAppropriate();
        double elapsed=Stopwatch.GetElapsedTime(lastSample).TotalSeconds;
        if(elapsed>=1)
        {long responses=server.Responses;rateStat.Value=((responses-lastResponses)/elapsed).ToString("F0");lastResponses=responses;lastSample=Stopwatch.GetTimestamp();if(Visible&&page=="Painel")rateStat.Invalidate();}
    }
    private void AddLog(string message) {incomingLogs.Enqueue($"{DateTime.Now:HH:mm:ss}  {message}");while(incomingLogs.Count>500)incomingLogs.TryDequeue(out _);}
    private void Export()
    {
        using var dialog=new SaveFileDialog {Filter="Texto (*.txt)|*.txt",FileName="Oberon_Extended_Diagnostics.txt"};
        if(dialog.ShowDialog()!=DialogResult.OK)return;
        try
        {
            var report=new StringBuilder("Oberon Extended 1.0\n");report.AppendLine(DateTimeOffset.Now.ToString("O"));
            report.AppendLine("Windows: "+Environment.OSVersion);report.AppendLine("Runtime: "+Environment.Version);
            report.AppendLine("Detecção de controles: "+engine.Diagnostics);
            report.AppendLine("Ajustes: "+settings.Input);report.AppendLine("Servidor: "+server.Running+" / conexões: "+server.Clients);
            for(int i=0;i<4;i++)report.AppendLine($"Controle {i+1}: {engine.Views[i].Name} / conectado={engine.Views[i].Connected}");
            report.AppendLine(string.Join(Environment.NewLine,logs));File.WriteAllText(dialog.FileName,report.ToString());
        }
        catch(Exception e){MessageBox.Show(e.Message,"Erro ao exportar");}
    }
    private void HideToTray(){Hide();refresh.Interval=500;tray.ShowBalloonTip(2200,"Oberon Extended","Os controles continuam ativos. Clique no ícone para abrir.",ToolTipIcon.Info);}
    private void RestoreWindow(){Show();WindowState=FormWindowState.Normal;Activate();ShowPage(page);}
    internal void SavePreviewAndClose(string path)
    {
        RefreshStatus();
        if(engine.Error!=null)throw new InvalidOperationException(engine.Error);
        using var bitmap=new Bitmap(Width,Height);DrawToBitmap(bitmap,new Rectangle(0,0,Width,Height));
        bitmap.Save(path,System.Drawing.Imaging.ImageFormat.Png);
        exiting=true;Close();
    }
    private void OnClosing(object? sender,FormClosingEventArgs e)
    {
        if(!exiting&&settings.CloseToTray&&e.CloseReason==CloseReason.UserClosing){e.Cancel=true;HideToTray();return;}
        refresh.Stop();tray.Visible=false;engine.Dispose();server.Dispose();refresh.Dispose();tray.Dispose();trayIcon.Dispose();
    }
    private static Icon MakeIcon()
    {
        // Read the same embedded Win32 icon used by Explorer, the title bar and the taskbar.
        // The Windows apphost receives Assets/oberon-extended.ico during publishing.
        return Icon.ExtractAssociatedIcon(Application.ExecutablePath) ??
            (Icon)SystemIcons.Application.Clone();
    }
}

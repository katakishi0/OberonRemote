using System.Diagnostics;
using System.Drawing;

namespace Oberon.Extended;

// Features extend the existing Green Desktop UI without replacing its layout.
internal sealed partial class MainWindow
{
    private readonly Panel themesPage = new();
    private readonly Panel experimentalPage = new();
    private readonly Panel updatesPage = new();
    private readonly Panel creditsPage = new();
    private Label? experimentalStatus;
    private Label? updateStatus;
    private readonly DarkComboBox pinnedChoice = new()
    {
        Width = 460, DropDownStyle = ComboBoxStyle.DropDownList,
        BackColor = Theme.Surface, ForeColor = Theme.White, Font = Theme.Font(11)
    };

    private void SetupExtendedNavigation(FlowLayoutPanel sidebar)
    {
        foreach (string name in new[] { "Temas", "Experimental", "Atualizações", "Ajuda", "Créditos" })
        {
            var button = Theme.Button(name);
            button.Width = 166;
            button.Height = 43;
            button.TextAlign = ContentAlignment.MiddleLeft;
            button.Padding = new Padding(10, 0, 0, 0);
            button.Click += (_, _) => ShowPage(name);
            navigation[name] = button;
            sidebar.Controls.Add(button);
        }
    }

    private void InitializeExtended()
    {
        Text = "Oberon Extended | Desktop Remote";
        Theme.Animations = settings.Animations;
        SetupControllerShortcuts();
        Attach(themesPage, BuildThemes());
        Attach(experimentalPage, BuildExperiments());
        Attach(updatesPage, BuildUpdates());
        Attach(creditsPage, BuildCredits());
        Theme.Switch(settings.ThemeName, settings.AccentHex, this);
    }

    private void Attach(Panel page, Control child)
    {
        page.Dock = DockStyle.Fill;
        page.Visible = false;
        child.Dock = DockStyle.Fill;
        page.Controls.Add(child);
        content.Controls.Add(page);
    }

    private void ApplyUiVisibility()
    {
        foreach (var card in cards)
        {
            card.ShowBattery = settings.ShowBattery;
            card.ShowFamily = settings.ShowFamily;
            card.Invalidate();
        }
        if (dashboardStats is not null)
        {
            dashboardStats.SuspendLayout();
            dashboardStats.Controls.Clear();
            dashboardStats.ColumnStyles.Clear();
            int columns = settings.ShowRate ? 3 : 2;
            dashboardStats.ColumnCount = columns;
            for (int i = 0; i < columns; i++)
                dashboardStats.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f / columns));
            dashboardStats.Controls.Add(controlsStat, 0, 0);
            dashboardStats.Controls.Add(networkStat, 1, 0);
            if (settings.ShowRate) dashboardStats.Controls.Add(rateStat, 2, 0);
            dashboardStats.ResumeLayout();
        }
        if (dashboardGrid is not null)
        {
            dashboardGrid.Height = settings.ShowControllerCards ? 760 : 330;
            dashboardGrid.RowStyles[2].SizeType = SizeType.Absolute;
            dashboardGrid.RowStyles[2].Height = settings.ShowControllerCards ? 42 : 0;
            dashboardGrid.RowStyles[3].SizeType = settings.ShowControllerCards ? SizeType.Percent : SizeType.Absolute;
            dashboardGrid.RowStyles[3].Height = settings.ShowControllerCards ? 100 : 0;
        }
        if (dashboardPadGrid is not null) dashboardPadGrid.Visible = settings.ShowControllerCards;
        if (dashboardCardCaption is not null) dashboardCardCaption.Visible = settings.ShowControllerCards;
        if (dashboardHint is not null) dashboardHint.Visible = settings.ShowHints;
        if (dashboardSubtitle is not null) dashboardSubtitle.Visible = settings.ShowHints;
        if (dashboardRoot is not null)
            dashboardRoot.AutoScrollMinSize = new Size(760, settings.ShowControllerCards ? 760 : 330);
    }

    private void ShowExtendedPage(string name)
    {
        themesPage.Visible = name == "Temas";
        experimentalPage.Visible = name == "Experimental";
        updatesPage.Visible = name == "Atualizações";
        creditsPage.Visible = name == "Créditos";
    }

    private static (Panel Root, FlowLayoutPanel Flow) NewScrollablePage()
    {
        var root = new DarkScrollPanel { AutoScroll = true };
        var flow = new FlowLayoutPanel
        {
            Dock = DockStyle.Top, AutoSize = true,
            FlowDirection = FlowDirection.TopDown, WrapContents = false,
            Padding = new Padding(0, 0, 12, 30), AutoScroll = false
        };
        root.Controls.Add(flow);
        return (root, flow);
    }

    private Control BuildThemes()
    {
        var (root, flow) = NewScrollablePage();
        flow.Controls.Add(Theme.Label("Do seu jeito.", 24, true));
        flow.Controls.Add(Theme.Label("Temas e personalização ao vivo, sem interromper os controles.", 10, false, Theme.Muted));
        flow.Controls.Add(Theme.Label("PALETA DE CORES", 10, true, Theme.Green));
        var list = new DarkComboBox
        {
            Width = 445, DropDownStyle = ComboBoxStyle.DropDownList,
            Font = Theme.Font(11), BackColor = Theme.Surface, ForeColor = Theme.White
        };
        list.Items.AddRange(new object[] { "Xbox Green", "Xbox Light", "Forest", "Midnight", "Violet", "Graphite", "Custom" });
        list.SelectedItem = list.Items.Contains(settings.ThemeName) ? settings.ThemeName : "Xbox Green";
        list.SelectedIndexChanged += (_, _) =>
        {
            settings = settings with { ThemeName = list.SelectedItem?.ToString() ?? "Xbox Green" };
            ApplyTheme();
        };
        flow.Controls.Add(list);
        var color = Theme.Button("Escolher cor personalizada", true);
        color.Width = 252;
        color.Click += (_, _) =>
        {
            using var dialog = new ColorDialog { FullOpen = true, Color = Theme.Green };
            if (dialog.ShowDialog() != DialogResult.OK) return;
            settings = settings with
            {
                AccentHex = ColorTranslator.ToHtml(dialog.Color),
                ThemeName = "Custom"
            };
            list.SelectedItem = "Custom";
            ApplyTheme();
        };
        flow.Controls.Add(color);
        AddCheck(flow, "Transições suaves nos botões", settings.Animations, enabled =>
        {
            settings = settings with { Animations = enabled };
            Theme.Animations = enabled;
            SaveSettings();
        });
        return root;
    }

    private void ApplyTheme()
    {
        Theme.Switch(settings.ThemeName, settings.AccentHex, this);
        foreach (var (_, button) in navigation)
        {
            button.BackColor = Theme.Surface;
            button.ForeColor = Theme.Muted;
        }
        if (navigation.TryGetValue(page, out var active))
        {
            active.BackColor = Theme.Green;
            active.ForeColor = Theme.Background;
        }
        SaveSettings();
    }

    private Control BuildExperiments()
    {
        var (root, flow) = NewScrollablePage();
        flow.Controls.Add(Theme.Label("Laboratório experimental.", 24, true));
        flow.Controls.Add(Theme.Label("Opções para testes. Não aumentam a potência do sinal Wi-Fi.", 10, false, Theme.Muted));
        flow.Controls.Add(Theme.Label("DETECÇÃO DOS CONTROLES", 10, true, Theme.Green));
        flow.Controls.Add(Theme.Label("O Windows pode reconhecer controles Xbox que não aparecem no SDL.", 10, false, Theme.Muted));
        var backend = new DarkComboBox
        {
            Width = 495, DropDownStyle = ComboBoxStyle.DropDownList,
            BackColor = Theme.Surface, ForeColor = Theme.White, Font = Theme.Font(11)
        };
        backend.Items.AddRange(new object[] { "Automático: SDL + XInput", "Priorizar XInput para Xbox", "Apenas SDL (original)" });
        backend.SelectedIndex = (int)settings.XboxMode;
        backend.SelectedIndexChanged += (_, _) =>
        {
            var selected = (XboxInputMode)backend.SelectedIndex;
            settings = settings with { XboxMode = selected };
            engine.XboxBackend = selected;
            OnBackendSelected();
            SaveSettings();
            AddLog("Modo de captura alterado: " + selected + ". O controle será reavaliado.");
        };
        flow.Controls.Add(backend);
        var scanInfo = Theme.Button("Diagnóstico de controles");
        scanInfo.Width = 245;
        scanInfo.Click += (_, _) => ShowPage("Diagnóstico");
        flow.Controls.Add(scanInfo);
        flow.Controls.Add(Theme.Label("Se o Xbox mostrar 0/4, tente Priorizar XInput. A conexão é somente por controle, não por Wi-Fi.", 9, false, Theme.Muted));
        AddKeyboardOptions(flow);
        flow.Controls.Add(Theme.Label("AJUSTES EXPERIMENTAIS", 10, true, Theme.Green));
        AddCheck(flow, "Turbo A (enquanto pressionado)", settings.Input.TurboA,
            enabled => UpdateInput(settings.Input with { TurboA = enabled }));
        AddCheck(flow, "Turbo B (enquanto pressionado)", settings.Input.TurboB,
            enabled => UpdateInput(settings.Input with { TurboB = enabled }));
        var turbo = new DarkComboBox
        {
            Width = 480, DropDownStyle = ComboBoxStyle.DropDownList,
            Font = Theme.Font(11), BackColor = Theme.Surface, ForeColor = Theme.White
        };
        turbo.Items.AddRange(new object[] { "Turbo 4 Hz", "Turbo 8 Hz", "Turbo 12 Hz" });
        turbo.SelectedIndex = settings.Input.TurboHz <= 4 ? 0 : settings.Input.TurboHz >= 12 ? 2 : 1;
        turbo.SelectedIndexChanged += (_, _) =>
            UpdateInput(settings.Input with { TurboHz = new[] { 4, 8, 12 }[turbo.SelectedIndex] });
        flow.Controls.Add(turbo);
        var sensitivityLabel = Theme.Label("Sensibilidade dos analógicos: " + settings.Input.StickSensitivity.ToString("F2") + "x", 11, true);
        flow.Controls.Add(sensitivityLabel);
        var sensitivity = new DarkSlider
        {
            Width = 480, Minimum = 50, Maximum = 150, TickFrequency = 10,
            Value = (int)(Math.Clamp(settings.Input.StickSensitivity, .5, 1.5) * 100), BackColor = Theme.Background
        };
        sensitivity.ValueChanged += (_, _) =>
        {
            var level = sensitivity.Value / 100.0;
            sensitivityLabel.Text = "Sensibilidade dos analógicos: " + level.ToString("F2") + "x";
            UpdateInput(settings.Input with { StickSensitivity = level });
        };
        flow.Controls.Add(sensitivity);
        flow.Controls.Add(Theme.Label("Turbo e sensibilidade modificam apenas os comandos locais. Turbo desativado por padrão.", 9, false, Theme.Muted));
        var highPoll = Theme.Button("Captura local 250 Hz");
        highPoll.Width = 236;
        highPoll.Click += (_, _) => UpdateInput(settings.Input with { PollHz = 250 });
        flow.Controls.Add(highPoll);
        flow.Controls.Add(Theme.Label("A taxa de resposta real depende do Xbox e da rede; isto não aumenta o sinal Wi-Fi.", 9, false, Theme.Muted));
        flow.Controls.Add(Theme.Label("EXPERIMENTOS DE REDE", 10, true, Theme.Green));
        AddCheck(flow, "Fixar servidor ao IPv4 escolhido no painel", settings.PinAddress, enabled =>
        {
            string selected = pinnedChoice.SelectedItem as string ?? "";
            settings = settings with
            {
                PinAddress = enabled && RemoteServer.IsLocalBindAddress(selected),
                PinnedIp = enabled ? selected : ""
            };
            SaveSettings();
        });
        flow.Controls.Add(Theme.Label("Endereço IPv4 da interface:", 10, true));
        flow.Controls.Add(pinnedChoice);
        var apply = Theme.Button("Aplicar IP do servidor", true);
        apply.Width = 240;
        apply.Click += (_, _) => ApplyBinding();
        flow.Controls.Add(apply);
        experimentalStatus = Theme.Label("O endereço fixado precisa continuar atribuído a este PC.", 9, false, Theme.Muted);
        flow.Controls.Add(experimentalStatus);
        flow.Controls.Add(Theme.Label("Alterar o endereço desconecta o Xbox. Para manter o mesmo IP após mudanças na rede, configure uma reserva DHCP ou IP estático.", 9, false, Theme.Muted));
        flow.Controls.Add(Theme.Label("COMPATIBILIDADE", 10, true, Theme.Green));
        var mapping = Theme.Button("Importar mapeamentos SDL");
        mapping.Width = 240;
        mapping.Click += (_, _) => ImportMappings();
        flow.Controls.Add(mapping);
        flow.Controls.Add(Theme.Label("DualShock, DualSense, Switch Pro, Joy-Con e Xbox usam mapeamentos SDL quando reconhecidos. Outros modelos podem usar controller-mappings.txt.", 9, false, Theme.Muted));
        return root;
    }

    private void UpdatePinnedAddresses()
    {
        string preferred = settings.PinnedIp;
        pinnedChoice.Items.Clear();
        foreach (var value in addresses.Items)
            if (value is string address && System.Net.IPAddress.TryParse(address, out _))
                pinnedChoice.Items.Add(address);
        if (pinnedChoice.Items.Count > 0)
            pinnedChoice.SelectedItem = pinnedChoice.Items.Contains(preferred) ? preferred : pinnedChoice.Items[0];
    }

    private void ApplyBinding()
    {
        string selected = pinnedChoice.SelectedItem as string ?? "";
        if (settings.PinAddress && !RemoteServer.IsLocalBindAddress(selected))
        {
            MessageBox.Show("Selecione um IPv4 válido no painel antes de aplicar.", "Vínculo de rede");
            return;
        }
        settings = settings with { PinnedIp = settings.PinAddress ? selected : "" };
        if (settings.PinAddress && addresses.Items.Contains(selected)) addresses.SelectedItem = selected;
        SaveSettings();
        server.Stop();
        engine.CaptureEnabled=false;
        StartServer();
        if (experimentalStatus != null)
            experimentalStatus.Text = server.Running
                ? "Vinculado a " + (settings.PinAddress ? settings.PinnedIp : "all IPv4 interfaces")
                : "Falha ao vincular IP. Consulte o diagnóstico.";
        AddLog("Vínculo de rede updated. The Xbox must reconnect.");
    }

    private void ImportMappings()
    {
        using var dialog = new OpenFileDialog { Filter = "SDL mapping files (*.txt)|*.txt|All files (*.*)|*.*" };
        if (dialog.ShowDialog() != DialogResult.OK) return;
        try
        {
            Directory.CreateDirectory(AppSettings.DirectoryPath);
            var destination = Path.Combine(AppSettings.DirectoryPath, "controller-mappings.txt");
            if (!Path.GetFullPath(dialog.FileName).Equals(Path.GetFullPath(destination), StringComparison.OrdinalIgnoreCase))
                File.Copy(dialog.FileName, destination, true);
            engine.ImportMappings(destination);
            AddLog("Mapeamentos SDL importados. Reconecte os controles para aplicar.");
        }
        catch (Exception ex) { MessageBox.Show(ex.Message, "Mapeamentos SDL"); }
    }

    private Control BuildUpdates()
    {
        var (root, flow) = NewScrollablePage();
        flow.Controls.Add(Theme.Label("Versão e atualizações.", 24, true));
        flow.Controls.Add(Theme.Label("OBERON EXTENDED " + ExtendedVersion.Number, 12, true, Theme.Green));
        flow.Controls.Add(Theme.Label("Verificação manual das versões do GitHub, sem downloads ou instalações silenciosas.", 10, false, Theme.Muted));
        updateStatus = Theme.Label("Pronto para verificar.", 11, false);
        flow.Controls.Add(updateStatus);
        AddCheck(flow, "Verificar versões ao iniciar", settings.CheckUpdatesAtStartup, enabled =>
        {
            settings = settings with { CheckUpdatesAtStartup = enabled };
            SaveSettings();
        });
        var check = Theme.Button("Verificar atualizações", true);
        check.Width = 252;
        check.Click += async (_, _) =>
        {
            check.Enabled = false;
            try { await CheckUpdatesAsync(); }
            finally { check.Enabled = true; }
        };
        flow.Controls.Add(check);
        var releases = Theme.Button("Abrir página de versões");
        releases.Click += (_, _) => ExtendedVersion.Open(ExtendedVersion.Releases);
        flow.Controls.Add(releases);
        return root;
    }

    private async Task CheckUpdatesAsync()
    {
        if (updateStatus is null) return;
        updateStatus.Text = "Consultando versões do GitHub...";
        try
        {
            var result = await ExtendedVersion.CheckForUpdatesAsync();
            updateStatus.Text = result.Status;
        }
        catch (Exception ex) { updateStatus.Text = "Não foi possível verificar: " + ex.Message; }
    }

    private Control BuildCredits()
    {
        var (root, flow) = NewScrollablePage();
        flow.Controls.Add(Theme.Label("Baseado no Oberon.", 24, true));
        flow.Controls.Add(Theme.Label("OBERON EXTENDED", 11, true, Theme.Green));
        flow.Controls.Add(Theme.Label("Fork e manutenção: katakishi0", 11));
        flow.Controls.Add(Theme.Label("PROJETO ORIGINAL", 11, true, Theme.Green));
        flow.Controls.Add(Theme.Label("Oberon Remote Input por SamsidParty e colaboradores, incluindo SamarthMP.", 11));
        flow.Controls.Add(Theme.Label("Aplicativo do Xbox e protocolo WebSocket originais preservados.", 10, false, Theme.Muted));
        flow.Controls.Add(Theme.Label("TECNOLOGIAS UTILIZADAS", 11, true, Theme.Green));
        flow.Controls.Add(Theme.Label("SDL2 / MonoGame.Library.SDL — captura de controles", 10));
        flow.Controls.Add(Theme.Label("Fleck — servidor WebSocket local", 10));
        flow.Controls.Add(Theme.Label(".NET / Windows Forms — execução e interface", 10));
        flow.Controls.Add(Theme.Label("A licença original exige código modificado público e não ofuscado e proíbe publicação de versões modificadas na Microsoft Store.", 9, false, Theme.Muted));
        var upstream = Theme.Button("Repositório original"); upstream.Width = 232;
        upstream.Click += (_, _) => ExtendedVersion.Open("https://github.com/SamsidParty/OberonRemote");
        var fork = Theme.Button("Código Oberon Extended"); fork.Width = 232;
        fork.Click += (_, _) => ExtendedVersion.Open(ExtendedVersion.Project);
        flow.Controls.Add(upstream); flow.Controls.Add(fork);
        return root;
    }
}

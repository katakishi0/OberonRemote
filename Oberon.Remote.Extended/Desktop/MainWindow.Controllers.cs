using System.Diagnostics;

namespace Oberon.Extended;

// Shared controller actions and opt-in keyboard/mouse options.
// Controller cards remain on the dashboard; no separate Controllers page.
internal sealed partial class MainWindow
{
    private readonly Label virtualStatus = Theme.Label("Controle virtual desativado", 10, true, Theme.Muted);
    private long sdlSelectionAt = Stopwatch.GetTimestamp();
    private bool promptedForCurrentSDLSelection;

    private void OnBackendSelected()
    {
        promptedForCurrentSDLSelection = false;
        sdlSelectionAt = Stopwatch.GetTimestamp();
    }

    private void SuggestXInputIfAppropriate()
    {
        if (promptedForCurrentSDLSelection || settings.SuppressXInputSuggestion ||
            settings.XboxMode != XboxInputMode.SdlOnly ||
            engine.AvailableXInputControllers == 0 ||
            Stopwatch.GetElapsedTime(sdlSelectionAt).TotalSeconds < 3 ||
            engine.Views.Any(v => v.Connected && v.Source == "SDL" &&
                (v.Family == "Xbox" || v.Name.Contains("Xbox", StringComparison.OrdinalIgnoreCase))))
            return;

        promptedForCurrentSDLSelection = true;
        using var dialog = new XInputSuggestionDialog();
        dialog.ShowDialog(this);
        if (dialog.DontShowAgain)
        {
            settings = settings with { SuppressXInputSuggestion = true };
            SaveSettings();
        }
        if (dialog.ApplyXInput)
        {
            settings = settings with { XboxMode = XboxInputMode.PreferXInput };
            engine.XboxBackend = XboxInputMode.PreferXInput;
            SaveSettings();
            AddLog("XInput aplicado por escolha do usuário.");
        }
    }

    private void RestoreDefaults()
    {
        if (MessageBox.Show(
            "Todas as configurações salvas serão restauradas ao padrão, incluindo controles, temas e avisos. " +
            "O aplicativo será fechado. Abra-o novamente para continuar.",
            "Restaurar configurações", MessageBoxButtons.YesNo,
            MessageBoxIcon.Question) != DialogResult.Yes) return;
        try
        {
            new AppSettings(new InputOptions()).Save();
        }
        catch (Exception ex)
        {
            MessageBox.Show("Não foi possível restaurar: " + ex.Message,
                "Oberon Extended", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }
        exiting = true;
        Close();
    }


    private void SetupControllerShortcuts()
    {
        foreach (var card in cards)
        {
            card.ChangeBackend = ChangeXboxBackend;
            card.ExplainBackend = ExplainControllerBackend;
        }
    }

    private void ToggleConnection()
    {
        if (server.Running)
        {
            engine.CaptureEnabled = false;
            server.Stop();
            AddLog("Conexão desligada: servidor e comandos ao Xbox interrompidos.");
        }
        else
        {
            server.Paused = false;
            StartServer();
        }
        RefreshStatus();
    }

    private void ChangeXboxBackend()
    {
        // Choose based on the input source actually shown in the card. A saved
        // XInput preference may currently be falling back to SDL.
        bool currentlySdl = engine.Views.Any(v => v.Connected &&
            v.Family == "Xbox" && v.Source == "SDL");
        var next = currentlySdl ? XboxInputMode.PreferXInput : XboxInputMode.SdlOnly;
        settings = settings with { XboxMode = next };
        engine.XboxBackend = next;
        OnBackendSelected();
        SaveSettings();
        AddLog("Captura Xbox: " + (next == XboxInputMode.PreferXInput ? "XInput" : "SDL") + ".");
    }

    private void ExplainControllerBackend()
    {
        InformationDialog.Explain(this, "SDL e XInput",
            "XInput é recomendado para muitos controles Xbox Wireless Controller no Windows. " +
            "Se o dispositivo não funcionar corretamente em XInput, tente SDL.\n\n" +
            "A recomendação não altera suas configurações automaticamente. " +
            "Use o botão no cartão do controle para trocar.\n\n" +
            "Controles paralelos podem não informar o nível de bateria. " +
            "Desconhecido não significa que a bateria acabou.");
    }

    private void AddKeyboardOptions(FlowLayoutPanel flow)
    {
        flow.Controls.Add(Theme.Label("TECLADO E MOUSE", 11, true, Theme.Green));
        flow.Controls.Add(Theme.Label(
            "Opcional: emule um controle em um slot livre. Pressione F8 para ligar ou desligar a captura.", 
            10, false, Theme.Muted));
        AddCheck(flow, "Usar teclado como controle virtual", settings.VirtualKeyboard, enabled =>
        {
            settings = settings with { VirtualKeyboard = enabled };
            engine.VirtualEnabled = enabled;
            if (!enabled) engine.DisarmVirtual();
            SaveSettings();
        });
        AddCheck(flow, "Mover o analógico direito usando o mouse", settings.VirtualMouseAim, enabled =>
        {
            settings = settings with { VirtualMouseAim = enabled };
            engine.MouseAim = enabled;
            SaveSettings();
        });
        flow.Controls.Add(virtualStatus);
        var edit = Theme.Button("Personalizar teclas", true);
        edit.Width = 240;
        edit.Click += (_, _) =>
        {
            engine.DisarmVirtual();
            engine.VirtualEnabled = false;
            using var dialog = new KeyboardBindingsDialog(
                settings.VirtualKeys ?? VirtualKeyMapping.Default, updated =>
                {
                    settings = settings with { VirtualKeys = updated };
                    engine.VirtualMapping = updated;
                    SaveSettings();
                });
            try { dialog.ShowDialog(this); }
            finally { engine.DisarmVirtual(); engine.VirtualEnabled = settings.VirtualKeyboard; }
        };
        flow.Controls.Add(edit);

    }

    private void UpdateControllerStatus()
    {
        for (int i = 0; i < cards.Length; i++)
        {
            // Keep existing aliases from earlier previews without showing a separate editor.
            cards[i].Alias = i switch
            {
                0 => settings.ControllerName1,
                1 => settings.ControllerName2,
                2 => settings.ControllerName3,
                3 => settings.ControllerName4,
                _ => ""
            };
        }
        if (page == "Experimental")
        {
            virtualStatus.Text = !engine.VirtualEnabled ? "Controle virtual desativado" :
                engine.VirtualArmed ? "Controle virtual ativo (F8 para desativar)" :
                "Controle virtual pronto (pressione F8 para ativar)";
            virtualStatus.ForeColor = engine.VirtualArmed ? Theme.Green : Theme.Muted;
        }
    }
}

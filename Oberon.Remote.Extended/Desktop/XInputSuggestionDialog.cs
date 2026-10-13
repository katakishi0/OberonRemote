namespace Oberon.Extended;

// The suggestion never changes backends without explicit approval.
// Both accept and decline may persist "Don't show again" independently.
internal sealed class XInputSuggestionDialog : Form
{
    private readonly DarkCheckBox neverAgain;
    internal bool DontShowAgain => neverAgain.Checked;
    internal bool ApplyXInput { get; private set; }

    internal XInputSuggestionDialog()
    {
        Text = "Ajuda para detectar seu controle";
        ClientSize = new Size(570, 327);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterParent;
        BackColor = Theme.Background;
        ForeColor = Theme.White;
        Font = Theme.Font(11);

        var title = Theme.Label("Encontramos outra opção.", 19, true, Theme.Green);
        title.Location = new Point(24,20);
        Controls.Add(title);

        var message = new Label
        {
            Location = new Point(26,78),Size = new Size(519,118),
            ForeColor = Theme.White,Font = Theme.Font(11),
            Text = "O SDL não encontrou um controle Xbox ativo, mas o Windows identificou " +
                   "um dispositivo compatível com XInput.\n\n" +
                   "Deseja experimentar XInput? Isso não altera o Windows e você poderá " +
                   "voltar ao SDL quando quiser."
        };
        Controls.Add(message);

        neverAgain = new DarkCheckBox
        {
            Text = "Não mostrar esta sugestão novamente",
            Location = new Point(25,204),Width=490,Height=34
        };
        Controls.Add(neverAgain);

        var apply = Theme.Button("Aplicar XInput", true);
        apply.Location = new Point(311,265);
        apply.Width=233;
        apply.Click += (_,_) => {ApplyXInput=true;DialogResult=DialogResult.OK;Close();};
        Controls.Add(apply);

        var keep = Theme.Button("Continuar com SDL");
        keep.Location = new Point(25,265);
        keep.Width=246;
        keep.Click += (_,_) => {ApplyXInput=false;DialogResult=DialogResult.Cancel;Close();};
        Controls.Add(keep);
        AcceptButton = apply;
        CancelButton = keep;
        Shown += (_,_)=>WindowsTheme.SetDarkTitle(this);
    }
}

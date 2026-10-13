using System.Drawing;

namespace Oberon.Extended;

// Modal, user-friendly key assignment editor. F8 is reserved for arm/disarm.
// All changes are persisted by the caller; no global keyboard hooks installed.
internal sealed class KeyboardBindingsDialog : Form
{
    private VirtualKeyMapping mapping;
    private readonly Action<VirtualKeyMapping> apply;
    private readonly Dictionary<string, Button> buttons = new();
    private string? capturing;
    private Button? capturingButton;
    private readonly Label feedback = Theme.Label("Clique em uma ação e pressione a nova tecla.", 10, false, Theme.Muted);

    public KeyboardBindingsDialog(VirtualKeyMapping initial, Action<VirtualKeyMapping> onChanged)
    {
        mapping = initial;
        apply = onChanged;
        Text = "Configurar teclado - Oberon Extended";
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(710, 670);
        MinimumSize = new Size(620, 500);
        BackColor = Theme.Background;
        ForeColor = Theme.White;
        Font = Theme.Font(11);
        KeyPreview = true;
        ShowInTaskbar = false;

        var header = new Panel { Dock = DockStyle.Top, Height = 122, Padding = new Padding(22, 18, 12, 4) };
        header.Controls.Add(new Label
        {
            Text="Personalizar teclas", Font=Theme.Font(20,true),
            ForeColor=Theme.White, Location=new Point(20,15),AutoSize=true
        });
        header.Controls.Add(new Label
        {
            Text="Clique em Alterar, pressione uma tecla e o ajuste será salvo.",
            Font=Theme.Font(10),ForeColor=Theme.Muted,Location=new Point(22,58),AutoSize=true
        });
        feedback.Location = new Point(22,91);
        header.Controls.Add(feedback);
        Controls.Add(header);

        var footer = new FlowLayoutPanel
        {
            Dock=DockStyle.Bottom,Height=67,FlowDirection=FlowDirection.RightToLeft,
            Padding=new Padding(20,10,15,0)
        };
        var close=Theme.Button("Concluir",true);close.Width=143;close.Click+=(_,_)=>Close();
        var restore=Theme.Button("Restaurar padrão");restore.Width=180;
        restore.Click+=(_,_)=>
        {
            capturing=null; capturingButton=null;
            mapping=VirtualKeyMapping.Default;
            apply(mapping);
            RefreshLabels();
            feedback.Text="Mapeamento padrão restaurado e salvo.";
        };
        footer.Controls.Add(close);footer.Controls.Add(restore);
        Controls.Add(footer);

        var scroll=new Panel { Dock=DockStyle.Fill,AutoScroll=true,Padding=new Padding(22,2,18,20) };
        var list=new FlowLayoutPanel
        {
            Dock=DockStyle.Top,AutoSize=true,WrapContents=false,
            FlowDirection=FlowDirection.TopDown
        };
        foreach(var (id,label) in VirtualKeyMapping.Entries)
        {
            var row=new FlowLayoutPanel {Width=618,Height=48,WrapContents=false,Margin=new Padding(0,1,0,2)};
            var caption = new Label
            {
                Text=label,Width=346,Height=38,ForeColor=Theme.White,
                Font=Theme.Font(10),TextAlign=ContentAlignment.MiddleLeft
            };
            var button=Theme.Button("",false);
            button.Width=222;button.Height=38;button.TabStop=true;
            button.Click+=(_,_)=>
            {
                capturing=id;
                capturingButton=button;
                RefreshLabels();
                feedback.Text="Pressione a nova tecla. ESC cancela; F8 é reservado.";
                button.Focus();
            };
            row.Controls.Add(caption);
            row.Controls.Add(button);
            buttons[id]=button;
            list.Controls.Add(row);
        }
        scroll.Controls.Add(list);
        Controls.Add(scroll);
        scroll.BringToFront();
        RefreshLabels();
        Shown += (_,_)=>{WindowsTheme.SetDarkTitle(this);WindowsTheme.ApplyNativeDarkTheme(this);};
    }

    private static string KeyDisplay(int key) => key switch
    {
        0x01=>"Mouse esquerdo", 0x02=>"Mouse direito",
        0x08=>"Backspace", 0x09=>"Tab", 0x0D=>"Enter",
        0x10=>"Shift", 0x11=>"Ctrl",
        0x25=>"Seta esquerda", 0x26=>"Seta cima",
        0x27=>"Seta direita", 0x28=>"Seta baixo",
        _=>Enum.IsDefined(typeof(Keys),key) ? ((Keys)key).ToString() :
           "Tecla "+key.ToString("X2")
    };
    private void RefreshLabels()
    {
        foreach(var (id,button) in buttons)
        {
            button.Text=id==capturing?"Pressione uma tecla...":KeyDisplay(mapping.Get(id));
            button.Invalidate();
        }
    }
    private bool Capture(Keys keyData)
    {
        if(capturing is null)return false;
        int key=(int)(keyData&Keys.KeyCode);
        if(key==(int)Keys.Escape)
        {
            capturing=null;capturingButton=null;
            feedback.Text="Alteração cancelada.";
            RefreshLabels();return true;
        }
        if(!VirtualKeyMapping.IsAssignable(key))
        {
            feedback.Text="Tecla reservada ou não suportada. Escolha outra.";
            return true;
        }
        mapping=mapping.WithKey(capturing,key);
        apply(mapping);
        capturing=null;capturingButton=null;
        feedback.Text="Tecla definida e salva. Pressione F8 no aplicativo para ativar o controle virtual.";
        RefreshLabels();
        return true;
    }
    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if(capturing is not null && Capture(keyData))return true;
        return base.ProcessCmdKey(ref msg,keyData);
    }
    protected override void OnKeyDown(KeyEventArgs e)
    {
        if(capturing is not null && Capture(e.KeyData))
        {
            e.Handled=true;e.SuppressKeyPress=true;return;
        }
        base.OnKeyDown(e);
    }
    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        capturing=null;capturingButton=null;
        base.OnFormClosing(e);
    }
}

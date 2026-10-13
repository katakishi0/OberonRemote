using System.Drawing.Drawing2D;

namespace Oberon.Extended;

internal static class Theme
{
    internal static Color Background = Color.FromArgb(11, 18, 16);
    internal static Color Surface = Color.FromArgb(19, 30, 25);
    internal static Color Border = Color.FromArgb(40, 61, 50);
    internal static Color Green = Color.FromArgb(127, 239, 152);
    internal static Color Muted = Color.FromArgb(151, 172, 158);
    internal static Color White = Color.FromArgb(234, 246, 238);
    internal static bool Animations { get; set; } = true;
    internal static bool IsLight { get; private set; }
    internal static void Switch(string variant, string customHex, Control root)
    {
        IsLight = variant == "Xbox Light";
        var oldBg = Background; var oldSurface = Surface; var oldBorder = Border;
        var oldAccent = Green; var oldText = White; var oldMuted = Muted;
        switch (variant)
        {
            case "Xbox Light":
                Background = Color.FromArgb(236, 244, 238); Surface = Color.White;
                Border = Color.FromArgb(180, 200, 187); Green = Color.FromArgb(20, 126, 65);
                break;
            case "Forest":
                Background = Color.FromArgb(9, 21, 17); Surface = Color.FromArgb(17, 37, 27);
                Border = Color.FromArgb(39, 73, 51); Green = Color.FromArgb(60, 194, 119); break;
            case "Midnight":
                Background = Color.FromArgb(11, 15, 26); Surface = Color.FromArgb(21, 31, 49);
                Border = Color.FromArgb(46, 63, 88); Green = Color.FromArgb(111, 174, 255); break;
            case "Violet":
                Background = Color.FromArgb(20, 14, 29); Surface = Color.FromArgb(34, 25, 47);
                Border = Color.FromArgb(72, 55, 91); Green = Color.FromArgb(205, 149, 255); break;
            case "Graphite":
                Background = Color.FromArgb(15, 17, 20); Surface = Color.FromArgb(29, 32, 36);
                Border = Color.FromArgb(63, 68, 70); Green = Color.FromArgb(204, 222, 211); break;
            case "Custom":
                Background = Color.FromArgb(12, 18, 22); Surface = Color.FromArgb(25, 34, 39);
                Border = Color.FromArgb(55, 71, 75);
                try { Green = ColorTranslator.FromHtml(customHex); } catch { Green = Color.FromArgb(127, 239, 152); }
                break;
            default:
                Background = Color.FromArgb(11, 18, 16); Surface = Color.FromArgb(19, 30, 25);
                Border = Color.FromArgb(40, 61, 50); Green = Color.FromArgb(127, 239, 152); break;
        }
        Muted = IsLight ? Color.FromArgb(97, 117, 103) : Color.FromArgb(151, 172, 158);
        White = IsLight ? Color.FromArgb(30, 48, 37) : Color.FromArgb(234, 246, 238);
        if (variant == "Midnight" || variant == "Violet") Muted = Color.FromArgb(173, 178, 195);
        if (variant == "Graphite") Muted = Color.FromArgb(165, 176, 171);
        void Paint(Control ctl)
        {
            Color Map(Color value)
            {
                if (value.ToArgb() == oldBg.ToArgb()) return Background;
                if (value.ToArgb() == oldSurface.ToArgb()) return Surface;
                if (value.ToArgb() == oldBorder.ToArgb()) return Border;
                if (value.ToArgb() == oldAccent.ToArgb()) return Green;
                if (value.ToArgb() == oldText.ToArgb()) return White;
                if (value.ToArgb() == oldMuted.ToArgb()) return Muted;
                return value;
            }
            ctl.BackColor = Map(ctl.BackColor);
            ctl.ForeColor = Map(ctl.ForeColor);
            if (ctl is Button b) b.FlatAppearance.BorderColor = Border;
            if (ctl is TextBox tb) tb.BackColor = Surface;
            if (ctl is DarkComboBox combo) combo.Invalidate();
            foreach (Control child in ctl.Controls) Paint(child);
            ctl.Invalidate();
        }
        Paint(root);
    }
    internal static Color Mix(Color a, Color b, float amount)
    {
        amount = Math.Clamp(amount, 0f, 1f);
        return Color.FromArgb((int)(a.R + (b.R - a.R) * amount),
            (int)(a.G + (b.G - a.G) * amount), (int)(a.B + (b.B - a.B) * amount));
    }
    private static readonly string UiFont = FontFamily.Families.Any(f => f.Name == "Segoe UI Variable") ? "Segoe UI Variable" : "Segoe UI";
    internal static Font Font(float size, bool bold = false) => new(UiFont, size, bold ? FontStyle.Bold : FontStyle.Regular);
    internal static Label Label(string text, float size = 11, bool bold = false, Color? color = null) => new()
    { Text = text, Font = Font(size, bold), ForeColor = color ?? White, AutoSize = true, Margin = new Padding(0, 0, 0, 8) };
    internal static Button Button(string text, bool primary = false)
    {
        var b = new AnimatedButton { Text = text, AutoSize = false, Height = 42, Width = 164, FlatStyle = FlatStyle.Flat,
            BackColor = primary ? Green : Surface, ForeColor = primary ? Background : White,
            Font = Font(10, true), Cursor = Cursors.Hand, Margin = new Padding(0, 0, 12, 8), UseVisualStyleBackColor = false };
        b.FlatAppearance.BorderSize = 0;
        b.FlatAppearance.MouseOverBackColor = Color.Transparent;
        b.FlatAppearance.MouseDownBackColor = Color.Transparent;
        b.TabStop = false;
        return b;
    }
    internal static void Round(Graphics g, RectangleF rect, float radius, Color fill, Color? border = null)
    {
        using var p = new GraphicsPath();
        float d = radius * 2;
        p.AddArc(rect.X, rect.Y, d, d, 180, 90); p.AddArc(rect.Right-d, rect.Y,d,d,270,90);
        p.AddArc(rect.Right-d,rect.Bottom-d,d,d,0,90); p.AddArc(rect.X,rect.Bottom-d,d,d,90,90); p.CloseFigure();
        using var brush = new SolidBrush(fill); g.FillPath(brush,p);
        if (border is Color c) { using var pen = new Pen(c); g.DrawPath(pen,p); }
    }
}

internal sealed class AnimatedButton : Button
{
    private readonly System.Windows.Forms.Timer transition = new() { Interval = 16 };
    private float hover;
    private bool over;
    private Color displayedColor = Color.Empty;
    public AnimatedButton()
    {
        DoubleBuffered = true;
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        FlatAppearance.BorderSize = 0;
        transition.Tick += (_, _) =>
        {
            float target = over ? 1 : 0;
            hover += (target - hover) * .28f;
            if (Math.Abs(hover - target) < .015f) hover = target;
            if (displayedColor.IsEmpty) displayedColor = BackColor;
            displayedColor = Theme.Animations ? Theme.Mix(displayedColor, BackColor, .24f) : BackColor;
            bool settled = Math.Abs(displayedColor.R - BackColor.R) +
                Math.Abs(displayedColor.G - BackColor.G) +
                Math.Abs(displayedColor.B - BackColor.B) < 4;
            if (settled) displayedColor = BackColor;
            if (hover == target && settled) transition.Stop();
            Invalidate();
        };
    }
    protected override void OnBackColorChanged(EventArgs e)
    {
        base.OnBackColorChanged(e);
        if (displayedColor.IsEmpty || !Theme.Animations) displayedColor = BackColor;
        else transition.Start();
        Invalidate();
    }
    protected override void OnMouseEnter(EventArgs e)
    {
        base.OnMouseEnter(e); over = true;
        if (Theme.Animations) transition.Start(); else { hover = 1; Invalidate(); }
    }
    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e); over = false;
        if (Theme.Animations) transition.Start(); else { hover = 0; Invalidate(); }
    }
    protected override void OnPaintBackground(PaintEventArgs e)
    {
        using var brush = new SolidBrush(Parent?.BackColor ?? Theme.Background);
        e.Graphics.FillRectangle(brush, ClientRectangle);
    }
    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias;
        var fill = Theme.Mix(displayedColor.IsEmpty ? BackColor : displayedColor, Theme.Green, hover * .17f);
        // Sharp rectangular buttons, with a single clean border.
        using var background = new SolidBrush(fill);
        g.FillRectangle(background, 0, 0, Width, Height);
        using var border = new Pen(Theme.Mix(Theme.Border, fill, .76f));
        g.DrawRectangle(border, 0, 0, Math.Max(0, Width - 1), Math.Max(0, Height - 1));
        var flags = TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding;
        flags |= TextAlign == ContentAlignment.MiddleLeft ? TextFormatFlags.Left : TextFormatFlags.HorizontalCenter;
        TextRenderer.DrawText(g, Text, Font, new Rectangle(Padding.Left + 5, 0, Width - Padding.Horizontal - 10, Height), ForeColor, flags);
    }
    protected override void Dispose(bool disposing)
    {
        if (disposing) transition.Dispose();
        base.Dispose(disposing);
    }
}

internal sealed class StatCard : Control
{
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public string Caption { get; set; } = "";
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public string Value { get; set; } = "—";
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public string Detail { get; set; } = "";
    public StatCard() { DoubleBuffered = true; Dock = DockStyle.Fill; Margin = new Padding(0,0,12,0); }
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e); var g=e.Graphics; g.SmoothingMode=SmoothingMode.AntiAlias;
        Theme.Round(g,new RectangleF(0,0,Width-1,Height-1),12,Theme.Surface,Theme.Border);
        using var small=Theme.Font(9); using var big=Theme.Font(21,true);
        TextRenderer.DrawText(g,Caption,small,new Point(18,13),Theme.Muted);
        TextRenderer.DrawText(g,Value,big,new Point(15,32),Theme.Green);
        TextRenderer.DrawText(g,Detail,small,new Point(18,73),Theme.Muted);
    }
}

internal sealed class PadCard : Control
{
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public int Slot { get; set; }
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public PadView State { get; set; } = new("Aguardando controle", false, default);
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public bool ShowBattery { get; set; }
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public bool ShowFamily { get; set; }
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public string Alias { get; set; } = "";
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public Action? ChangeBackend { get; set; }
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public Action? ExplainBackend { get; set; }
    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        if (!State.Connected || State.Family != "Xbox") return;
        if (e.X >= Width-197 && e.X < Width-13 && e.Y >= 6 && e.Y <= 37)
            ChangeBackend?.Invoke();
    }
    public PadCard() { DoubleBuffered=true; Dock=DockStyle.Fill; Margin=new Padding(0,0,12,12); }
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e); var g=e.Graphics; g.SmoothingMode=SmoothingMode.AntiAlias;
        Theme.Round(g,new RectangleF(0,0,Width-1,Height-1),12,Theme.Surface,Theme.Border);
        using var title=Theme.Font(11,true); using var small=Theme.Font(9); using var tiny=Theme.Font(8,true);
        TextRenderer.DrawText(g,$"CONTROLE 0{Slot+1}",small,new Point(20,13),Theme.Muted);
        if (State.Connected && ShowFamily)
            TextRenderer.DrawText(g,State.Family+" · "+State.Source,small,
                new Rectangle(145,13,Math.Max(60,Width-356),24),Theme.Green,TextFormatFlags.EndEllipsis);
        if (State.Connected && State.Family == "Xbox")
        {
            bool suggested = State.Source == "SDL" &&
                (State.Name.Contains("Wireless", StringComparison.OrdinalIgnoreCase) ||
                 State.Name.Contains("Xbox", StringComparison.OrdinalIgnoreCase));
            string action = suggested ? "XInput recomendado" :
                State.Source == "XInput" ? "Usar SDL" : "Alternar captura";
            using var fg = new SolidBrush(Theme.Mix(Theme.Background, Theme.Green, .12f));
            g.FillRectangle(fg, Width-197, 7, 184, 29);
            TextRenderer.DrawText(g, action, tiny, new Rectangle(Width-195, 7, 181, 29),
                Theme.Green, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter |
                TextFormatFlags.EndEllipsis);
        }
        if (State.Connected && ShowBattery)
            TextRenderer.DrawText(g,"Bateria: "+State.Power,small,
                new Rectangle(20,76,Width-40,20),Theme.Muted,TextFormatFlags.EndEllipsis);
        TextRenderer.DrawText(g,string.IsNullOrWhiteSpace(Alias)?State.Name:Alias,title,
            new Rectangle(20,43,Width-40,29),
            State.Connected?Theme.White:Theme.Muted,TextFormatFlags.EndEllipsis);
        var input=State.Input;
        if (!State.Connected) { TextRenderer.DrawText(g,"Conecte via USB ou Bluetooth",small,new Point(20,89),Theme.Muted); return; }
        DrawStick(g,25,104,input.LX,input.LY); DrawStick(g,87,104,input.RX,input.RY);
        string[] labels = State.Family switch
        {
            "PlayStation" => ["X", "O", "Sq", "Tri", "L1", "R1"],
            "Nintendo Switch" => ["B", "A", "Y", "X", "L", "R"],
            _ => ["A", "B", "X", "Y", "LB", "RB"]
        };
        int[] indices=[0,1,2,3,9,10];
        float gap=Math.Min(37,(Width-165)/6f);
        for(int b=0;b<6;b++)
        {
            bool pressed=(input.Buttons&(1u<<indices[b]))!=0;
            float x=167+b*gap;
            Theme.Round(g,new RectangleF(x,103,29,28),1,pressed?Theme.Green:Theme.Background);
            TextRenderer.DrawText(g,labels[b],tiny,new Rectangle((int)x,103,29,28),pressed?Theme.Background:Theme.Muted,TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter);
        }
        DrawTrigger(g,20,154,"LT",input.LT,Width/2-40); DrawTrigger(g,Width/2+10,154,"RT",input.RT,Width/2-45);
    }
    private static void DrawStick(Graphics g,int x,int y,short ax,short ay)
    {
        using var pen=new Pen(Theme.Border); g.DrawEllipse(pen,x,y,40,40);
        g.DrawLine(pen,x+20,y,x+20,y+40);g.DrawLine(pen,x,y+20,x+40,y+20);
        using var brush=new SolidBrush(Theme.Green);
        g.FillEllipse(brush,x+16+ax/32767f*15,y+16+ay/32767f*15,8,8);
    }
    private static void DrawTrigger(Graphics g,int x,int y,string label,short v,int width)
    {
        using var f=Theme.Font(8); TextRenderer.DrawText(g,label,f,new Point(x,y-1),Theme.Muted);
        using var bg=new SolidBrush(Theme.Background); using var fg=new SolidBrush(Theme.Green);
        g.FillRectangle(bg,x+26,y+4,width-26,5); g.FillRectangle(fg,x+26,y+4,(width-26)*Math.Max(0,v/32767f),5);
    }
}

using System.Drawing.Drawing2D;

namespace Oberon.Extended;

internal sealed class DarkSlider : Control
{
    private int minimum, maximum = 100, value = 50;
    public event EventHandler? ValueChanged;
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public int Minimum
    {
        get => minimum;
        set { minimum = value; if (maximum < minimum) maximum = minimum; Value = Math.Clamp(this.value, minimum, maximum); Invalidate(); }
    }
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public int Maximum
    {
        get => maximum;
        set { maximum = Math.Max(value, minimum); Value = Math.Clamp(this.value, minimum, maximum); Invalidate(); }
    }
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public int Value
    {
        get => value;
        set
        {
            int next = Math.Clamp(value, Minimum, Maximum);
            if (this.value == next) return;
            this.value = next; Invalidate(); ValueChanged?.Invoke(this, EventArgs.Empty);
        }
    }
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public int TickFrequency { get; set; } = 10;
    public DarkSlider()
    {
        Height = 40; Width = 470;
        DoubleBuffered = true;
        SetStyle(ControlStyles.Selectable, true);
        TabStop = true;
        BackColor = Theme.Background;
    }
    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e); Focus(); UpdateFromX(e.X);
    }
    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (e.Button == MouseButtons.Left) UpdateFromX(e.X);
    }
    private void UpdateFromX(int x)
    {
        double part = Math.Clamp((x - 17d) / Math.Max(1, Width - 34), 0d, 1d);
        Value = Minimum + (int)Math.Round((Maximum - Minimum) * part);
    }
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.KeyCode == Keys.Left || e.KeyCode == Keys.Down) Value--;
        else if (e.KeyCode == Keys.Right || e.KeyCode == Keys.Up) Value++;
        else if (e.KeyCode == Keys.Home) Value = Minimum;
        else if (e.KeyCode == Keys.End) Value = Maximum;
        else return;
        e.Handled = true;
    }
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(BackColor);
        int y = Height / 2;
        double part = Maximum == Minimum ? 0 : (Value - Minimum) / (double)(Maximum - Minimum);
        int end = 17 + (int)Math.Round(part * Math.Max(1, Width - 34));
        using var basePen = new Pen(Theme.Border, 6) { StartCap = LineCap.Round, EndCap = LineCap.Round };
        using var activePen = new Pen(Theme.Green, 6) { StartCap = LineCap.Round, EndCap = LineCap.Round };
        g.DrawLine(basePen, 17, y, Width - 17, y);
        g.DrawLine(activePen, 17, y, end, y);
        using var dot = new SolidBrush(Theme.Green);
        g.FillEllipse(dot, end - 9, y - 9, 18, 18);
        using var hole = new SolidBrush(Theme.Surface);
        g.FillEllipse(hole, end - 3, y - 3, 6, 6);
    }
}

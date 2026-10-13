using System.Drawing.Drawing2D;

namespace Oberon.Extended;

internal sealed class DarkCheckBox : CheckBox
{
    public DarkCheckBox()
    {
        AutoSize = false;
        Width = 710;
        Height = 32;
        ForeColor = Theme.White;
        BackColor = Theme.Background;
        Font = Theme.Font(11);
        SetStyle(ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.AllPaintingInWmPaint | ControlStyles.ResizeRedraw, true);
    }
    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(Parent?.BackColor ?? Theme.Background);
        var square = new RectangleF(2, 6, 19, 19);
        using var fill = new SolidBrush(Checked ? Theme.Green : Theme.Surface);
        using var border = new Pen(Checked ? Theme.Green : Theme.Border, 1.3f);
        g.FillRectangle(fill, square);
        g.DrawRectangle(border, 2, 6, 19, 19);
        if (Checked)
        {
            using var check = new Pen(Theme.Background, 2.3f)
                { StartCap=LineCap.Round, EndCap=LineCap.Round };
            g.DrawLines(check, new[] {new Point(7,15),new Point(11,19),new Point(18,10)});
        }
        TextRenderer.DrawText(g,Text,Font,new Rectangle(33,1,Math.Max(10,Width-36),Height-2),
            Theme.White,TextFormatFlags.VerticalCenter|TextFormatFlags.NoPadding|TextFormatFlags.EndEllipsis);
    }
}

using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;

namespace Oberon.Extended;

// WinForms native combo edit surfaces are white by default. Paint the selection
// with the app palette and owner-draw every item in the drop-down.
internal sealed class DarkComboBox : ComboBox
{
    public DarkComboBox()
    {
        DropDownStyle = ComboBoxStyle.DropDownList;
        DrawMode = DrawMode.OwnerDrawFixed;
        ItemHeight = 31;
        FlatStyle = FlatStyle.Flat;
        BackColor = Theme.Surface;
        ForeColor = Theme.White;
        Font = Theme.Font(11);
        SetStyle(ControlStyles.OptimizedDoubleBuffer, true);
    }
    protected override void OnDrawItem(DrawItemEventArgs e)
    {
        if (e.Index < 0) return;
        bool selected = (e.State & DrawItemState.Selected) != 0;
        using var bg = new SolidBrush(selected ? Theme.Mix(Theme.Surface, Theme.Green, .20f) : Theme.Surface);
        e.Graphics.FillRectangle(bg, e.Bounds);
        TextRenderer.DrawText(e.Graphics, Items[e.Index]?.ToString() ?? "", Font,
            new Rectangle(e.Bounds.X + 12, e.Bounds.Y, e.Bounds.Width - 24, e.Bounds.Height),
            Theme.White, TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
        if ((e.State & DrawItemState.Focus) != 0) { /* selection highlight replaces native dotted focus border */ }
    }
    protected override void WndProc(ref Message m)
    {
        const int WM_PAINT = 0x000F;
        base.WndProc(ref m);
        if (m.Msg != WM_PAINT || !IsHandleCreated || DroppedDown) return;
        using var g = Graphics.FromHwnd(Handle);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        using var bg = new SolidBrush(Theme.Surface);
        g.FillRectangle(bg, ClientRectangle);
        using var border = new Pen(Theme.Border);
        g.DrawRectangle(border, 0, 0, Width - 1, Height - 1);
        string caption = SelectedItem?.ToString() ?? "";
        TextRenderer.DrawText(g, caption, Font, new Rectangle(12, 0, Width - 42, Height),
            Theme.White, TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
        using var arrow = new Pen(Theme.Muted, 1.5f);
        int x = Width - 20, y = Height / 2;
        g.DrawLine(arrow, x - 4, y - 2, x, y + 2);
        g.DrawLine(arrow, x, y + 2, x + 4, y - 2);
    }
}

internal static class WindowsTheme
{
    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);
    [DllImport("uxtheme.dll", CharSet=CharSet.Unicode)]
    private static extern int SetWindowTheme(IntPtr hwnd, string? appName, string? idList);
    internal static void ApplyNativeDarkTheme(Control root)
    {
        if (!OperatingSystem.IsWindows()) return;
        try
        {
            void Visit(Control control)
            {
                if (control.IsHandleCreated &&
                    (control is ScrollableControl || control is TextBox || control is ComboBox))
                    SetWindowTheme(control.Handle, "DarkMode_Explorer", null);
                foreach (Control child in control.Controls) Visit(child);
            }
            Visit(root);
        }
        catch (DllNotFoundException) { }
        catch (EntryPointNotFoundException) { }
    }
    internal static void SetDarkTitle(Form form)
    {
        if (!OperatingSystem.IsWindows()) return;
        try
        {
            int enabled = 1;
            // 20: Windows 10 2004 / Windows 11, 19: older Windows 10 builds.
            if (DwmSetWindowAttribute(form.Handle, 20, ref enabled, 4) != 0)
                DwmSetWindowAttribute(form.Handle, 19, ref enabled, 4);
        }
        catch (DllNotFoundException) { }
        catch (EntryPointNotFoundException) { }
    }
}

internal sealed class InformationDialog : Form
{
    internal InformationDialog(string title, string message)
    {
        Text = title;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MinimizeBox = false; MaximizeBox = false;
        StartPosition = FormStartPosition.CenterParent;
        ShowInTaskbar = false;
        ClientSize = new Size(540, 370);
        BackColor = Theme.Background;
        ForeColor = Theme.White;
        Font = Theme.Font(11);
        var heading = Theme.Label(title, 17, true, Theme.Green);
        heading.Location = new Point(26, 25);
        Controls.Add(heading);
        var body = new Label { Text = message, ForeColor = Theme.White, Font = Theme.Font(11),
            Bounds = new Rectangle(26, 79, 490, 218), AutoSize = false };
        Controls.Add(body);
        var close = Theme.Button("Entendi", true);
        close.Location = new Point(365, 310);
        close.Width = 150;
        close.DialogResult = DialogResult.OK;
        Controls.Add(close);
        AcceptButton = close;
        Shown += (_, _) => WindowsTheme.SetDarkTitle(this);
    }
    internal static void Explain(IWin32Window owner, string title, string message)
    {
        using var dialog = new InformationDialog(title, message);
        dialog.ShowDialog(owner);
    }
}

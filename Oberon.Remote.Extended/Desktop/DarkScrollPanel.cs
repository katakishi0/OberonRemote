using System.Runtime.InteropServices;
using System.Drawing.Drawing2D;

namespace Oberon.Extended;

// A scrollable WinForms panel with an app-colored scrollbar. Unlike the native
// white scrollbar, its small thumb is fully drawn from the active theme.
// Uses WinForms AutoScroll for keyboard, touchpad and wheel support.
internal sealed class DarkScrollPanel : Panel
{
    private bool hidingNative;
    private bool dragging;
    private int dragOrigin;
    private int dragInitial;
    private const int TrackWidth=14;

    [DllImport("user32.dll", ExactSpelling=true)]
    private static extern bool ShowScrollBar(IntPtr hwnd,int bar,bool show);

    internal DarkScrollPanel()
    {
        AutoScroll=true;
        DoubleBuffered=true;
        Padding=new Padding(0,0,TrackWidth+5,0);
        SetStyle(ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer |
                 ControlStyles.AllPaintingInWmPaint, true);
    }
    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        HideNative();
    }
    protected override void OnLayout(LayoutEventArgs e)
    {
        base.OnLayout(e);
        HideNative();
        InvalidateScrollTrack();
    }
    protected override void OnScroll(ScrollEventArgs se)
    {
        base.OnScroll(se);
        HideNative();
        InvalidateScrollTrack();
    }
    protected override void OnMouseWheel(MouseEventArgs e)
    {
        base.OnMouseWheel(e);
        HideNative();
        InvalidateScrollTrack();
    }
    private void HideNative()
    {
        if(hidingNative || !IsHandleCreated || !OperatingSystem.IsWindows()) return;
        try
        {
            hidingNative=true;
            ShowScrollBar(Handle,1,false); // SB_VERT
            ShowScrollBar(Handle,0,false); // SB_HORZ
        }
        finally { hidingNative=false; }
    }
    private int MaxPosition => Math.Max(0,DisplayRectangle.Height-ClientSize.Height);
    private int Offset => Math.Clamp(-AutoScrollPosition.Y,0,MaxPosition);
    private Rectangle Track => new(Math.Max(0,ClientSize.Width-TrackWidth-2),3,
        TrackWidth,Math.Max(0,ClientSize.Height-6));
    private Rectangle Thumb
    {
        get
        {
            var t=Track;
            if(t.Height<30)return Rectangle.Empty;
            int max=MaxPosition;
            int h=max<=0?t.Height:Math.Clamp(
                (int)((double)t.Height * ClientSize.Height / Math.Max(1,DisplayRectangle.Height)),
                28,t.Height);
            int y=t.Y+(max<=0?0:(int)((double)(t.Height-h)*Offset/max));
            return new Rectangle(t.X+4,y,6,h);
        }
    }
    private void InvalidateScrollTrack()
    {
        if(IsHandleCreated) Invalidate(Track);
    }
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        if(MaxPosition<=0) return;
        var track=Track;
        e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;
        using var bg=new SolidBrush(Theme.Mix(Theme.Background,Theme.Surface,.6f));
        using var fg=new SolidBrush(Theme.Mix(Theme.Green,Theme.Muted,.30f));
        e.Graphics.FillRectangle(bg,track);
        if(!Thumb.IsEmpty)e.Graphics.FillRectangle(fg,Thumb);
    }
    protected override void OnMouseDown(MouseEventArgs e)
    {
        if(MaxPosition>0 && Track.Contains(e.Location))
        {
            dragging=true;
            Capture=true;
            dragOrigin=e.Y;dragInitial=Offset;
            if(!Thumb.Contains(e.Location))
            {
                ScrollToOffset((int)((double)(e.Y-Track.Top)*MaxPosition/Math.Max(1,Track.Height)));
                dragInitial=Offset;
            }
            return;
        }
        base.OnMouseDown(e);
    }
    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if(!dragging)return;
        int travel=Math.Max(1,Track.Height-Thumb.Height);
        ScrollToOffset(dragInitial + (int)((double)(e.Y-dragOrigin)*MaxPosition/travel));
    }
    protected override void OnMouseUp(MouseEventArgs e)
    {
        dragging=false;
        Capture=false;
        base.OnMouseUp(e);
    }
    private void ScrollToOffset(int position)
    {
        AutoScrollPosition=new Point(0,Math.Clamp(position,0,MaxPosition));
        HideNative();
        InvalidateScrollTrack();
    }
}

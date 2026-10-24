using System.Runtime.InteropServices;

namespace RotaGame.App;

internal sealed partial class OptimizerForm
{
    [DllImport("user32.dll")] private static extern bool ReleaseCapture();
    [DllImport("user32.dll")] private static extern IntPtr SendMessage(IntPtr handle, int message, IntPtr wparam, IntPtr lparam);
    private void DragWindow(object? sender, MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Left) return;
        ReleaseCapture(); SendMessage(Handle, 0xA1, (IntPtr)2, IntPtr.Zero);
    }
    private void ToggleMaximize()
    {
        MaximizedBounds = Screen.FromControl(this).WorkingArea;
        WindowState = WindowState == FormWindowState.Maximized ? FormWindowState.Normal : FormWindowState.Maximized;
    }
    protected override void OnSizeChanged(EventArgs e) { base.OnSizeChanged(e); Invalidate(true); }
    protected override void OnResizeEnd(EventArgs e) { base.OnResizeEnd(e); Invalidate(true); }
    protected override void OnDpiChanged(DpiChangedEventArgs e) { base.OnDpiChanged(e); Arrange(); Invalidate(true); }
    protected override void WndProc(ref Message message)
    {
        if (message.Msg == 0x84 && WindowState == FormWindowState.Normal)
        {
            var p = PointToClient(new Point(unchecked((short)(long)message.LParam), unchecked((short)((long)message.LParam >> 16))));
            var edge = (int)Math.Ceiling(6 * DeviceDpi / 96f);
            var left = p.X < edge; var right = p.X >= ClientSize.Width-edge; var top = p.Y < edge; var bottom = p.Y >= ClientSize.Height-edge;
            if (left || right || top || bottom) { message.Result = (IntPtr)(top && left ? 13 : top && right ? 14 : bottom && left ? 16 : bottom && right ? 17 : left ? 10 : right ? 11 : top ? 12 : 15); return; }
        }
        base.WndProc(ref message);
    }
}

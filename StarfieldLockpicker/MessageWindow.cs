using PInvoke;
using StarfieldLockpicker;

public class MessageWindow : Form
{
    public event Action? OnHoyKeyPressed;

    public MessageWindow()
    {
        Utility.RegisterHotKey(Handle, 0, 0, (int)User32.VirtualKey.VK_F10);
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == (int)User32.WindowMessage.WM_HOTKEY)
        {
            OnHoyKeyPressed?.Invoke();
        }
        base.WndProc(ref m);
    }

    protected override void SetVisibleCore(bool value)
    {
        // Ensure the window never becomes visible
        base.SetVisibleCore(false);
    }

    protected override void OnClosed(EventArgs e)
    {
        Utility.UnregisterHotKey(Handle, 0);
    }
}
using PInvoke;

class MessageWindow : Form
{
    public event Action? OnHoyKeyPressed;
    
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
}
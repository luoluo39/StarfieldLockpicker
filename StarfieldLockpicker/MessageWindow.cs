using Windows.Win32;
using Windows.Win32.Foundation;
using StarfieldLockpicker;
using StarfieldLockpicker.Inputs;

public class MessageWindow : Form
{
    public event Action? OnHoyKeyPressed;

    public MessageWindow(VKCode hotKey)
    {
        if (!PInvoke.RegisterHotKey((HWND)Handle, 0, 0, (uint)hotKey))
        {
            PInvoke.UnregisterHotKey(HWND.Null, 0);
            Console.WriteLine($"The hotkey {hotKey} is in use! this may caused by other programs with same key, or running more than one instance of this program. try close all instances, wait for several seconds, and start again, or change hotkey in config");
        }
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == (int)PInvoke.WM_HOTKEY)
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
        PInvoke.UnregisterHotKey((HWND)Handle, 0);
    }
}
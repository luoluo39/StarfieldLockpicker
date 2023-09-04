using PInvoke;
using StarfieldLockpicker;
using StarfieldLockpicker.Inputs;

public class MessageWindow : Form
{
    public event Action? OnHoyKeyPressed;

    public MessageWindow()
    {
        if (!Enum.TryParse<VKCode>(AppConfig.Instance.HotKey, true, out var vk))
        {
            Console.WriteLine($"The key {AppConfig.Instance.HotKey} can not be parsed");
        }
        if (!Utility.RegisterHotKey(Handle, 0, 0, (int)vk))
        {
            Utility.UnregisterHotKey(IntPtr.Zero, 0);
            Console.WriteLine($"The hotkey {vk} is in use! this may caused by other programs with same key, or running more than one instance of this program. try close all instances, wait for several seconds, and start again, or change hotkey in config");
        }
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
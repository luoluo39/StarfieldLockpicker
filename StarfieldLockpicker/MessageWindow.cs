using System.Runtime.InteropServices;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.Input.KeyboardAndMouse;
using Windows.Win32.UI.WindowsAndMessaging;
using StarfieldLockpicker;
using StarfieldLockpicker.Inputs;

using static Windows.Win32.PInvoke;
using System.Reflection.Metadata;

public class MessageWindow
{
    public event Action? OnHoyKeyPressed;

    private readonly HWND _handle;

    public MessageWindow(VKCode hotKey, uint modifiers)
    {
        unsafe
        {
            const string className = "MessageWindowClass";
            fixed (char* pch = className)
            {
                WNDCLASSW wc = new WNDCLASSW();
                wc.lpfnWndProc = WindowProc;
                wc.hInstance = (HINSTANCE)GetModuleHandle(default(string)).DangerousGetHandle();
                wc.lpszClassName = new PCWSTR(pch);

                RegisterClass(wc);
                _handle = CreateWindowEx(default, "MessageWindowClass", "Message Window", default, 0, 0, 0, 0, HWND.Null, default, default, default);
            }
        }

        if (!RegisterHotKey(_handle, 0, (HOT_KEY_MODIFIERS)modifiers, (uint)hotKey))
        {
            UnregisterHotKey(HWND.Null, 0);
            Console.WriteLine($"The hotkey {hotKey} is in use! this may caused by other programs with same key, or running more than one instance of this program. try close all instances, wait for several seconds, and start again, or change hotkey in config");
        }
    }

    public void Run()
    {
        MSG msg;
        while (GetMessage(out msg, HWND.Null, 0, 0))
        {
            TranslateMessage(msg);
            DispatchMessage(msg);
        }

        UnregisterHotKey(_handle, 0);
    }

    private LRESULT WindowProc(HWND hwnd, uint uMsg, WPARAM wParam, LPARAM lParam)
    {
        switch (uMsg)
        {
            case WM_HOTKEY:
                OnHoyKeyPressed?.Invoke();
                break;
            case WM_CLOSE:
                // 关闭窗口时释放资源
                DestroyWindow(hwnd);
                break;
            case WM_DESTROY:
                PostQuitMessage(0);
                break;
            default:
                return DefWindowProc(hwnd, uMsg, wParam, lParam);
        }
        return default;
    }
}
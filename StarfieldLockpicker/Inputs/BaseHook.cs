using static PInvoke.User32;
using static PInvoke.Kernel32;

namespace StarfieldLockpicker.Inputs;

public abstract class BaseHook : IDisposable
{
    private SafeHookHandle? hookHandle;
    private WindowsHookType hookType;
    private WindowsHookDelegate hookDelegate;

    public BaseHook(WindowsHookType hookType)
    {
        this.hookType = hookType;
        hookDelegate = OnHookCall;
    }

    public bool Install()
    {
        var hInstance = LoadLibrary("user32.dll").DangerousGetHandle();
        hookHandle = SetWindowsHookEx(hookType, hookDelegate, hInstance, 0);
        return !hookHandle.IsInvalid;
    }

    public void Close()
    {
        Dispose();
    }

    protected virtual int OnHookCall(int nCode, nint wParam, nint lParam)
    {
        return CallNextHookEx(hookHandle.DangerousGetHandle(), nCode, wParam, lParam);
    }

    public void Dispose()
    {
        hookHandle?.Dispose();
        hookHandle = null;
    }
}
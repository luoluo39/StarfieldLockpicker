using System.Runtime.InteropServices;

using static PInvoke.User32;


namespace StarfieldLockpicker.Inputs;

public class KeyboardHook : BaseHook
{
    public record struct EventArgs(VKCode Key, KeyState State);
    public delegate bool OnKeyboardEventHandler(KeyboardHook sender, EventArgs args);
    public event OnKeyboardEventHandler? OnKeyboardEvent;

    public KeyboardHook() : base(WindowsHookType.WH_KEYBOARD_LL)
    {
    }

    protected override int OnHookCall(int nCode, nint wParam, nint lParam)
    {
        KEYBDINPUT keyboardStruct = (KEYBDINPUT)(Marshal.PtrToStructure(lParam, typeof(KEYBDINPUT)) ?? throw new NullReferenceException());
        var returnCall = OnKeyboardHookCall(nCode, (int)wParam, keyboardStruct);
        if (returnCall != 0) return returnCall;

        return base.OnHookCall(nCode, wParam, lParam);
    }

    protected virtual int OnKeyboardHookCall(int nCode, int wParam, KEYBDINPUT keyboardStruct)
    {
        var consumeEvent = false;
        KeyState keyState;

        var message = (WindowMessage)wParam;
        switch (message)
        {
            case WindowMessage.WM_KEYDOWN:
                keyState = KeyState.KeyDown;
                break;
            case WindowMessage.WM_KEYUP:
                keyState = KeyState.KeyUp;
                break;
            case WindowMessage.WM_SYSKEYDOWN:
                keyState = KeyState.KeyDown;
                break;
            case WindowMessage.WM_SYSKEYUP:
                keyState = KeyState.KeyUp;
                break;
            default:
                keyState = KeyState.Unknown;
                break;
        }

        consumeEvent = OnKeyboardEvent?.Invoke(this, new((VKCode)keyboardStruct.wVk, keyState)) ?? false;
        return consumeEvent ? 1 : 0;
    }
}
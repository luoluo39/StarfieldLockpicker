using System.Runtime.InteropServices;
using Windows.Win32;
using Windows.Win32.UI.Input.KeyboardAndMouse;

using ScanCode = System.UInt16;

namespace StarfieldLockpicker.Inputs;

public static class Input
{
    private static Lazy<ScanCode[]> lazyVKCode2ScanCode = new(LoadVKCode2ScanCode);
    private static ScanCode[] VKCode2ScanCode => lazyVKCode2ScanCode.Value;
    private static volatile bool disabled = false;

    private static ScanCode[] LoadVKCode2ScanCode()
    {
        var keys = (ushort[])Enum.GetValues(typeof(VIRTUAL_KEY));
        var arr = new ScanCode[keys.Max() + 1];
        for (int i = 0; i < keys.Length; i++)
        {
            arr[keys[i]] = (ScanCode)PInvoke.MapVirtualKey(keys[i], MAP_VIRTUAL_KEY_TYPE.MAPVK_VK_TO_VSC);
        }
        return arr;
    }

    private static ScanCode VKCodeToScanCode(VIRTUAL_KEY key)
    {
        return VKCode2ScanCode[(int)key];
    }

    public static void ForceReload()
    {
        lazyVKCode2ScanCode = new Lazy<ScanCode[]>(LoadVKCode2ScanCode);
    }

    public static void Disable()
    {
        disabled = true;
    }

    private static void SendInputs(params INPUT[] inputs)
    {
        if (disabled)
            return;

        PInvoke.SendInput(inputs, Marshal.SizeOf<INPUT>());
    }

    private static void KeyboardInput(VIRTUAL_KEY key, ScanCode scan, KEYBD_EVENT_FLAGS flag = 0, uint time = 0)
    {
        var inp = new INPUT
        {
            type = INPUT_TYPE.INPUT_KEYBOARD,
            Anonymous =
            {
                ki = new KEYBDINPUT
                {
                    wVk = key,
                    wScan = scan,
                    dwFlags = flag,
                    time = time,
                }
            }
        };

        SendInputs(inp);
    }

    public static void KeyboardKeyDown(VKCode key)
    {
        KeyboardInput((VIRTUAL_KEY)key, VKCodeToScanCode((VIRTUAL_KEY)key));
    }

    public static void KeyboardKeyUp(VKCode key)
    {
        KeyboardInput((VIRTUAL_KEY)key, VKCodeToScanCode((VIRTUAL_KEY)key), KEYBD_EVENT_FLAGS.KEYEVENTF_KEYUP);
    }

    public static void KeyboardKeyClick(VKCode key, int interval)
    {
        KeyboardKeyDown(key);
        Thread.Sleep(interval);
        KeyboardKeyUp(key);
    }
}
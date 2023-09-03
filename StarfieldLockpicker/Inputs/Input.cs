using System.Runtime.InteropServices;
using static PInvoke.User32;

namespace StarfieldLockpicker.Inputs;

public static class Input
{
    [DllImport("user32.dll")] private static extern ushort VkKeyScanA(byte ch);

    private static Lazy<ScanCode[]> lazyVKCode2ScanCode = new(LoadVKCode2ScanCode);
    private static ScanCode[] VKCode2ScanCode => lazyVKCode2ScanCode.Value;

    private static ScanCode[] LoadVKCode2ScanCode()
    {
        var keys = (ushort[])Enum.GetValues(typeof(VirtualKey));
        var arr = new ScanCode[keys.Max() + 1];
        for (int i = 0; i < keys.Length; i++)
        {
            arr[keys[i]] = (ScanCode)MapVirtualKey(keys[i], MapVirtualKeyTranslation.MAPVK_VK_TO_VSC);
        }
        return arr;
    }

    private static VirtualKey[] LoadChar2VKCode()
    {
        var arr = new VirtualKey[256];
        for (int i = 0; i < 256; i++)
        {
            var val = VkKeyScanA((byte)i);
            arr[i] = unchecked((short)val) < 0 ? 0 : (VirtualKey)val;
        }
        return arr;
    }

    public static void ForceReload()
    {
        lazyVKCode2ScanCode = new Lazy<ScanCode[]>(LoadVKCode2ScanCode);
    }

    public static ScanCode VKCodeToScanCode(VirtualKey key)
    {
        return VKCode2ScanCode[(int)key];
    }


    private static void SendInputs(params INPUT[] inputs)
    {
        SendInput(inputs.Length, inputs, Marshal.SizeOf<INPUT>());
    }

    private static void KeyboardInput(VirtualKey key, ScanCode scan, KEYEVENTF flag = 0, uint time = 0)
    {
        var inp = new INPUT
        {
            type = InputType.INPUT_KEYBOARD,
            Inputs =
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
        KeyboardInput((VirtualKey)key, VKCodeToScanCode((VirtualKey)key));
    }

    public static void KeyboardKeyUp(VKCode key)
    {
        KeyboardInput((VirtualKey)key, VKCodeToScanCode((VirtualKey)key), KEYEVENTF.KEYEVENTF_KEYUP);
    }

    public static void KeyboardKeyClick(VKCode key, int interval)
    {
        KeyboardKeyDown(key);
        Thread.Sleep(interval);
        KeyboardKeyUp(key);
    }
}
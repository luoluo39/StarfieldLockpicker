using System.Diagnostics.CodeAnalysis;
using System.Drawing;
using System.Numerics;
using System.Text.Json;
using System.Text.Json.Serialization;
using Windows.Win32;
using StarfieldLockpicker.Inputs;
using System.Threading;
using Windows.Win32.UI.Input.KeyboardAndMouse;

namespace StarfieldLockpicker;

[Serializable]
public class AppConfig
{
    public static AppConfig CreateDefaultConfig(Stream utf8Json, bool init)
    {
        var result = new AppConfig();
        result.ParseKeys();
        if (init)
            result.Init();

        JsonSerializer.Serialize(utf8Json, result, new JsonSerializerOptions
        {
            TypeInfoResolver = SourceGenerationContext.Default,
            WriteIndented = true
        });

        return result;
    }

    public static bool TryLoadConfig(string text, bool init, [NotNullWhen(true)] out AppConfig? result)
    {
        try
        {
            var deserialized = JsonSerializer.Deserialize<AppConfig>(text, new JsonSerializerOptions
            {
                TypeInfoResolver = SourceGenerationContext.Default,
                ReadCommentHandling = JsonCommentHandling.Skip
            }) ?? throw new NullReferenceException("null deserialized");

            Console.WriteLine("config loaded");
            result = deserialized;
            result.ParseKeys();
            return !init || result.Init();
        }
        catch (Exception e)
        {
            Console.WriteLine("failed to load config!");
            Console.WriteLine(e);
            result = null;
            return false;
        }
    }

    private static bool ParseKey(string str, out VKCode vkCode)
    {
        if (str.Length == 1)
        {
            var sh = PInvoke.VkKeyScan(str[0]);
            if (sh != -1)
            {
                vkCode = (VKCode)(sh & byte.MaxValue);
                return true;
            }
        }

        if (Enum.TryParse(str, true, out vkCode))
            return true;

        if (Enum.TryParse(str, true, out VIRTUAL_KEY vk) || Enum.TryParse("vk_" + str, true, out vk))
        {
            vkCode = (VKCode)vk;
            return true;
        }
        return false;
    }

    private void ParseKeys()
    {
        VKCode vk;

        var hotKeyStr = HotKey.Split('+').Last();
        if (!ParseKey(hotKeyStr, out vk))
            Utility.ConsoleError($"Key {hotKeyStr} can not be parsed");
        VirtualHotKey = vk;
        HotKeyModifier = (uint)ParseHotKeyModifiers(HotKey);

        if (!ParseKey(KeyPrevious, out vk))
            Utility.ConsoleError($"Key {KeyPrevious} can not be parsed");
        VirtualPrevious = vk;

        if (!ParseKey(KeyNext, out vk))
            Utility.ConsoleError($"Key {KeyNext} can not be parsed");
        VirtualNext = vk;

        if (!ParseKey(KeyRotateAntiClockwise, out vk))
            Utility.ConsoleError($"Key {KeyRotateAntiClockwise} can not be parsed");
        VirtualRotateAntiClockwise = vk;

        if (!ParseKey(KeyRotateClockwise, out vk))
            Utility.ConsoleError($"Key {KeyRotateClockwise} can not be parsed");
        VirtualRotateClockwise = vk;

        if (!ParseKey(KeyInsert, out vk))
            Utility.ConsoleError($"Key {KeyInsert} can not be parsed");
        VirtualInsert = vk;
    }

    private static HOT_KEY_MODIFIERS ParseHotKeyModifiers(string hotKey) =>
        hotKey.Split('+')
            .SkipLast(1)
            .Aggregate<string, HOT_KEY_MODIFIERS>(0, (current, key) => current | key.ToLower() switch
            {
                "ctrl" => HOT_KEY_MODIFIERS.MOD_CONTROL,
                "alt" => HOT_KEY_MODIFIERS.MOD_ALT,
                "shift" => HOT_KEY_MODIFIERS.MOD_SHIFT,
                "win" => HOT_KEY_MODIFIERS.MOD_WIN,
                _ => 0
            });

    private bool Init()
    {
        var hWnd = PInvoke.FindWindow(null, GameWindowTitle);
        if (hWnd.IsNull)
        {
            Console.WriteLine($"Error: cant find a window named '{GameWindowTitle}'");
            return false;
        }

        PInvoke.GetWindowRect(hWnd, out var rect);

        Console.WriteLine($"ClientRect: {rect.X},{rect.Y},{rect.Width},{rect.Height}");

        UpdateResolution(rect);
        return true;
    }

    private void UpdateResolution(Rectangle clientRect)
    {
        ClientRect = clientRect;
        var clientScale = Math.Min(clientRect.Width / 16f, clientRect.Height / 9f) / Math.Min(ReferenceResolutionWidth / 16f, ReferenceResolutionHeight / 9f);

        var ax = clientScale;
        var bx = (ClientRect.Width - ReferenceResolutionWidth * clientScale) / 2 + ClientRect.X;

        var ay = clientScale;
        var by = (ClientRect.Height - ReferenceResolutionHeight * clientScale) / 2 + ClientRect.Y;

        ClientScale = clientScale;
        ClientMatrix = new Matrix3x2(
            ax, 0,
            0, ay,
            bx, by
            );

        var refCenter = new Vector2(ReferenceResolutionWidth, ReferenceResolutionHeight) / 2f;
        var circleR = (CircleRadiusKey + SampleRadiusKey) + 2;

        var rocF = new RectangleF(refCenter.X - circleR, refCenter.Y - circleR, circleR + circleR,
            circleR + circleR);
        var regionOfCircle = Rectangle.Ceiling(rocF);

        var regionOfKeySelection = new Rectangle(KeyAreaX0 - 1, KeyAreaY0 - 1, KeyAreaWidth + 2, KeyAreaHeight + 2);

        var regionOfInterest = Rectangle.Union(regionOfCircle, regionOfKeySelection);

        ClientRegionOfInterest = this.TranslateRectangleCeiling(regionOfInterest);
        ClientRegionOfKeySelection = this.TranslateRectangleCeiling(regionOfKeySelection);
        ClientRegionOfCircle = this.TranslateRectangleCeiling(regionOfCircle);
    }

    public event Action<Size>? OnResolutionChanged;

    public string GameWindowTitle { get; set; } = "Starfield";
    public string HotKey { get; set; } = "F10";
    public string KeyPrevious { get; set; } = "Q";
    public string KeyNext { get; set; } = "T";
    public string KeyRotateAntiClockwise { get; set; } = "A";
    public string KeyRotateClockwise { get; set; } = "D";
    public string KeyInsert { get; set; } = "E";

    public float ResponseWaitTimeout { get; set; } = 500;
    public float IntervalForUIRefresh { get; set; } = 20;
    public float IntervalForCommandExecution { get; set; } = 20;
    public float IntervalForLayerCompleteAnimation { get; set; } = 20;
    public float IntervalForKeyboardClick { get; set; } = 16;
    public float IntervalBetweenKeyboardClick { get; set; } = 20;

    public bool EnablePreciseDelay { get; set; } = false;

    public bool PrintDebug { get; set; } = false;
    public bool PrintInfo { get; set; } = true;
    public bool PrintWarnings { get; set; } = true;
    public bool PrintError { get; set; } = true;

    public bool PrintMaxColor0 { get; set; } = false;
    public bool PrintMaxColor1 { get; set; } = false;
    public bool PrintMaxColor2 { get; set; } = false;
    public bool PrintMaxColor3 { get; set; } = false;
    public bool PrintMaxColorKey { get; set; } = false;

    public float CircleCenterX { get; set; } = 960;
    public float CircleCenterY { get; set; } = 540;

    public float CircleRadius0 { get; set; } = 205;
    public float CircleRadius1 { get; set; } = 168;
    public float CircleRadius2 { get; set; } = 135;
    public float CircleRadius3 { get; set; } = 105;
    public float CircleRadiusKey { get; set; } = 240;

    public float SampleRadius0 { get; set; } = 2f;
    public float SampleRadius1 { get; set; } = 2f;
    public float SampleRadius2 { get; set; } = 2f;
    public float SampleRadius3 { get; set; } = 2f;
    public float SampleRadiusKey { get; set; } = 7;

    public float SampleThr0 { get; set; } = 0.05f;
    public float SampleThr1 { get; set; } = 0.05f;
    public float SampleThr2 { get; set; } = 0.05f;
    public float SampleThr3 { get; set; } = 0.05f;
    public float SampleThrKey { get; set; } = 0.5f;

    public int KeyAreaX0 { get; set; } = 1333;
    public int KeyAreaY0 { get; set; } = 130;
    public int KeyAreaWidth { get; set; } = 494;
    public int KeyAreaHeight { get; set; } = 744;

    public int ReferenceResolutionWidth { get; set; } = 1920;
    public int ReferenceResolutionHeight { get; set; } = 1080;

    public double ImageMseThr { get; set; } = 45;


    [JsonIgnore] public VKCode VirtualHotKey { get; private set; }
    [JsonIgnore] public uint HotKeyModifier { get; private set; }

    [JsonIgnore] public VKCode VirtualPrevious { get; private set; }
    [JsonIgnore] public VKCode VirtualNext { get; private set; }
    [JsonIgnore] public VKCode VirtualRotateAntiClockwise { get; private set; }
    [JsonIgnore] public VKCode VirtualRotateClockwise { get; private set; }
    [JsonIgnore] public VKCode VirtualInsert { get; private set; }


    [JsonIgnore] public Rectangle ClientRect { get; private set; }
    [JsonIgnore] public Matrix3x2 ClientMatrix { get; private set; }
    [JsonIgnore] public float ClientScale { get; private set; }

    [JsonIgnore] public Rectangle ClientRegionOfInterest { get; private set; }
    [JsonIgnore] public Rectangle ClientRegionOfCircle { get; private set; }
    [JsonIgnore] public Rectangle ClientRegionOfKeySelection { get; private set; }
}

[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(AppConfig))]
internal partial class SourceGenerationContext : JsonSerializerContext
{
}
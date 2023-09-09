using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using System.Text.Json;
using System.Text.Json.Serialization;
using StarfieldLockpicker.Inputs;

namespace StarfieldLockpicker;

[Serializable]
public class AppConfig
{
    private const string ConfigPath = "config.json";

    public static AppConfig Instance
    {
        get
        {
            if (_instance is not null)
                return _instance;
            if (!TryLoadOrCreateConfig(out _instance))
            {
                Console.WriteLine("failed to load config. exiting");
                throw new Exception();
            }
            return _instance;
        }
    }

    private static AppConfig? _instance;

    private static bool TryLoadOrCreateConfig([NotNullWhen(true)] out AppConfig? result)
    {
        if (File.Exists(ConfigPath))
        {
            var text = File.ReadAllText(ConfigPath);
            try
            {
                var deserialized = JsonSerializer.Deserialize<AppConfig>(text, new JsonSerializerOptions
                {
                    ReadCommentHandling = JsonCommentHandling.Skip
                }) ?? throw new NullReferenceException("null deserialized");

                Console.WriteLine("config loaded");
                result = deserialized;
                Init(result);
                return true;
            }
            catch (Exception e)
            {
                Console.WriteLine("failed to load config!");
                Console.WriteLine(e);
                result = null;
                return false;
            }
        }

        result = new AppConfig();
        {
            Init(result);
        }
        var serialized = JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(ConfigPath, serialized);
        Console.WriteLine("no config found, creating default config.");
        return true;
    }

    private static void Init(AppConfig result)
    {
        var screenSize = Screen.AllScreens[result.Display].Bounds.Size;
        result.ScreenWidth = screenSize.Width;
        result.ScreenHeight = screenSize.Height;

        result.ReferenceUIScale = Math.Min(result.ReferenceResolutionWidth / 16f, result.ReferenceResolutionHeight / 9f);
        result.ReferenceUIWidth = result.ReferenceUIScale * 16;
        result.ReferenceUIHeight = result.ReferenceUIScale * 9;
        result.ScreenUIScale = Math.Min(result.ScreenWidth / 16f, result.ScreenHeight / 9f);
        result.ScreenUIWidth = result.ScreenUIScale * 16;
        result.ScreenUIHeight = result.ScreenUIScale * 9;

        var refCenter = new Vector2(result.ReferenceResolutionWidth, result.ReferenceResolutionHeight) / 2f;
        var circleR = (result.CircleRadiusKey + result.SampleRadiusKey) + 2;

        var rocF = new RectangleF(refCenter.X - circleR, refCenter.Y - circleR, circleR + circleR,
            circleR + circleR);
        result.RegionOfCircle = Rectangle.Ceiling(rocF);

        result.RegionOfKeySelection = new Rectangle(result.KeyAreaX0 - 1, result.KeyAreaY0 - 1, result.KeyAreaWidth + 2, result.KeyAreaHeight + 2);

        result.RegionOfInterest = Rectangle.Union(result.RegionOfCircle, result.RegionOfKeySelection);

        VKCode vk;
        if (!Enum.TryParse(result.HotKey, true, out vk))
            Utility.ConsoleError($"Key {result.HotKey} can not be parsed");
        result.VirtualHotKey = vk;

        if (!Enum.TryParse(result.KeyPrevious, true, out vk))
            Utility.ConsoleError($"Key {result.KeyPrevious} can not be parsed");
        result.VirtualPrevious = vk;

        if (!Enum.TryParse(result.KeyNext, true, out vk))
            Utility.ConsoleError($"Key {result.KeyNext} can not be parsed");
        result.VirtualNext = vk;

        if (!Enum.TryParse(result.KeyRotateAntiClockwise, true, out vk))
            Utility.ConsoleError($"Key {result.KeyRotateAntiClockwise} can not be parsed");
        result.VirtualRotateAntiClockwise = vk;

        if (!Enum.TryParse(result.KeyRotateClockwise, true, out vk))
            Utility.ConsoleError($"Key {result.KeyRotateClockwise} can not be parsed");
        result.VirtualRotateClockwise = vk;

        if (!Enum.TryParse(result.KeyInsert, true, out vk))
            Utility.ConsoleError($"Key {result.KeyInsert} can not be parsed");
        result.VirtualInsert = vk;
    }

    public int Display { get; set; } = 0;
    public string HotKey { get; set; } = "F10";
    public string KeyPrevious { get; set; } = "Q";
    public string KeyNext { get; set; } = "T";
    public string KeyRotateAntiClockwise { get; set; } = "A";
    public string KeyRotateClockwise { get; set; } = "D";
    public string KeyInsert { get; set; } = "E";

    public float ResponseWaitTimeout { get; set; } = 1000;
    public float IntervalForUIRefresh { get; set; } = 20;
    public float IntervalForCommandExecution { get; set; } = 20;
    public float IntervalForLayerCompleteAnimation { get; set; } = 1100;
    public float IntervalForKeyboardClick { get; set; } = 15;
    public float IntervalBetweenKeyboardClick { get; set; } = 15;

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
    [JsonIgnore] public VKCode VirtualPrevious { get; private set; }
    [JsonIgnore] public VKCode VirtualNext { get; private set; }
    [JsonIgnore] public VKCode VirtualRotateAntiClockwise { get; private set; }
    [JsonIgnore] public VKCode VirtualRotateClockwise { get; private set; }
    [JsonIgnore] public VKCode VirtualInsert { get; private set; }
    [JsonIgnore] public VKCode VirtualWorkaround { get; private set; }

    [JsonIgnore] public int ScreenWidth { get; private set; }
    [JsonIgnore] public int ScreenHeight { get; private set; }
    [JsonIgnore] public float ReferenceUIScale { get; private set; }
    [JsonIgnore] public float ReferenceUIWidth { get; private set; }
    [JsonIgnore] public float ReferenceUIHeight { get; private set; }
    [JsonIgnore] public float ScreenUIScale { get; private set; }
    [JsonIgnore] public float ScreenUIWidth { get; private set; }
    [JsonIgnore] public float ScreenUIHeight { get; private set; }
    [JsonIgnore] public Rectangle RegionOfInterest { get; private set; }
    [JsonIgnore] public Rectangle RegionOfCircle { get; private set; }
    [JsonIgnore] public Rectangle RegionOfKeySelection { get; private set; }
}
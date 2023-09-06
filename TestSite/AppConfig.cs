using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace StarfieldLockpicker;

[Serializable]
public class AppConfig
{
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
        result = new AppConfig();
        {
            result.ScreenWidth = 2560;
            result.ScreenHeight = 1600;
        }
        return true;
    }

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

    public int Display { get; set; } = 0;

    public string HotKey { get; set; } = "F10";

    public bool PrintMaxColor0 { get; set; } = false;
    public bool PrintMaxColor1 { get; set; } = false;
    public bool PrintMaxColor2 { get; set; } = false;
    public bool PrintMaxColor3 { get; set; } = false;
    public bool PrintMaxColorKey { get; set; } = false;

    [JsonIgnore]
    public int ScreenWidth { get; private set; }

    [JsonIgnore]
    public int ScreenHeight { get; private set; }

    [JsonIgnore]
    public float ReferenceUIScale => Math.Min(ReferenceResolutionWidth / 16f, ReferenceResolutionHeight / 9f);
    [JsonIgnore]
    public float ReferenceUIWidth => ReferenceUIScale * 16;
    [JsonIgnore]
    public float ReferenceUIHeight => ReferenceUIScale * 9;
    [JsonIgnore]
    public float ScreenUIScale => Math.Min(ScreenWidth / 16f, ScreenHeight / 9f);
    [JsonIgnore]
    public float ScreenUIWidth => ScreenUIScale * 16;
    [JsonIgnore]
    public float ScreenUIHeight => ScreenUIScale * 9;
}
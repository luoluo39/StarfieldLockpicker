using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using System.Text.Json;
using System.Text.Json.Serialization;

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
                var deserialized = JsonSerializer.Deserialize<AppConfig>(text) ?? throw new NullReferenceException("null deserialized");

                Console.WriteLine("config loaded");
                result = deserialized;

                var screenSize = Screen.AllScreens[result.Display].Bounds.Size;
                result.ScreenWidth = screenSize.Width;
                result.ScreenHeight = screenSize.Height;
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
            var screenSize = Screen.AllScreens[result.Display].Bounds.Size;
            result.ScreenWidth = screenSize.Width;
            result.ScreenHeight = screenSize.Height;
        }
        var serialized = JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(ConfigPath, serialized);
        Console.WriteLine("no config found, creating default config.");
        return true;
    }

    public float CircleCenterX { get; set; } = 960;
    public float CircleCenterY { get; set; } = 540;

    public float CircleRadius0 { get; set; } = 205;
    public float CircleRadius1 { get; set; } = 168;
    public float CircleRadius2 { get; set; } = 135;
    public float CircleRadius3 { get; set; } = 105;
    public float CircleRadiusKey { get; set; } = 240;

    public float SampleRadius0 { get; set; } = 10;
    public float SampleRadius1 { get; set; } = 10;
    public float SampleRadius2 { get; set; } = 10;
    public float SampleRadius3 { get; set; } = 5;
    public float SampleRadiusKey { get; set; } = 7;

    public float SampleThr0 { get; set; } = 0.2f;
    public float SampleThr1 { get; set; } = 0.2f;
    public float SampleThr2 { get; set; } = 0.2f;
    public float SampleThr3 { get; set; } = 0.2f;
    public float SampleThrKey { get; set; } = 0.2f;

    public int KeyAreaX0 { get; set; } = 1333;
    public int KeyAreaY0 { get; set; } = 130;
    public int KeyAreaWidth { get; set; } = 494;
    public int KeyAreaHeight { get; set; } = 744;

    public int ReferenceResolutionWidth { get; set; } = 1920;
    public int ReferenceResolutionHeight { get; set; } = 1080;

    public double ImageMseThr { get; set; } = 45;

    public int Display { get; set; } = 0;

    public string HotKey { get; set; } = "F10";

    public bool PrintMaxColor0 { get; set; } = true;
    public bool PrintMaxColor1 { get; set; } = true;
    public bool PrintMaxColor2 { get; set; } = true;
    public bool PrintMaxColor3 { get; set; } = true;
    public bool PrintMaxColorKey { get; set; } = false;

    [JsonIgnore]
    public int ScreenWidth { get; private set; }

    [JsonIgnore]
    public int ScreenHeight { get; private set; }

    [JsonIgnore]
    public Vector2 ScreenSizeVector => new(ScreenWidth, ScreenHeight);

    [JsonIgnore]
    public Size ScreenSize => new(ScreenWidth, ScreenHeight);
}
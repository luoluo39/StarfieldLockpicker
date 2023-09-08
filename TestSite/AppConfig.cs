using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace TestSite;

[Serializable]
public class AppConfig
{
    public static AppConfig Instance { get; set; }

    private static AppConfig? _instance;

    public static int DefaultScreenWidth = 0;
    public static int DefaultScreenHeight = 0;

    public static AppConfig? LoadOrCreateConfig(string? configPath)
    {
        if (File.Exists(configPath))
        {
            var text = File.ReadAllText(configPath);
            try
            {
                var deserialized = JsonSerializer.Deserialize<AppConfig>(text) ?? throw new NullReferenceException("null deserialized");

                Init(deserialized);
                return deserialized;
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                return null;
            }
        }

        var result = new AppConfig();
        Init(result);
        return result;
    }

    private static void Init(AppConfig result)
    {
        var screenSize = Screen.AllScreens[result.Display].Bounds.Size;
        result.ScreenWidth = DefaultScreenWidth != 0 ? DefaultScreenWidth : screenSize.Width;
        result.ScreenHeight = DefaultScreenHeight != 0 ? DefaultScreenHeight : screenSize.Height;

        result.ReferenceUIScale = Math.Min(result.ReferenceResolutionWidth / 16f, result.ReferenceResolutionHeight / 9f);
        result.ReferenceUIWidth = result.ReferenceUIScale * 16;
        result.ReferenceUIHeight = result.ReferenceUIScale * 9;
        result.ScreenUIScale = Math.Min(result.ScreenWidth / 16f, result.ScreenHeight / 9f);
        result.ScreenUIWidth = result.ScreenUIScale * 16;
        result.ScreenUIHeight = result.ScreenUIScale * 9;

        var refCenter = new Vector2(result.ReferenceResolutionWidth, result.ReferenceResolutionHeight) / 2f;
        var roiMin = refCenter;
        var roiMax = refCenter;

        var circleR = Vector2.One * (result.CircleRadiusKey + result.SampleRadiusKey) * 1.05f;
        roiMin = Vector2.Min(roiMin, refCenter - circleR);
        roiMax = Vector2.Max(roiMax, refCenter + circleR);

        roiMin = Vector2.Min(roiMin, new Vector2(result.KeyAreaX0, result.KeyAreaY0));
        roiMax = Vector2.Max(roiMax, new Vector2(result.KeyAreaX0 + result.KeyAreaWidth, result.KeyAreaY0 + result.KeyAreaHeight));

        roiMin = Utility.TranslatePosition(roiMin, result);
        roiMax = Utility.TranslatePosition(roiMax, result);

        var roiMinPoint = new Point
        {
            X = int.Max(0, (int)float.Floor(roiMin.X)),
            Y = int.Max(0, (int)float.Floor(roiMin.Y))
        };

        var roiMaxPoint = new Point
        {
            X = int.Min(result.ScreenWidth, (int)float.Ceiling(roiMax.X)),
            Y = int.Min(result.ScreenHeight, (int)float.Ceiling(roiMax.Y))
        };

        result.RegionOfInterest = new Rectangle(roiMinPoint, new Size(roiMaxPoint.X - roiMinPoint.X, roiMaxPoint.Y - roiMinPoint.Y));
    }

    public int Display { get; set; } = 0;

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

    [JsonIgnore] public int ScreenWidth { get; private set; }
    [JsonIgnore] public int ScreenHeight { get; private set; }
    [JsonIgnore] public float ReferenceUIScale { get; private set; }
    [JsonIgnore] public float ReferenceUIWidth { get; private set; }
    [JsonIgnore] public float ReferenceUIHeight { get; private set; }
    [JsonIgnore] public float ScreenUIScale { get; private set; }
    [JsonIgnore] public float ScreenUIWidth { get; private set; }
    [JsonIgnore] public float ScreenUIHeight { get; private set; }
    [JsonIgnore] public Rectangle RegionOfInterest { get; private set; }
}
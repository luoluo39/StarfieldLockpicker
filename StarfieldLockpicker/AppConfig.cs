namespace StarfieldLockpicker;

[Serializable]
public class AppConfig
{
    public float CircleCenterX { get; set; } = 960;
    public float CircleCenterY { get; set; } = 540;

    public float CircleRadius0 { get; set; } = 205;
    public float CircleRadius1 { get; set; } = 168;
    public float CircleRadius2 { get; set; } = 135;
    public float CircleRadiusKey { get; set; } = 240;

    public float SampleRadius0 { get; set; } = 10;
    public float SampleRadius1 { get; set; } = 10;
    public float SampleRadius2 { get; set; } = 10;
    public float SampleRadiusKey { get; set; } = 7;

    public float SampleThr0 { get; set; } = 80;
    public float SampleThr1 { get; set; } = 40;
    public float SampleThr2 { get; set; } = 40;
    public float SampleThrKey { get; set; } = 80;
}
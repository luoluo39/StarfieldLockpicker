// See https://aka.ms/new-console-template for more information

using System.Drawing.Imaging;
using System.Numerics;
using StarfieldLockpicker;

Console.WriteLine("Hello, World!");

uint[] lockShapes = new uint[4];

var bm = (Bitmap)Image.FromFile(@"C:\Users\24580\Downloads\U45Nrwt222.png");

var bm1 = Utility.FillKeyArea(bm, Color.AliceBlue);
bm1.Save("awgahrah.png", ImageFormat.Png);
var shape0 = GetShape32(bm,
    205,
    10,
    0.175f,
    true);

var shape1 = GetShape32(bm,
    168,
    10,
    0.175f,
    true);

var shape2 = GetShape32(bm,
    135,
    10,
    0.175f,
    true);

var shape3 = GetShape32(bm,
    105,
    7,
    0.175f,
    true);

var shapeK = GetShape32(bm,
    240,
    7,
    0.175f,
    true);

lockShapes[0] = shape0;
lockShapes[1] = shape1;
lockShapes[2] = shape2;
lockShapes[3] = shape3;


Console.WriteLine("Lock shapes:");
for (int i = 0; i < lockShapes.Length; i++)
{
    Console.Write($"  {i}: ");
    ConsoleWriteShape32(lockShapes[i]);
}
ConsoleWriteShape32(shapeK);
static void ConsoleWriteShape32(uint shape)
{
    for (int i = 0; i < 32; i++)
    {
        var x = (shape >> i) & 1;
        Console.Write(x);
    }
    Console.WriteLine();
}

uint GetShape32(Bitmap image, float circleRadius, float sampleRadius, float thr, bool print = false)
{
    var center = new Vector2(960, 540);
    var scaledCenter = Utility.ScalePosition(center);
    var scaledRadius = Utility.ScaleRadius(circleRadius);
    var scaledSampleRadius = Utility.ScaleRadius(sampleRadius);

    uint v = 0;
    for (var i = 0; i < 32; i++)
    {
        var x = 2 * float.Pi * i / 32;
        var (sin, cos) = float.SinCos(x);
        var pos = new Vector2(cos, sin) * scaledRadius + scaledCenter;
        var gray = Utility.CalculateMaxB(image, pos, scaledSampleRadius);

        if (print) Console.Write($",{gray:F2}");

        v |= gray > thr ? 1U << i : 0;
    }
    if (print) Console.WriteLine();

    return v;
}
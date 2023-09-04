using System;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Drawing;
using System.Drawing.Imaging;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using StarfieldLockpicker.Inputs;

namespace StarfieldLockpicker;

public class UnlockApp : IDisposable
{
    private Task? runningTask;
    private AppConfig config = AppConfig.Instance;
    private CancellationToken cancellationToken;
    private int status;

    public UnlockApp(CancellationToken cancellationToken)
    {
        this.cancellationToken = cancellationToken;
    }

    public void Run(MessageWindow messageWindow)
    {
        Input.ForceReload();

        messageWindow.OnHoyKeyPressed += OnKeybdHookOnOnKeyboardEvent;
    }

    private void OnKeybdHookOnOnKeyboardEvent()
    {
        if (TrySwitchStatus(AppStatus.Ready, AppStatus.Running))
        {
            //start a task to avoid (stuck or exception in a hook callback and block all keyboard inputs)
            runningTask = UnlockAsync(cancellationToken);
            runningTask.ContinueWith(task => TrySwitchStatus(AppStatus.Running, AppStatus.Ready), cancellationToken);
        }
    }

    private bool TrySwitchStatus(AppStatus from, AppStatus to)
    {
        return Interlocked.CompareExchange(ref status, (int)to, (int)from) == (int)from;
    }

    [SuppressMessage("ReSharper", "MethodSupportsCancellation")]
    private async Task UnlockAsync(CancellationToken cancellationToken)
    {
        Console.WriteLine("Begin");
        //first we get a image of each level of the lock (and also the first key)
        var (keys, locks) = await CaptureLockAndKeys();

        //to reduce search range, we find every possible position of each key
        Console.WriteLine("Captured");
        var keyPositions = FindKeyPositions(keys, locks);

        //pick one pos for each key, and find the first solve
        var result = FindFirstSolve(locks, keys, keyPositions);
        if (result is null)
        {
            Console.WriteLine("No solve, maybe try again?");
            return;
        }

        Console.WriteLine("first solve:");
        foreach (var (level, rotation) in result)
        {
            Console.WriteLine($"Level: {level}, Rot:{rotation / 2}");
        }

        await Task.Delay(50);

        //rotate each key to the calculated position
        await RotateAndInsertKeys(result);
    }

    private static async Task RotateAndInsertKeys(KeyLevelRot[] result)
    {
        foreach (var t in result)
        {
            if (t.Rotation > 16)
            {
                for (int j = 0; j < 32 - t.Rotation; j++)
                {
                    Input.KeyboardKeyClick(VKCode.A, 50);
                    await Task.Delay(50);
                }
            }
            else
            {
                for (int j = 0; j < t.Rotation; j++)
                {
                    Input.KeyboardKeyClick(VKCode.D, 50);
                    await Task.Delay(50);
                }
            }

            Input.KeyboardKeyClick(VKCode.T, 50);
            await Task.Delay(50);
        }

        await Task.Delay(50);
        //insert keys
        int cursorPos = 0;

        var maxLevel = result.Max(t => t.Level);
        for (var level = 0; level <= maxLevel; level++)
        {
            var lvl = level;
            var remains = result.Count(t => t.Level == lvl);
            while (remains > 0)
            {
                var keyLevel = result[cursorPos].Level;
                if (keyLevel == level)
                {
                    Input.KeyboardKeyClick(VKCode.E, 50);
                    remains--;
                }
                else if (keyLevel > level || keyLevel == -1)
                {
                    //manual bypass
                    Input.KeyboardKeyClick(VKCode.T, 50);
                }
                else if (keyLevel < level)
                {
                    //auto bypass
                }

                await Task.Delay(50);
                cursorPos = (cursorPos + 1) % result.Length;
            }

            await Task.Delay(1200);
            Console.WriteLine($"Finish Level {level}");
        }

        Console.WriteLine($"Finish All Levels");
    }

    private static List<KeyLevelRot>[] FindKeyPositions(uint[] keys, uint[] levels)
    {
        var keyPositions = keys.Select(_ => new List<KeyLevelRot>()).ToArray();
        for (var i = 0; i < keys.Length; i++)
        {
            var keyShape = keys[i];
            var positions = keyPositions[i];
            positions.Add(new(-1, 0));
            for (var k = 0; k < levels.Length; k++)
            {
                for (var j = 0; j < 32; j++)
                {
                    var rotated = uint.RotateLeft(keyShape, j);
                    if (0 == (rotated & levels[k]))
                    {
                        //no collision
                        positions.Add(new(k, j));
                    }
                }
            }
        }

        return keyPositions;
    }

    private async Task<(uint[] keys, uint[] levels)> CaptureLockAndKeys()
    {
        List<uint> keyShapes = new();

        int counter = 0;
        Bitmap? firstImage = null;
        while (true)
        {
            var image = Utility.CaptureScreen(config.Display);
            var shape = GetShape32(image, config.CircleRadiusKey, config.SampleRadiusKey, config.SampleThrKey);
            if (firstImage is not null)
            {
                var mse = Utility.CalculateKeyAreaMSE(image, firstImage);
                Console.WriteLine($"image mse: {mse}");

                if (mse < AppConfig.Instance.ImageMseThr)
                    break;

                image.Dispose();
            }
            else
                firstImage = image;

            keyShapes.Add(shape);

            Input.KeyboardKeyClick(VKCode.T, 50);
            await Task.Delay(50);

            if (counter++ > 20)
                throw new Exception();
        }

        uint[] lockShapes = new uint[4];
        {
            var shape0 = GetShape32(firstImage,
                config.CircleRadius0,
                config.SampleRadius0,
                config.SampleThr0,
                config.PrintMaxColor0);

            var shape1 = GetShape32(firstImage,
                config.CircleRadius1,
                config.SampleRadius1,
                config.SampleThr1,
                config.PrintMaxColor1);

            var shape2 = GetShape32(firstImage,
                config.CircleRadius2,
                config.SampleRadius2,
                config.SampleThr2,
                config.PrintMaxColor2);

            var shape3 = GetShape32(firstImage,
                config.CircleRadius3,
                config.SampleRadius3,
                config.SampleThr3,
                config.PrintMaxColor3);

            if (shape0 == 0)
                shape0 = uint.MaxValue;
            if (shape1 == 0)
                shape1 = uint.MaxValue;
            if (BitOperations.PopCount(shape2) < 10)
                shape2 = uint.MaxValue;
            if (BitOperations.PopCount(shape3) < 10)
                shape3 = uint.MaxValue;

            lockShapes[0] = shape0;
            lockShapes[1] = shape1;
            lockShapes[2] = shape2;
            lockShapes[3] = shape3;
        }

        firstImage.Dispose();

        Console.WriteLine("Lock shapes:");
        for (int i = 0; i < lockShapes.Length; i++)
        {
            Console.Write($"  {i}: ");
            ConsoleWriteShape32(lockShapes[i]);
        }

        Console.WriteLine("Key shapes:");
        for (int i = 0; i < keyShapes.Count; i++)
        {
            Console.Write($"  {i}: ");
            ConsoleWriteShape32(keyShapes[i]);
        }

        return (keyShapes.ToArray(), lockShapes);
    }

    private uint GetShape32(Bitmap image, float circleRadius, float sampleRadius, float thr, bool print = false)
    {
        var center = new Vector2(config.CircleCenterX, config.CircleCenterY);
        var scaledCenter = Utility.ScalePosition(center);
        var scaledRadius = Utility.ScaleRadius(circleRadius);
        var scaledSampleRadius = Utility.ScaleRadius(sampleRadius);

        uint v = 0;
        for (var i = 0; i < 32; i++)
        {
            var x = 2 * float.Pi * i / 32;
            var (sin, cos) = float.SinCos(x);
            var pos = new Vector2(cos, sin) * scaledRadius + scaledCenter;
            var gray = Utility.CalculateMaxColor(image, pos, scaledSampleRadius, print);
            v |= gray > thr ? 1U << i : 0;
        }
        return v;
    }

    static void ConsoleWriteShape32(uint shape)
    {
        for (int i = 0; i < 32; i++)
        {
            var x = (shape >> i) & 1;
            Console.Write(x);
        }
        Console.WriteLine();
    }

    //private static KeyLevelRot[]? FindFirstSolve(uint[] locks, uint[] keys, ReadOnlySpan<List<KeyLevelRot>> //keyPositionsArray)
    //{
    //    Span<uint> pg = stackalloc uint[locks.Length];
    //    Span<int> indexes = stackalloc int[keys.Length];
    //    locks.CopyTo(pg);
    //    return FindFirstSolve2(pg, indexes, keys, keyPositionsArray);
    //}

    //private static KeyLevelRot[]? FindFirstSolve2(Span<uint> pg, Span<int> indexes, uint[] keys, //ReadOnlySpan<List<KeyLevelRot>> keyPositionsArray)
    //{
    //    for (var i = 0; i < keyPositionsArray[0].Count; i++)
    //    {
    //        var keyLevelRot = keyPositionsArray[0][i];
    //        var rotated = uint.RotateLeft(keys[i], keyLevelRot.Rotation);
    //        if (keyLevelRot.Level >= 0 && (pg[keyLevelRot.Level] & rotated) != 0)
    //        {
    //            //collision
    //            continue;
    //        }
    //
    //        pg[keyLevelRot.Level] |= rotated;
    //
    //        if (keyPositionsArray.Length == 1)
    //        {
    //            if (Utility.CheckAllBitSet(pg))
    //            {
    //                //this is first solve
    //
    //                var result = new KeyLevelRot[keys.Length];
    //                for (var j = 0; j < keys.Length; j++)
    //                {
    //                    result[j] = keyPositionsArray[j][index[j]];
    //                }
    //                //we need only the first result
    //                return result;
    //            }
    //        }
    //        else
    //        {
    //            FindFirstSolve2(pg, keys, keyPositionsArray[1..]);
    //        }
    //    }
    //}

    private static KeyLevelRot[]? FindFirstSolve(uint[] locks, uint[] keys, List<KeyLevelRot>[] keyPositionsArray)
    {
        var dims = keyPositionsArray.Select(t => t.Count).ToImmutableArray();
        var maxExclusive = dims.Aggregate(1UL, (a, b) => a * (ulong)b);

        const int chunkSize = 65536;
        var chunkCount = (uint)(maxExclusive / chunkSize + ((maxExclusive % chunkSize > 0) ? 1UL : 0UL));

        Console.WriteLine($"combination count:{maxExclusive}");
        Console.WriteLine($"chunk count:{chunkCount}");

        StrongBox<KeyLevelRot[]?> resultBox = new(null);
        Parallel.For(0, chunkCount, (ci, s) =>
        {
            Span<int> index = stackalloc int[keyPositionsArray.Length];
            Span<uint> playground = stackalloc uint[locks.Length];

            var from = (ulong)ci * chunkSize;
            var to = Math.Min(maxExclusive, from + chunkSize);

            for (ulong comb = from; comb < to; comb++)
            {
                locks.CopyTo(playground);

                Utility.ExtractIndexes(comb, index, dims.AsSpan());
                int keyIndex;
                for (keyIndex = 0; keyIndex < index.Length; keyIndex++)
                {
                    var key = keys[keyIndex];
                    var keyPlaces = keyPositionsArray[keyIndex];

                    var (level, rot) = keyPlaces[index[keyIndex]];

                    if (level < 0)
                        continue;

                    var toInsert = uint.RotateLeft(key, rot);

                    if ((playground[level] & toInsert) != 0)
                        break;

                    playground[level] |= toInsert;
                }

                //if break in for loop
                if (keyIndex != index.Length) continue;

                if (Utility.CheckAllBitSet(playground))
                {
                    var result = new KeyLevelRot[keys.Length];
                    for (keyIndex = 0; keyIndex < index.Length; keyIndex++)
                    {
                        result[keyIndex] = keyPositionsArray[keyIndex][index[keyIndex]];
                    }
                    //we need only the first result
                    if (null == Interlocked.CompareExchange(ref resultBox.Value, result, null))
                        s.Break();
                    return;
                }
            }
        });


        return resultBox.Value;
    }

    public void Dispose()
    {
    }
}
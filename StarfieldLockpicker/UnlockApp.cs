using System;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Drawing;
using System.Drawing.Imaging;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using PInvoke;
using StarfieldLockpicker.Inputs;

namespace StarfieldLockpicker;

public class UnlockApp : IDisposable
{
    private Task? runningTask;
    private AppConfig config = AppConfig.Instance;
    private CancellationToken appCancellationToken;
    private CancellationTokenSource? taskCancellationTokenSource;
    private int status;

    public UnlockApp(CancellationToken cancellationToken)
    {
        this.appCancellationToken = cancellationToken;
    }

    public void Run(MessageWindow messageWindow)
    {
        Input.ForceReload();

        messageWindow.OnHoyKeyPressed += OnKeybdHookOnOnKeyboardEvent;
        appCancellationToken.Register(() =>
        {
            taskCancellationTokenSource?.Cancel();
        });
    }

    private void OnKeybdHookOnOnKeyboardEvent()
    {
        if (TrySwitchStatus(AppStatus.Ready, AppStatus.Running))
        {
            //start a task to avoid (stuck or exception in a hook callback and block all keyboard inputs)
            taskCancellationTokenSource = new();
            runningTask = UnlockAsync(taskCancellationTokenSource.Token);
            runningTask = runningTask.ContinueWith(task =>
            {
                TrySwitchStatus(AppStatus.Running, AppStatus.Ready);
                runningTask = null;
                Utility.ConsoleInfo($"task finished with status: {task.Status}");
                if (task.Exception is not null)
                {
                    if (task.Exception.InnerExceptions.Count == 1)
                    {
                        var exc = task.Exception.InnerExceptions.Single();
                        if (exc is TaskCanceledException or OperationCanceledException)
                            Utility.ConsoleWarning("Task canceled");
                        else if (exc is TerminatingException)
                            Utility.ConsoleWarning("Task terminated due to error");
                        else
                            Utility.ConsoleError($"Task completed with exception:\n{task.Exception}");
                    }
                    else
                        Utility.ConsoleError($"Task completed with exception:\n{task.Exception}");
                }
                taskCancellationTokenSource = null;
            }, taskCancellationTokenSource.Token);
        }
        else if (TrySwitchStatus(AppStatus.Running, AppStatus.Ready))
        {
            //press hotkey again will stop running task
            Utility.ConsoleInfo("Cancellation requested.");
            taskCancellationTokenSource.Cancel();
            try
            {
                runningTask.Wait();
            }
            catch (Exception)
            {
            }
            Utility.ConsoleInfo("Task canceled.");
        }
    }

    private bool TrySwitchStatus(AppStatus from, AppStatus to)
    {
        return Interlocked.CompareExchange(ref status, (int)to, (int)from) == (int)from;
    }

    private async Task UnlockAsync(CancellationToken cancellationToken)
    {
        Utility.ConsoleInfo("Begin");
        //first we get a image of each level of the lock (and also the first key)
        var (keys, keySelectionImages, locks) = await CaptureLockAndKeysAsync(cancellationToken);

        //to reduce search range, we find every possible position of each key
        Utility.ConsoleInfo("Captured");
        var keyPositions = FindKeyPositions(keys, locks);

        //pick one pos for each key, and find the first solve
        var result = FindFirstSolve(locks, keyPositions);
        if (result is null)
        {
            Utility.ConsoleWarning("No solve, maybe try again?");
            return;
        }

        Utility.ConsoleInfo("first solve:");
        foreach (var (_, level, rotation) in result)
        {
            Utility.ConsoleInfo($"Level: {level}, Rot:{rotation / 2}");
        }

        await Task.Delay(50, cancellationToken);

        //rotate each key to the calculated position
        await RotateAndInsertKeysAsnc(result, keySelectionImages, cancellationToken);

        foreach (var image in keySelectionImages)
        {
            image.Dispose();
        }
    }

    private async Task RotateAndInsertKeysAsnc(KeyLevelRot[] result, Bitmap[] keySelectionImages,
        CancellationToken cancellationToken)
    {
        for (var keyIndex = 0; keyIndex < result.Length; keyIndex++)
        {
            await SelectKeyAsync(keySelectionImages, keyIndex, cancellationToken);
            await RotateKeyAsync(result[keyIndex], cancellationToken);
        }

        var currentLevel = 0;
        var remains = result.Count(t => t.Level == currentLevel);
        var maxLevel = result.Max(t => t.Level);
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var selected = GetSelectedKeyIndex(keySelectionImages, out var image);
            Utility.ConsoleDebug($"Selected: {selected}:{result[selected].Level} current level: {currentLevel}");

            //pre-insert check
            //var key = result[selected];
            if (result[selected].Level == currentLevel)
            {
                Utility.ConsoleDebug($"Inserting: {selected}:{result[selected].Level}");

                await InsertKeyAsync(image, cancellationToken);
                if (--remains == 0)
                {
                    Utility.ConsoleInfo($"Finish Level {currentLevel}");
                    currentLevel++;
                    if (currentLevel > maxLevel)
                        break;
                    remains = result.Count(t => t.Level == currentLevel);
                    await Task.Delay(1200, cancellationToken);
                }
            }
            else
            {
                Input.KeyboardKeyClick(VKCode.T, 50);
            }

            await Task.Delay(100);
            image.Dispose();
        }
        Utility.ConsoleInfo($"Finish All Levels");
    }

    private async Task InsertKeyAsync(Bitmap? beforeInsert, CancellationToken cancellationToken)
    {
        beforeInsert ??= Utility.CaptureScreen(config.Display);

        for (int i = 0; i < 5; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            Input.KeyboardKeyClick(VKCode.E, 50);
            await Task.Delay(100, cancellationToken);

            Utility.ConsoleDebug("Insert command sent, waiting for 1000ms.");
            var success = await DoWaitingAsync(() =>
            {
                using var currentImage = Utility.CaptureScreen(config.Display);
                return Utility.CalculateKeyAreaMSE(currentImage, beforeInsert) >= config.ImageMseThr;
            }, 50, 1000, cancellationToken);

            if (success)
                return;

            Utility.ConsoleWarning($"insert timeout, retrying {i}/5");
        }


        Utility.ConsoleError("insert timeout. terminating");
        throw new TerminatingException();
    }

    private static async Task<bool> DoWaitingAsync(Func<bool> condition, int delay, float timeout,
        CancellationToken cancellationToken)
    {
        var begin = Stopwatch.GetTimestamp();
        while (true)
        {
            await Task.Delay(delay, cancellationToken);
            if (condition()) return true;

            var time = Stopwatch.GetElapsedTime(begin);
            if (time.TotalMilliseconds > timeout)
                return false;
        }
    }

    private async Task RotateKeyAsync(KeyLevelRot key, CancellationToken cancellationToken)
    {
        var (initialShape, _, rotation) = key;

        var rotated = uint.RotateLeft(initialShape, rotation);
        var rot = rotation;
        int counter = 0;
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            await RotateAsync(rot, cancellationToken);

            var current = GetKeyShape32();
            rot = GetDeltaRotate(current, rotated);
            if (rot == 0)
                break;

            Utility.ConsoleWarning("Warning: key is not rotated to position or shape does not match. waiting for 1000ms.");

            StrongBox<int> rotBox = new();
            var success = await DoWaitingAsync(() =>
                {
                    var current = GetKeyShape32();
                    rotBox.Value = GetDeltaRotate(current, rotated);
                    return rotBox.Value == 0;
                }, 50, 1000, cancellationToken);

            if (success)
                break;
            rot = rotBox.Value;

            if (rot == -1)
            {
                Utility.ConsoleError("Key shape do not match, what is happening?");
                throw new TerminatingException();
            }

            Utility.ConsoleWarning($"Warning: key is not rotated to position. retrying {counter}/10.");

            if (counter > 10)
            {
                Utility.ConsoleError("Failed to rotate key to position, too many times.");
                throw new TerminatingException();
            }
            counter++;
        }
    }

    private async Task SelectKeyAsync(Bitmap[] keySelectionImages, int keyIndex, CancellationToken cancellationToken)
    {
        int times = 0;
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var actualKeyIndex = GetSelectedKeyIndex(keySelectionImages);
            var delta = keyIndex - actualKeyIndex;

            if (delta == 0)
                break;

            if (times != 0)
            {
                if (times % 2 == 1)
                {
                    Utility.ConsoleWarning(
                        $"Warning: key is not selected, waiting for 1000ms. current={actualKeyIndex} dest={keyIndex} delta={delta}");

                    var success = await DoWaitingAsync(() => keyIndex == GetSelectedKeyIndex(keySelectionImages), 50, 1000, cancellationToken);

                    if (success)
                        break;
                    times++;
                    continue;
                }
                Utility.ConsoleWarning(
                    $"Warning: failed to select key, retrying. current={actualKeyIndex} dest={keyIndex} delta={delta}");
            }
            if (times > 5)
            {
                Utility.ConsoleError("Failed to select the key, too many times.");
                throw new TerminatingException();
            }

            for (int i = 0; i < Math.Abs(delta); i++)
            {
                if (delta > 0)
                    Input.KeyboardKeyClick(VKCode.T, 50);
                else
                    Input.KeyboardKeyClick(VKCode.Q, 50);
                await Task.Delay(50);
            }

            times++;
        }
        Utility.ConsoleDebug($"Selected key {keyIndex}");
    }

    private int GetSelectedKeyIndex(Bitmap[] keySelectionImages)
    {
        using var image = Utility.CaptureScreen(config.Display);

        return keySelectionImages.Select((t, i) => (t, i)).MinBy(t => Utility.CalculateKeyAreaMSE(image, t.t)).i;
    }
    private int GetSelectedKeyIndex(Bitmap[] keySelectionImages, out Bitmap image)
    {
        image = Utility.CaptureScreen(config.Display);
        var img = image;
        return keySelectionImages.Select((t, i) => (t, i)).MinBy(t => Utility.CalculateKeyAreaMSE(img, t.t)).i;
    }

    private int GetDeltaRotate(uint src, uint dst)
    {
        for (int i = 0; i < 32; i++)
        {
            if (uint.RotateLeft(src, i) == dst)
                return i;
        }
        return -1;
    }

    private static async Task RotateAsync(int r, CancellationToken cancellationToken)
    {
        if (r > 16)
        {
            for (int j = 0; j < 32 - r; j++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                Input.KeyboardKeyClick(VKCode.A, 50);
                await Task.Delay(20, cancellationToken);
            }
        }
        else
        {
            for (int j = 0; j < r; j++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                Input.KeyboardKeyClick(VKCode.D, 50);
                await Task.Delay(20, cancellationToken);
            }
        }
        await Task.Delay(100, cancellationToken);
    }

    private static List<KeyLevelRot>[] FindKeyPositions(uint[] keys, uint[] levels)
    {
        var keyPositions = keys.Select(_ => new List<KeyLevelRot>()).ToArray();
        for (var i = 0; i < keys.Length; i++)
        {
            var keyShape = keys[i];
            var positions = keyPositions[i];
            positions.Add(new(keyShape, -1, 0));
            for (var k = 0; k < levels.Length; k++)
            {
                for (var j = 0; j < 32; j++)
                {
                    var rotated = uint.RotateLeft(keyShape, j);
                    if (0 == (rotated & levels[k]))
                    {
                        //no collision
                        positions.Add(new(keyShape, k, j));
                    }
                }
            }
        }

        return keyPositions;
    }

    private async Task<(uint[] keys, Bitmap[] keyShapes, uint[] levels)> CaptureLockAndKeysAsync(
        CancellationToken cancellationToken)
    {
        List<(uint, Bitmap)> keyShapes = new();

        var tryCounter = 5;
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var image = Utility.CaptureScreen(config.Display);
            //keys are fine with old method
            if (keyShapes.Count > 0)
            {
                var mse0 = Utility.CalculateKeyAreaMSE(image, keyShapes.Last().Item2);
                //this is same image, but game miss the input
                if (mse0 < AppConfig.Instance.ImageMseThr)
                {
                    if (tryCounter-- <= 0)
                    {
                        Utility.ConsoleError("Image seems to be frozen. Are you really running this in game?");
                        throw new TerminatingException();
                    }
                    Utility.ConsoleWarning($"Image not changed. mse: {mse0}");
                    image.Dispose();
                    await Task.Delay(100, cancellationToken);
                    continue;
                }

                var mse = Utility.CalculateKeyAreaMSE(image, keyShapes.First().Item2);
                Utility.ConsoleDebug($"image mse: {mse},{mse0}");

                if (mse < AppConfig.Instance.ImageMseThr)
                    break;

                tryCounter = 5;
            }
            var shape = GetShape32(image, config.CircleRadiusKey, config.SampleRadiusKey, config.SampleThrKey, config.PrintMaxColorKey);
            Utility.ConsoleInfo($"captured key {keyShapes.Count}");
            keyShapes.Add((shape, image));

            Input.KeyboardKeyClick(VKCode.T, 50);
            await Task.Delay(100, cancellationToken);

            if (keyShapes.Count > 16)
            {
                Utility.ConsoleError("Too many keys! Terminating");
                throw new TerminatingException();
            }
        }

        uint[] lockShapes = new uint[4];
        {
            var firstImage = keyShapes.First().Item2;
            var shape0 = GradGetLockShape32(firstImage, 0);
            var shape1 = GradGetLockShape32(firstImage, 1);
            var shape2 = GradGetLockShape32(firstImage, 2);
            var shape3 = GradGetLockShape32(firstImage, 3);


            if (BitOperations.PopCount(shape0) < 10)
            {
                shape0 = uint.MaxValue;
                Utility.ConsoleWarning("Warning: less than 10 detect for layer 0, this is not supposed to be happening");
            }
            if (BitOperations.PopCount(shape1) < 10)
            {
                shape1 = uint.MaxValue;
                Utility.ConsoleWarning("Warning: less than 10 detect for layer 1, this is not supposed to be happening");
            }
            if (BitOperations.PopCount(shape2) < 10 && shape2 != 0)
            {
                shape2 = uint.MaxValue;
                Utility.ConsoleWarning("Warning: less than 10 but not 0 detect for layer 2.");
            }
            if (shape2 == 0)
                shape2 = uint.MaxValue;
            if (BitOperations.PopCount(shape3) < 10 && shape3 != 0)
            {
                shape3 = uint.MaxValue;
                Utility.ConsoleWarning("Warning: less than 10 but not 0 detect for layer 3.");
            }
            if (shape3 == 0)
                shape3 = uint.MaxValue;

            lockShapes[0] = shape0;
            lockShapes[1] = shape1;
            lockShapes[2] = shape2;
            lockShapes[3] = shape3;
        }

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
            ConsoleWriteShape32(keyShapes[i].Item1);
        }

        return (keyShapes.Select(t => t.Item1).ToArray(), keyShapes.Select(t => t.Item2).ToArray(), lockShapes);
    }

    private uint GetKeyShape32(Bitmap image)
    {
        return GetShape32(image, config.CircleRadiusKey, config.SampleRadiusKey, config.SampleThrKey, config.PrintMaxColorKey);
    }

    private uint GetKeyShape32()
    {
        using var image = Utility.CaptureScreen(config.Display);
        return GetKeyShape32(image);
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
            var gray = Utility.CalculateMaxB(image, pos, scaledSampleRadius);

            if (print) Console.Write($",{gray:F2}");

            v |= gray > thr ? 1U << i : 0;
        }
        if (print) Console.WriteLine();

        return v;
    }

    private uint GradGetLockShape32(Bitmap image, int layer)
    {
        var sp0 = (config.CircleRadius0 + config.CircleRadiusKey) / 2f;
        var sp1 = (config.CircleRadius1 + config.CircleRadius0) / 2f;
        var sp2 = (config.CircleRadius2 + config.CircleRadius1) / 2f;
        var sp3 = (config.CircleRadius3 + config.CircleRadius2) / 2f;
        var sp4 = 2 * sp3 - sp2;

        return layer switch
        {
            0 => GradGetShape32(image, sp1, sp0, config.SampleRadius0, 10f, config.SampleThr0, config.PrintMaxColor0),
            1 => GradGetShape32(image, sp2, sp1, config.SampleRadius1, 10f, config.SampleThr1, config.PrintMaxColor1),
            2 => GradGetShape32(image, sp3, sp2, config.SampleRadius2, 10f, config.SampleThr2, config.PrintMaxColor2),
            3 => GradGetShape32(image, sp4, sp3, config.SampleRadius3, 10f, config.SampleThr3, config.PrintMaxColor3),
            _ => throw new ArgumentOutOfRangeException(nameof(layer))
        };
    }

    private uint GradGetShape32(Bitmap image, float minRadius, float maxRadius, float sampleRadius, float stepLen, float thr, bool print = false)
    {
        var center = new Vector2(config.CircleCenterX, config.CircleCenterY);
        var scaledCenter = Utility.ScalePosition(center);
        var scaledMaxRadius = Utility.ScaleRadius(maxRadius);
        var scaledMinRadius = Utility.ScaleRadius(minRadius);
        var scaledStepLen = Utility.ScaleRadius(stepLen);
        var scaledSampleRadius = Utility.ScaleRadius(sampleRadius);

        uint v = 0;
        for (var i = 0; i < 32; i++)
        {
            var x = 2 * float.Pi * i / 32;
            var (sin, cos) = float.SinCos(x);

            var last = float.NaN;
            for (var s = scaledMinRadius; s < scaledMaxRadius; s += scaledStepLen)
            {
                var pos = new Vector2(cos, sin) * s + scaledCenter;
                var current = Utility.CalculateMaxB(image, pos, scaledSampleRadius);

                if (!float.IsNaN(last) && print)
                {
                    Console.WriteLine(current - last);
                }
                if (!float.IsNaN(last) && current - last >= thr)
                {
                    v |= 1U << i;
                    break;
                }
                last = current;
            }
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

    private static KeyLevelRot[]? FindFirstSolve(uint[] locks, List<KeyLevelRot>[] keyPositionsArray)
    {
        Span<uint> pg = stackalloc uint[locks.Length];
        Span<int> indexes = stackalloc int[keyPositionsArray.Length];
        locks.CopyTo(pg);
        return FindFirstSolve2(pg, indexes, keyPositionsArray, 0);
    }

    private static KeyLevelRot[]? FindFirstSolve2(Span<uint> pg, Span<int> indexes, List<KeyLevelRot>[] keyPositionsArray, int pos)
    {
        for (var i = 0; i < keyPositionsArray[pos].Count; i++)
        {
            indexes[pos] = i;


            var keyLevelRot = keyPositionsArray[pos][i];

            var rotated = keyLevelRot.Level >= 0 ? uint.RotateLeft(keyLevelRot.InitialShape, keyLevelRot.Rotation) : 0;
            if (keyLevelRot.Level >= 0 && (pg[keyLevelRot.Level] & rotated) != 0)
            {
                //collision
                continue;
            }

            if (keyLevelRot.Level >= 0)
                pg[keyLevelRot.Level] |= rotated;

            if (pos == keyPositionsArray.Length - 1)
            {
                if (Utility.CheckAllBitSet(pg))
                {
                    //this is first solve

                    var result = new KeyLevelRot[keyPositionsArray.Length];
                    for (var j = 0; j < keyPositionsArray.Length; j++)
                    {
                        result[j] = keyPositionsArray[j][indexes[j]];
                    }
                    //we need only the first result
                    return result;
                }
            }
            else
            {
                var result = FindFirstSolve2(pg, indexes, keyPositionsArray, pos + 1);
                if (result is not null)
                    return result;
            }
            if (keyLevelRot.Level >= 0)
                pg[keyLevelRot.Level] &= ~rotated;
        }
        return null;
    }

    public void Dispose()
    {
        try
        {
            runningTask?.Wait();
        }
        catch
        {
        }
    }
}
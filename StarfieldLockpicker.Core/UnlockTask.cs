using System.Collections.Immutable;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace StarfieldLockpicker.Core;

public class UnlockTask
{
    private readonly ICoreInterface core;
    private readonly CancellationToken cancellationToken;

    private UnlockTask(ICoreInterface core, CancellationToken cancellationToken)
    {
        this.core = core;
        this.cancellationToken = cancellationToken;
    }

    public static Task RunAsync(ICoreInterface core, CancellationToken cancellationToken)
    {
        var task = new UnlockTask(core, cancellationToken);
        return task.RunAsync();
    }

    private async Task RunAsync()
    {
        core.ConsoleInfo("Begin");
        var (keys, keySelectionImages, locks) = await CaptureLockAndKeysAsync();

        try
        {
            //to reduce search range, we find every possible position of each key
            core.ConsoleInfo("Captured");
            var keyPositions = FindKeyPositions(keys.AsSpan(), locks.AsSpan());

            //pick one pos for each key, and find the first solve
            var result = FindFirstSolve(locks.AsSpan(), keyPositions);
            if (result is null)
            {
                core.ConsoleWarning("No solve, maybe try again?");
                return;
            }

            core.ConsoleInfo("first solve:");
            foreach (var (_, level, rotation) in result)
            {
                core.ConsoleInfo($"Level: {level}, Rot:{rotation}");
            }

            await Task.Delay(50, cancellationToken);

            //rotate each key to the calculated position
            await RotateKeysAsync(result, keySelectionImages);
            await InsertKeysAsync(result, keySelectionImages);
        }
        finally
        {
            foreach (var image in keySelectionImages)
                image.Dispose();
        }
    }

    private async Task RotateKeysAsync(IReadOnlyList<KeyLevelRot> result, IReadOnlyList<IKeySelectionImage> keySelectionImages)
    {
        for (var keyIndex = 0; keyIndex < result.Count; keyIndex++)
        {
            if (result[keyIndex].Level == -1 || result[keyIndex].Rotation == 0)
                continue;
            await SelectKeyAsync(keySelectionImages, keyIndex);
            await RotateKeyAsync(result[keyIndex]);
        }
    }

    private async Task InsertKeysAsync(IReadOnlyList<KeyLevelRot> solve, IReadOnlyList<IKeySelectionImage> keySelectionImages)
    {
        var currentLevel = solve.Where(t => t.Level >= 0).Min(t => t.Level);
        var remains = solve.Count(t => t.Level == currentLevel);
        var maxLevel = solve.Max(t => t.Level);
        var traveledWithoutInsert = 0;
        bool[] inserted = new bool[solve.Count];
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var (image, selected, mse) = await GetSelectedKeyIndexAndImage(keySelectionImages);
            using (image)
            {
                core.ConsoleDebug($"Selected: {selected}:{solve[selected].Level} mse:{mse:F2} current level: {currentLevel}");

                if (inserted[selected])
                {
                    core.ConsoleError("Selecting key should have been inserted. terminating");
                    throw new TerminatingException();
                }

                if (solve[selected].Level == currentLevel)
                {
                    core.ConsoleDebug($"Inserting: {selected}:{solve[selected].Level}");

                    await InsertKeyAsync(image);
                    remains--;
                    traveledWithoutInsert = 0;
                    inserted[selected] = true;

                    core.ConsoleDebug($"Inserted: {selected}:{solve[selected].Level}");
                }
                else
                {
                    if (traveledWithoutInsert > 2 * solve.Count)
                    {
                        core.ConsoleError($"traveled too many times without inserting({traveledWithoutInsert}). terminating");
                        throw new TerminatingException();
                    }

                    await SkipKeyAsync(image);
                    traveledWithoutInsert++;
                }

                if (remains == 0)
                {
                    core.ConsoleInfo($"Finish Level {currentLevel}");
                    currentLevel++;
                    if (currentLevel > maxLevel)
                        break;
                    remains = solve.Count(t => t.Level == currentLevel);
                    await core.Delay(DelayReason.LayerCompleteAnimation, cancellationToken);
                }
            }
        }
        core.ConsoleInfo($"Finish All Levels");
    }

    private async Task InsertKeyAsync(IKeySelectionImage beforeInsert)
    {
        int maxRetry = 5;
        for (int i = 0; i < maxRetry; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            await core.SendCommandAsync(InputCommand.Insert, cancellationToken);

            core.ConsoleDebug($"insert: command sent, waiting for effect.");
            var (success, mse) = await core.WaitUntil(async () =>
            {
                using var currentImage = await core.CaptureKeyImageAsync(cancellationToken);
                var mse = currentImage.KeyAreaMseWith(beforeInsert);
                return (mse >= core.MseThr, mse);
            }, DelayReason.CommandExecution, cancellationToken);

            core.ConsoleDebug($"insert: command finished, mse: {mse:F2}.");
            if (success)
                return;

            core.ConsoleDebug($"insert: command timeout, retrying {i}/{maxRetry}.");
        }

        core.ConsoleDebug($"insert: fail too many times. terminating.");
        throw new TerminatingException();
    }

    private async Task SkipKeyAsync(IKeySelectionImage beforeInsert)
    {
        int maxRetry = 5;
        for (int i = 0; i < maxRetry; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            await core.SendCommandAsync(InputCommand.Next, cancellationToken);

            core.ConsoleDebug($"skip: command sent, waiting for effect.");
            var (success, mse) = await core.WaitUntil(async () =>
            {
                using var currentImage = await core.CaptureKeyImageAsync(cancellationToken);
                var mse = currentImage.KeyAreaMseWith(beforeInsert);
                return (mse >= core.MseThr, mse);
            }, DelayReason.CommandExecution, cancellationToken);

            core.ConsoleDebug($"skip: command finished, mse: {mse:F2}.");
            if (success)
                return;

            core.ConsoleDebug($"skip: command timeout, retrying {i}/{maxRetry}.");
        }

        core.ConsoleDebug($"skip: fail too many times. terminating.");
        throw new TerminatingException();
    }

    private async Task RotateKeyAsync(KeyLevelRot key)
    {
        var target = uint.RotateLeft(key.InitialShape, key.Rotation);

        //we assume key is never rotated before
        int delta = key.Rotation;

        if (delta == 0)
        {
            core.ConsoleDebug("no need to rotate.");
            return;
        }

        for (var counter = 0; counter < 10; counter++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            core.ConsoleDebug($"begin rotate command, delta = {delta}");

            if (delta > 16)
                await core.RepeatCommandsAsync(InputCommand.RotateAntiClockwise, 32 - delta, cancellationToken);
            else
                await core.RepeatCommandsAsync(InputCommand.RotateClockwise, delta, cancellationToken);

            core.ConsoleDebug("rotate command sent. waiting for 1000ms");
            (var success, delta) = await core.WaitUntil(async () =>
            {
                using var image = await core.CaptureLockImageAsync(cancellationToken);
                var shape = image.GetKeyShape();
                var dr = GetDeltaRotate(shape, target);
                return (dr == 0, dr);
            }, DelayReason.UIRefresh, cancellationToken);

            if (success)
            {
                core.ConsoleDebug("rotate command finished.");
                return;
            }

            if (delta == -1)
            {
                core.ConsoleError("Key shape do not match, what is happening? terminating");
                throw new TerminatingException();
            }
            core.ConsoleWarning($"Warning: key is not rotated to position. retrying {counter}/10.");
        }
        core.ConsoleError("Failed to rotate key to position, too many times.");
        throw new TerminatingException();
    }

    private async Task SelectKeyAsync(IReadOnlyList<IKeySelectionImage> keySelectionImages, int keyIndex)
    {
        var (actualKeyIndex, _) = await GetSelectedKeyIndex(keySelectionImages);
        var delta = keyIndex - actualKeyIndex;

        if (delta == 0)
        {
            core.ConsoleDebug($"Selecting {actualKeyIndex}. no command needed");
            return;
        }

        for (var counter = 0; counter < 5; counter++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            core.ConsoleDebug($"begin select command, current={actualKeyIndex} dest={keyIndex} delta={delta}");

            await core.RepeatCommandsAsync(delta > 0 ? InputCommand.Next : InputCommand.Previous, Math.Abs(delta), cancellationToken);

            core.ConsoleDebug("select command sent, waiting for 1000ms.");

            (_, actualKeyIndex) = await core.WaitUntil(async () =>
            {
                var cur = (await GetSelectedKeyIndex(keySelectionImages)).index;
                return (keyIndex == cur, cur);
            }, DelayReason.CommandExecution, cancellationToken);

            delta = keyIndex - actualKeyIndex;
            if (delta == 0)
            {
                core.ConsoleDebug($"Selected key {keyIndex}");
                return;
            }
            core.ConsoleWarning($"failed to select key {keyIndex}, current={actualKeyIndex}. trying to fix");
        }
        core.ConsoleError($"failed to select key {keyIndex}, too many times.");
        throw new TerminatingException();
    }

    private async Task<(int index, double mse)> GetSelectedKeyIndex(IReadOnlyList<IKeySelectionImage> keySelectionImages)
    {
        var (image, index, mse) = await GetSelectedKeyIndexAndImage(keySelectionImages);
        image.Dispose();
        return (index, mse);
    }

    private async Task<(IKeySelectionImage, int index, double mse)> GetSelectedKeyIndexAndImage(IReadOnlyList<IKeySelectionImage> keySelectionImages)
    {
        var image = await core.CaptureKeyImageAsync(cancellationToken);
        var min = keySelectionImages
            .Select((t, i) => (image: t, index: i, mse: image.KeyAreaMseWith(t)))
            .MinBy(t => t.mse);
        return min with { image = image };
    }

    private static List<KeyLevelRot>[] FindKeyPositions(ReadOnlySpan<uint> keys, ReadOnlySpan<uint> levels)
    {
        var keyPositions = Enumerable.Range(0, keys.Length).Select(_ => new List<KeyLevelRot>()).ToArray();
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

    private async Task<ImmutableArray<IFullImage>> CaptureLockImagesAsync()
    {
        var images = ImmutableArray.CreateBuilder<IFullImage>(16);
        bool ret = false;
        try
        {
            images.Add(await core.CaptureFullScreenAsync(cancellationToken));
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var (image, mse0) = await WaitUntilKeyChangeAsync(images.Last());

                var mse = image.KeyAreaMseWith(images.First());
                core.ConsoleDebug($"image mse: {mse:F2},{mse0:F2}");

                if (mse < core.MseThr)
                {
                    image.Dispose();
                    break;
                }

                if (images.Count > 16)
                {
                    core.ConsoleError("Too many keys! Terminating");
                    throw new TerminatingException();
                }
                images.Add(image);
                core.ConsoleInfo($"captured key {images.Count - 1}");
            }

            ret = true;
            return images.ToImmutable();
        }
        finally
        {
            if (!ret)
            {
                foreach (var image in images)
                    image.Dispose();
                images.Clear();
            }
        }
    }

    private async Task<(IFullImage, double)> WaitUntilKeyChangeAsync(IFullImage compare)
    {
        for (var i = 0; i < 5; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            await core.SendCommandAsync(InputCommand.Next, cancellationToken);
            var (success, (image, mse0)) = await core.WaitUntil(async () =>
            {
                var image = await core.CaptureFullScreenAsync(cancellationToken);
                var mse = image.KeyAreaMseWith(compare);
                var ret = mse >= core.MseThr;
                if (!ret)
                    image.Dispose();
                return ret ? (true, (image, mse)) : (false, (null, mse));
            }, DelayReason.UIRefresh, cancellationToken);

            if (success)
                return (image!, mse0);

            core.ConsoleWarning("image not changed. retrying");
        }
        core.ConsoleError("too many times. terminating");
        throw new TerminatingException();
    }

    private async Task<(ImmutableArray<uint> keys, ImmutableArray<IFullImage> images, ImmutableArray<uint> locks)> CaptureLockAndKeysAsync()
    {
        var images = await CaptureLockImagesAsync();

        var shapeResults = images
            .AsParallel()
            .AsOrdered()
            .Select(image =>
            {
                uint[] ret = new uint[5];

                ret[0] = image.GetKeyShape();
                for (var i = 1; i < ret.Length; i++)
                    ret[i] = image.GetLockShape(i - 1);

                return ret;
            }).ToImmutableArray();

        var keyShapes = shapeResults.Select(t => t[0]).ToImmutableArray();

        var shapeResultGroups = shapeResults
            .GroupBy(t => string.Join(' ', t.Skip(1)))
            .Select(t => (item: t.First(), count: t.Count()))
            .OrderByDescending(t => t.count)
            .ToImmutableArray();

        core.ConsoleInfo("Groups:");
        foreach (var (item, count) in shapeResultGroups)
        {
            core.ConsoleInfo($"Count: {count}");
            for (int i = 1; i < item.Length; i++)
            {
                core.ConsoleInfo($"  Layer{i - 1}: {ShapeToString(item[i])}");
            }
        }

        core.ConsoleInfo("Key shapes:");
        for (int i = 0; i < keyShapes.Length; i++)
        {
            core.ConsoleInfo($"  {i}: {ShapeToString(keyShapes[i])}");
        }

        if (shapeResultGroups.Length > 1)
        {
            core.ConsoleError("Inconsistency detected in lock shapes.");
            throw new TerminatingException();
        }

        var lockShapes = shapeResultGroups.Single().item.Skip(1).ToArray();

        for (var i = 0; i < lockShapes.Length; i++)
        {
            var shape = lockShapes[i];
            if (BitOperations.PopCount(shape) < 10)
            {
                if (i < 2)
                    core.ConsoleWarning($"Warning: less than 10 detect for layer {i}.");
                else if (shape != 0)
                    core.ConsoleWarning($"Warning: less than 10 but not 0 detect for layer {i}.");
            }
            if (shape == 0)
                lockShapes[i] = uint.MaxValue;
        }

        return (keyShapes, images, lockShapes.ToImmutableArray());
    }

    private static string ShapeToString(uint shape)
    {
        Span<char> span = stackalloc char[32];
        for (int i = 0; i < 32; i++)
        {
            span[i] = (char)('0' + ((shape >> i) & 1));
        }
        return span.ToString();
    }

    private static KeyLevelRot[]? FindFirstSolve(ReadOnlySpan<uint> locks, List<KeyLevelRot>[] keyPositionsArray)
    {
        Span<uint> pg = stackalloc uint[locks.Length];
        Span<int> indexes = stackalloc int[keyPositionsArray.Length];
        locks.CopyTo(pg);
        return FindFirstSolve2(pg, indexes, keyPositionsArray, 0);
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
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
                if (CheckAllBitSet(pg))
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

    private static bool CheckAllBitSet(ReadOnlySpan<uint> playground)
    {
        foreach (var item in playground)
        {
            if (item != uint.MaxValue)
                return false;
        }
        return true;
    }

    private static int GetDeltaRotate(uint src, uint dst)
    {
        for (int i = 0; i < 32; i++)
        {
            if (uint.RotateLeft(src, i) == dst)
                return i;
        }
        return -1;
    }
}
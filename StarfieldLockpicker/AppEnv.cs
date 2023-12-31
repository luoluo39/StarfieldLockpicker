﻿using System.Diagnostics;
using StarfieldLockpicker.Core;
using StarfieldLockpicker.Inputs;

namespace StarfieldLockpicker
{
    public class AppEnv : ICoreInterface
    {
        private readonly AppConfig _config;
        private readonly BitmapPool _bitmapPool;

        public AppEnv(AppConfig config, BitmapPool bitmapPool)
        {
            _config = config;
            _bitmapPool = bitmapPool;
        }

        public Task<IFullImage> CaptureFullScreenAsync(CancellationToken cancellationToken)
        {
            var bitmap = _bitmapPool.Rent(_config.ClientRegionOfInterest);
            Utility.CaptureScreenArea(bitmap.Inner.Bitmap, _config.ClientRegionOfInterest);
            return Task.FromResult((IFullImage)bitmap);
        }

        public Task<ILockImage> CaptureLockImageAsync(CancellationToken cancellationToken)
        {
            var bitmap = _bitmapPool.Rent(_config.ClientRegionOfCircle);
            Utility.CaptureScreenArea(bitmap.Inner.Bitmap, _config.ClientRegionOfCircle);
            return Task.FromResult((ILockImage)bitmap);
        }

        public Task<IKeySelectionImage> CaptureKeyImageAsync(CancellationToken cancellationToken)
        {
            var bitmap = _bitmapPool.Rent(_config.ClientRegionOfKeySelection);
            Utility.CaptureScreenArea(bitmap.Inner.Bitmap, _config.ClientRegionOfKeySelection);
            return Task.FromResult((IKeySelectionImage)bitmap);
        }

        private PrecisionTimer _clickTimer;
        public async Task SendCommandAsync(InputCommand command, CancellationToken cancellationToken)
        {
            var key = command switch
            {
                InputCommand.Next => _config.VirtualNext,
                InputCommand.Previous => _config.VirtualPrevious,
                InputCommand.RotateClockwise => _config.VirtualRotateClockwise,
                InputCommand.RotateAntiClockwise => _config.VirtualRotateAntiClockwise,
                InputCommand.Insert => _config.VirtualInsert,
                _ => throw new ArgumentOutOfRangeException(nameof(command), command, null)
            };

            _clickTimer.Wait(cancellationToken);

            await Input.KeyboardKeyClickAsync(key, (int)_config.IntervalForKeyboardClick);

            _clickTimer = new(TimeSpan.FromMilliseconds(_config.IntervalBetweenKeyboardClick));
        }

        public async Task SendCommandsAsync(InputCommand[] commands, CancellationToken cancellationToken)
        {
            foreach (var command in commands)
            {
                await SendCommandAsync(command, cancellationToken);
            }
        }

        public async Task RepeatCommandsAsync(InputCommand command, int times, CancellationToken cancellationToken)
        {
            for (var i = 0; i < times; i++)
            {
                await SendCommandAsync(command, cancellationToken);
            }
        }

        public Task Delay(DelayReason reason, CancellationToken cancellationToken)
        {
            var time = reason switch
            {
                DelayReason.UIRefresh => TimeSpan.FromMilliseconds(_config.IntervalForUIRefresh),
                DelayReason.CommandExecution => TimeSpan.FromMilliseconds(_config.IntervalForCommandExecution),
                DelayReason.LayerCompleteAnimation => TimeSpan.FromMilliseconds(_config.IntervalForLayerCompleteAnimation),
                _ => throw new ArgumentOutOfRangeException(nameof(reason), reason, null)
            };

            return DelayInternal(time, cancellationToken);
        }

        public async Task<bool> WaitUntil(Func<Task<bool>> condition, DelayReason reason, CancellationToken cancellationToken)
        {
            var begin = Stopwatch.GetTimestamp();
            while (true)
            {
                await Delay(reason, cancellationToken);
                if (await condition()) return true;

                if (Stopwatch.GetElapsedTime(begin) > TimeSpan.FromMilliseconds(_config.ResponseWaitTimeout))
                    return false;
            }
        }

        public async Task<(bool, TResult)> WaitUntil<TResult>(Func<Task<(bool, TResult)>> condition, DelayReason reason, CancellationToken cancellationToken)
        {
            var begin = Stopwatch.GetTimestamp();
            while (true)
            {
                await Delay(reason, cancellationToken);
                var (s, r) = await condition();
                if (s) return (s, r);

                if (Stopwatch.GetElapsedTime(begin) > TimeSpan.FromMilliseconds(_config.ResponseWaitTimeout))
                    //Always return last result!
                    return (false, r);
            }
        }

        private Task DelayInternal(TimeSpan time, CancellationToken cancellationToken)
        {
            if (_config.EnablePreciseDelay)
            {
                PrecisionTimer.Wait(time, cancellationToken);
                return Task.CompletedTask;
            }
            return Task.Delay(time, cancellationToken);
        }

        public void ConsoleError(string str)
        {
            if (_config.PrintError)
                Utility.ConsoleError(str);
        }

        public void ConsoleWarning(string str)
        {
            if (_config.PrintWarnings)
                Utility.ConsoleWarning(str);
        }

        public void ConsoleInfo(string str)
        {
            if (_config.PrintInfo)
                Utility.ConsoleInfo(str);
        }

        public void ConsoleDebug(string str)
        {
            if (_config.PrintDebug)
                Utility.ConsoleDebug(str);
        }

        public double MseThr => _config.ImageMseThr;
    }
}

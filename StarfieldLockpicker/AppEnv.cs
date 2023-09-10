using System.Diagnostics;
using StarfieldLockpicker.Core;
using StarfieldLockpicker.Inputs;

namespace StarfieldLockpicker
{
    public class AppEnv : ICoreInterface
    {
        private AppConfig _config;
        private BitmapPool fullSizePool;
        private BitmapPool keyAreaPool;
        private BitmapPool circleAreaPool;

        public AppEnv(AppConfig config)
        {
            _config = config;
            fullSizePool = new BitmapPool(config.TranslateRectangleCeiling(config.RegionOfInterest), config);
            keyAreaPool = new BitmapPool(config.TranslateRectangleCeiling(config.RegionOfKeySelection), config);
            circleAreaPool = new BitmapPool(config.TranslateRectangleCeiling(config.RegionOfCircle), config);
        }

        public void ReleaseUnusedBitmaps()
        {
            fullSizePool.ReleaseAll();
            keyAreaPool.ReleaseAll();
            circleAreaPool.ReleaseAll();
        }

        public Task<IFullImage> CaptureFullScreenAsync(CancellationToken cancellationToken)
        {
            var bitmap = fullSizePool.Rent();
            Utility.CaptureScreenArea(bitmap.Inner.Bitmap, _config.Display, fullSizePool.BitmapRect);
            return Task.FromResult((IFullImage)bitmap);
        }

        public Task<ILockImage> CaptureLockImageAsync(CancellationToken cancellationToken)
        {
            var bitmap = circleAreaPool.Rent();
            Utility.CaptureScreenArea(bitmap.Inner.Bitmap, _config.Display, circleAreaPool.BitmapRect);
            return Task.FromResult((ILockImage)bitmap);
        }

        public Task<IKeySelectionImage> CaptureKeyImageAsync(CancellationToken cancellationToken)
        {
            var bitmap = keyAreaPool.Rent();
            Utility.CaptureScreenArea(bitmap.Inner.Bitmap, _config.Display, keyAreaPool.BitmapRect);
            return Task.FromResult((IKeySelectionImage)bitmap);
        }

        private long _lastClick = 0;
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

            if (Stopwatch.GetElapsedTime(_lastClick).TotalMilliseconds < _config.IntervalBetweenKeyboardClick)
                await Task.Delay((int)_config.IntervalBetweenKeyboardClick, cancellationToken);

            await Input.KeyboardKeyClickAsync(key, (int)_config.IntervalForKeyboardClick);
            _lastClick = Stopwatch.GetTimestamp();
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
            for (int i = 0; i < times; i++)
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

            return Task.Delay(time, cancellationToken);
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

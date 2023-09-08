﻿using System.Diagnostics;
using StarfieldLockpicker.Core;

namespace StarfieldLockpicker
{
    public class AppEnv : ICoreInterface
    {
        private AppConfig _config;
        private BitmapPool pool;

        public AppEnv(AppConfig config)
        {
            _config = config;
            pool = new BitmapPool(new Size(config.ScreenWidth, config.ScreenHeight), config);
        }

        public void ReleaseUnusedBitmaps()
        {
            pool.ReleaseAll();
        }

        public Task<IFullImage> CaptureFullScreenAsync(CancellationToken cancellationToken)
        {
            var bitmap = pool.Rent();
            Utility.CaptureScreen(bitmap.Inner.Bitmap, _config.Display);
            return Task.FromResult((IFullImage)bitmap);
        }

        public Task<ILockImage> CaptureLockImageAsync(CancellationToken cancellationToken)
        {
            var bitmap = pool.Rent();
            Utility.CaptureScreen(bitmap.Inner.Bitmap, _config.Display);
            return Task.FromResult((ILockImage)bitmap);
        }

        public Task<IKeySelectionImage> CaptureKeyImageAsync(CancellationToken cancellationToken)
        {
            var bitmap = pool.Rent();
            Utility.CaptureScreen(bitmap.Inner.Bitmap, _config.Display);
            return Task.FromResult((IKeySelectionImage)bitmap);
        }

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

            Inputs.Input.KeyboardKeyClick(key, 10);
            await Task.Delay(10, cancellationToken);
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
            Utility.ConsoleError(str);
        }

        public void ConsoleWarning(string str)
        {
            Utility.ConsoleWarning(str);
        }

        public void ConsoleInfo(string str)
        {
            Utility.ConsoleInfo(str);
        }

        public void ConsoleDebug(string str)
        {
            Utility.ConsoleDebug(str);
        }

        public double MseThr => AppConfig.Instance.ImageMseThr;
    }
}
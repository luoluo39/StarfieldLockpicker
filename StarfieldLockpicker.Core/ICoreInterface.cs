namespace StarfieldLockpicker.Core;

public interface ICoreInterface
{
    Task<IFullImage> CaptureFullScreenAsync(CancellationToken cancellationToken);
    Task<ILockImage> CaptureLockImageAsync(CancellationToken cancellationToken);
    Task<IKeySelectionImage> CaptureKeyImageAsync(CancellationToken cancellationToken);

    Task SendCommandAsync(InputCommand command, CancellationToken cancellationToken);
    Task SendCommandsAsync(InputCommand[] commands, CancellationToken cancellationToken);
    Task RepeatCommandsAsync(InputCommand command, int times, CancellationToken cancellationToken);
    Task Delay(DelayReason reason, CancellationToken cancellationToken);

    Task<bool> WaitUntil(Func<Task<bool>> condition, DelayReason reason, CancellationToken cancellationToken);
    Task<(bool, TResult)> WaitUntil<TResult>(Func<Task<(bool, TResult)>> condition, DelayReason reason, CancellationToken cancellationToken);

    void ConsoleError(string str);
    void ConsoleWarning(string str);
    void ConsoleInfo(string str);
    void ConsoleDebug(string str);

    double MseThr { get; }
}
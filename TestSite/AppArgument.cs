using StarfieldLockpicker.Core;

public class AppArgument
{
    public int InputDelay = 50;

    public double GetDelayTime(DelayReason reason)
    {
        return reason switch
        {
            DelayReason.UIRefresh => 50,
            DelayReason.CommandExecution => 50,
            DelayReason.LayerCompleteAnimation => 1200,
            _ => throw new ArgumentOutOfRangeException(nameof(reason), reason, null)
        };
    }

    public double GetWaitTimeout(DelayReason reason)
    {
        return reason switch
        {
            DelayReason.UIRefresh => 1000,
            DelayReason.CommandExecution => 1000,
            DelayReason.LayerCompleteAnimation => 3000,
            _ => throw new ArgumentOutOfRangeException(nameof(reason), reason, null)
        };
    }
}
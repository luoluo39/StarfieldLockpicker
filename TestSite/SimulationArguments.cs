public class SimulationArguments
{
    public double MseThr = 45;

    public double ChanceEatKey = 0.1;
    public double ChanceLargeLag = 0;
    public double ChanceSmallLag = 0.4;
    public double ChanceCancellation = 0;

    public double BaseTimeSmallLag = 60;
    public double TimeSmallLag = 120;

    public double BaseTimeLargeLag = 600;
    public double TimeLargeLag = 1200;

    public double BaseReactTime = 20;
    public double TimeReactTime = 20;

    public double MinDelayOffset = 0;
    public double MaxDelayOffset = 20;

    public bool CheckEatKey => Random.Shared.NextDouble() < ChanceEatKey;
    public bool CheckSmallLag => Random.Shared.NextDouble() < ChanceSmallLag;
    public bool CheckLargeLag => Random.Shared.NextDouble() < ChanceLargeLag;
    public bool CheckCancellation => Random.Shared.NextDouble() < ChanceCancellation;

    public double RandomTimeSmallLag => BaseTimeSmallLag + Random.Shared.NextDouble() * TimeSmallLag;
    public double RandomTimeLargeLag => BaseTimeLargeLag + Random.Shared.NextDouble() * TimeLargeLag;
    public double RandomTimeReact => BaseReactTime + Random.Shared.NextDouble() * TimeReactTime;
    public double RandomTimeDelayOffset => Lerp(Random.Shared.NextDouble(), MinDelayOffset, MaxDelayOffset);
    public double RandomMseRotatedKey => (Random.Shared.NextDouble() + 1) * MseThr;
    public double RandomMseDifferentKey => (3 * Random.Shared.NextDouble() + 2) * MseThr;
    public double RandomMseSameImage => Random.Shared.NextDouble() * MseThr;

    private double Lerp(double t, double min, double max)
    {
        return double.FusedMultiplyAdd(t, max - min, min);
    }
}
namespace StarfieldLockpicker.Core;

public interface IKeySelectionImage : IDisposable
{
    double KeyAreaMseWith(IKeySelectionImage other);
}
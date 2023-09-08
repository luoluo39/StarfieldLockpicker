namespace StarfieldLockpicker.Core;

public interface ILockImage : IDisposable
{
    uint GetLockShape(int layer);
    uint GetKeyShape();
}
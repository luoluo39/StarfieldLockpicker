namespace StarfieldLockpicker.Core;

public class TerminatingException : Exception
{
    public TerminatingException()
    {
    }

    public TerminatingException(string message) : base(message)
    {
    }

    public TerminatingException(string message, Exception inner) : base(message, inner)
    {
    }
}
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
using Microsoft.VisualBasic;
using StarfieldLockpicker;

Application.SetHighDpiMode(HighDpiMode.SystemAware);
//no async method usage here because message loop need to run on main thread

//ensure that the config is loaded
try
{
    _ = AppConfig.Instance;
}
catch (Exception e)
{
    Console.WriteLine(e);
    Console.ReadLine();
    return;
}
using var messageWindow = new MessageWindow();
using var cts = new CancellationTokenSource();
using var app = new UnlockApp(cts.Token);

ConsoleExtern.OnClosed += Application.Exit;
Application.ApplicationExit += (_, _) =>
{
    cts.Cancel();
};
ConsoleExtern.Register();

app.Run(messageWindow);
Application.Run(messageWindow);

class ConsoleExtern
{
    public static void Register()
    {
        SetConsoleCtrlHandler(Handler, true);
    }

    // https://learn.microsoft.com/en-us/windows/console/setconsolectrlhandler?WT.mc_id=DT-MVP-5003978
    [DllImport("Kernel32")]
    private static extern bool SetConsoleCtrlHandler(SetConsoleCtrlEventHandler handler, bool add);

    // https://learn.microsoft.com/en-us/windows/console/handlerroutine?WT.mc_id=DT-MVP-5003978
    private delegate bool SetConsoleCtrlEventHandler(CtrlType sig);

    private enum CtrlType
    {
        CTRL_C_EVENT = 0,
        CTRL_BREAK_EVENT = 1,
        CTRL_CLOSE_EVENT = 2,
        CTRL_LOGOFF_EVENT = 5,
        CTRL_SHUTDOWN_EVENT = 6
    }

    public static event Action? OnClosed;

    private static bool Handler(CtrlType signal)
    {
        switch (signal)
        {
            case CtrlType.CTRL_BREAK_EVENT:
            case CtrlType.CTRL_C_EVENT:
            case CtrlType.CTRL_LOGOFF_EVENT:
            case CtrlType.CTRL_SHUTDOWN_EVENT:
            case CtrlType.CTRL_CLOSE_EVENT:
                Console.WriteLine("Closing");
                Trace.WriteLine("Closing");
                OnClosed?.Invoke();
                Application.Exit();
                return false;

            default:
                return false;
        }
    }
}
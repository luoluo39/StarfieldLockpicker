using System.Text.Json;
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

var messageWindow = new MessageWindow();

using var cts = new CancellationTokenSource();
using var app = new UnlockApp(cts.Token);
app.Run(messageWindow);
//hook only works with a message loop
Application.Run(messageWindow);
cts.Cancel();

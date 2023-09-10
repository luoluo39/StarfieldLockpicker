using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using StarfieldLockpicker;
using StarfieldLockpicker.Core;

internal static class Program
{
    private static Task? activeTask;
    private static CancellationTokenSource? activeCts;
    private static object lockHandle = new();

    private static MessageWindow messageWindow;
    private static AppEnv core;

    public static void Main(string[] args)
    {
        Application.SetHighDpiMode(HighDpiMode.SystemAware);
        //no async method usage here because message loop need to run on main thread
        //ensure that the config is loaded
        AppConfig? config;
        try
        {
            if (!AppConfig.TryLoadOrCreateConfig("config.json", out config))
            {
                Console.WriteLine("Failed to load config.");
                Console.ReadLine();
                return;
            }
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            Console.ReadLine();
            return;
        }
        messageWindow = new MessageWindow(config.VirtualHotKey);

        core = new AppEnv(config);
        messageWindow.OnHoyKeyPressed += MessageWindow_OnHoyKeyPressed;

        Application.Run(messageWindow);
    }


    [MethodImpl(MethodImplOptions.Synchronized)]
    static void MessageWindow_OnHoyKeyPressed()
    {
        lock (lockHandle)
        {
            if (null == activeTask)
            {
                activeCts = new CancellationTokenSource();
                activeTask = UnlockTask.RunAsync(core, activeCts.Token);
                activeTask.ContinueWith(task =>
                {
                    lock (lockHandle)
                    {
                        activeTask = null;
                        activeCts.Dispose();
                        activeCts = null;

                        Utility.ConsoleInfo($"task finished with status: {task.Status}");
                        if (task.Exception is not null)
                        {
                            if (task.Exception.InnerExceptions.Count == 1)
                            {
                                var exc = task.Exception.InnerExceptions.Single();
                                switch (exc)
                                {
                                    case TaskCanceledException or OperationCanceledException:
                                        Utility.ConsoleWarning("Task canceled");
                                        break;
                                    case TerminatingException:
                                        Utility.ConsoleWarning("Task terminated due to error");
                                        break;
                                    default:
                                        Utility.ConsoleError($"Task completed with exception:\n{task.Exception}");
                                        break;
                                }
                            }
                            else
                                Utility.ConsoleError($"Task completed with exception:\n{task.Exception}");
                        }

                        core.ReleaseUnusedBitmaps();
                    }
                });
            }
            else
            {
                activeCts?.Cancel();
            }
        }

    }
}
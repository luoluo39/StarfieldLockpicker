using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Windows.Win32;
using Windows.Win32.UI.HiDpi;
using Windows.Win32.UI.Input.KeyboardAndMouse;
using StarfieldLockpicker;
using StarfieldLockpicker.Core;
using Windows.System;
using Windows.Win32.Foundation;
using Windows.Win32.UI.WindowsAndMessaging;

internal static class Program
{
    private static Task? activeTask;
    private static CancellationTokenSource? activeCts;
    private static object lockHandle = new();

    private static MessageWindow messageWindow;
    private static AppEnv? core;
    private static BitmapPool? bitmapPool;

    public static void Main(string[] args)
    {
        PInvoke.SetProcessDpiAwareness(PROCESS_DPI_AWARENESS.PROCESS_PER_MONITOR_DPI_AWARE);
        //no async method usage here because message loop need to run on main thread
        //ensure that the config is loaded
        AppConfig? config;

        if (File.Exists("config.json"))
        {
            if (!AppConfig.TryLoadConfig(File.ReadAllText("config.json"), false, out config))
                return;
        }
        else
        {
            Console.WriteLine("config file does not exists, creating default.");
            using var stream = File.Open("config.json", FileMode.CreateNew);
            config = AppConfig.CreateDefaultConfig(stream, false);
        }

        messageWindow = new MessageWindow(config.VirtualHotKey, config.HotKeyModifier | (uint)HOT_KEY_MODIFIERS.MOD_NOREPEAT);

        messageWindow.OnHoyKeyPressed += MessageWindow_OnHoyKeyPressed;

        messageWindow.Run();

        CancelTask();
    }


    [MethodImpl(MethodImplOptions.Synchronized)]
    static void MessageWindow_OnHoyKeyPressed()
    {
        lock (lockHandle)
        {
            if (null == activeTask)
            {
                BeginTask();
            }
            else
            {
                CancelTask();
            }
        }
    }

    private static void CancelTask()
    {
        activeCts?.Cancel();
    }

    private static void BeginTask()
    {
        //only create config at startup
        if (!File.Exists("config.json") || !AppConfig.TryLoadConfig(File.ReadAllText("config.json"), true, out var config))
        {
            Console.WriteLine("cannot load config");
            return;
        }

        bitmapPool = new(config);
        core = new AppEnv(config, bitmapPool);

        activeCts = new CancellationTokenSource();
        activeTask = UnlockTask.RunAsync(core, activeCts.Token);
        activeTask.ContinueWith(task =>
        {
            lock (lockHandle)
            {
                activeTask = null;
                activeCts.Dispose();
                activeCts = null;

                bitmapPool.ReleaseAll();
                bitmapPool = null;
                core = null;

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
            }
        });
    }
}
﻿using System.Diagnostics;
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
        messageWindow = new MessageWindow();

        core = new AppEnv(AppConfig.Instance);
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
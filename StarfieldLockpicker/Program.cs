﻿using System.Text.Json;
using StarfieldLockpicker;

//no async method usage here because message loop need to run on main thread
const string configPath = "config.json";

AppConfig config;
if (File.Exists(configPath))
{
    var text = File.ReadAllText(configPath);
    var deserialized = JsonSerializer.Deserialize<AppConfig>(text);

    if (deserialized == null)
    {
        Console.WriteLine("failed to load config!");
        return;
    }
    Console.WriteLine("config loaded");
    config = deserialized;
}
else
{
    config = new AppConfig();
    var serialized = JsonSerializer.Serialize(config);
    File.WriteAllText(configPath, serialized);
    Console.WriteLine("no config found, creating default config.");
}

using var cts = new CancellationTokenSource();
using var app = new UnlockApp(config, cts.Token);
app.Run();
//hook only works with a message loop
Utility.MessageLoop(cts.Token);
cts.Cancel();
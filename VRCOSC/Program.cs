﻿// Copyright (c) VolcanicArts. Licensed under the GPL-3.0 License.
// See the LICENSE file in the repository root for full license text.

using System;
using Velopack;
using VRCOSC.App;
using VRCOSC.App.Utils;

namespace VRCOSC;

public static class Program
{
    [STAThread]
    public static void Main()
    {
        VelopackApp.Build().Run();

        AppDomain.CurrentDomain.UnhandledException += (_, e) => Logger.Error((Exception)e.ExceptionObject, "An unhandled error has occured");

        var app = new App.MainApp();
        var mainWindow = new MainWindow();
        app.Run(mainWindow);
    }
}

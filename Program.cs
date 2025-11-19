// ***********************************************************************
// Assembly         : Program
// Author           : InfinityCTI
// Created          : 07-15-2025
//
// Last Modified By : InfinityCTI
// Last Modified On : 07-23-2025
// ***********************************************************************
// <copyright file="Program.cs" company="InfinityCTI, Inc.">
//     Copyright ©  2025
// </copyright>
// <summary></summary>
// ***********************************************************************
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ServiceProcess;
using icti_emc_event_handler.Properties;
using Serilog;
using SimpleServices;

namespace icti_emc_event_handler
{
    [RunInstaller(true)]
    public class Program : SimpleServiceApplication
    {
        [STAThread]
        static void Main(string[] args)
        {
            try
            {
                //Serilog.Debugging.SelfLog.Enable(Console.WriteLine);
                var myOutputTemplate = "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u4}] [THR {ThreadId}] {Message:l}{NewLine}{Exception}";
                Log.Logger = new LoggerConfiguration()
                .Enrich.WithMachineName()
                .Enrich.WithProcessId()
                .Enrich.WithThreadId()
                .Enrich.FromLogContext()
                .Enrich.WithProperty("ApplicationName", General.GetProductName)
                .Enrich.WithProperty("Version", General.GetProductVersion)
                .WriteTo.Console(outputTemplate: myOutputTemplate)
                .WriteTo.File(AppDomain.CurrentDomain.BaseDirectory + $"\\Logs\\ICTI-EMC-EventHandler-.log", 
                fileSizeLimitBytes: Settings.Default.LogFileSizeLimitInMB * 1024 * 1024,                
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: Settings.Default.LogDailyFileCount * Settings.Default.LogNumberDaysRetention,
                rollOnFileSizeLimit: true,
                outputTemplate: myOutputTemplate)
                .CreateLogger();


                AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
                new Service(args,
                        new List<IWindowsService> { new ICTIEMCEventHandlerService() }.ToArray,
                        installationSettings: (serviceInstaller, serviceProcessInstaller) =>
                        {
                            serviceInstaller.ServiceName = "ICTIEMCEventHandler";
                            serviceInstaller.DisplayName = "ICTI EMC Event Handler";
                            serviceInstaller.Description = "This service consumes EMC XML Client API to monitor agent device and hunt groups. This service provides AES CTI events to configured web callbacks";
                            serviceInstaller.StartType = ServiceStartMode.Automatic;
                            serviceProcessInstaller.Account = ServiceAccount.LocalService;

                        },
                        configureContext: x => { x.Log = Log.Information; })
                .Host();
            }
            catch (Exception ex)
            {
                Log.Error("Error in {ex}", ex);
            }
            finally
            {
                // Finally, once just before the application exits...
                Log.CloseAndFlush();
            }
        }

        private static void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            Log.Fatal("Unhandled exception: {exception}", e.ExceptionObject);
        }
    }
}

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Principal;
using System.Threading;
using System.Threading.Tasks;

namespace ControllerService
{
    public class Worker : BackgroundService
    {
        private readonly ILogger<Worker> _logger;

        public Worker(ILogger<Worker> logger)
        {
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            // check for root account (required for starting the FDA service)            
            if (Environment.OSVersion.Platform == PlatformID.Unix)
            {
                if (Environment.UserName != "root")
                {
                    _logger.Log(LogLevel.Warning,"FDAController service is running under the account '" + Environment.UserName + "'. Service must run as root to be able to start the FDA service",new object[] { });
                }
            }

            if (Environment.OSVersion.Platform == PlatformID.Win32NT)
            {
                if (!(new System.Security.Principal.WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator)))
                {
                    _logger.Log(LogLevel.Warning, "Warning: FDAController service not running in administrator mode.The service must run as administrator to be able to start the FDA as an administrator", new object[] { });
                }
            }
            

            // listens for basic services requests (start/stop FDA, etc)
            Globals.BasicServicesServer = new BasicServicesServer(9571,_logger);
            Globals.BasicServicesServer.Start();

            // connects to the FDA on the control port
            Globals.BasicServicesClient = new BasicServicesClient(9572,_logger);
            Globals.BasicServicesClient.Start();

            // connects to the FDA on the operational messages port, receives any messages that the FDA posts, and forwards them to clients on operational messages passthrough port
            Globals.OMPassthrough = new OMPassthough(9573, 9570, _logger);
            Globals.OMPassthrough.Start();

            while (!stoppingToken.IsCancellationRequested)
            {               
                await Task.Delay(1000, stoppingToken);
            }
        }
    }
}

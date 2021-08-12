using System;
using System.Diagnostics;
using System.Security.Principal;
using System.ServiceProcess;

namespace Common
{
    public static class MQTTUtils
    {
        private static readonly string serviceName = "mosquitto";
        private static readonly string exePath = "C:\\IntricateFDA\\MQTT\\mosquitto.exe";
        private static readonly string workingDir = "C:\\IntricateFDA\\MQTT";

        private static string RunCommand(string command, string args, string workingDir = "")
        {
            var processStartInfo = new ProcessStartInfo()
            {
                FileName = command,
                Arguments = args,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };

            if (workingDir != "")
                processStartInfo.WorkingDirectory = workingDir;

            Process process = new() { StartInfo = processStartInfo };

            process.Start();
            string output = process.StandardOutput.ReadToEnd();
            string error = process.StandardError.ReadToEnd();
            process.WaitForExit();

            if (string.IsNullOrEmpty(error)) { return output; }
            else { return error; }
        }

        public static bool ThisProcessIsAdmin()
        {
            if (OperatingSystem.IsWindows())
            {
                return new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator);
            }

            if (OperatingSystem.IsLinux())
            {
                return Environment.UserName == "root";
            }

            return false; // unrecognized OS
        }

        public static bool ServiceInstalled()
        {
            if (OperatingSystem.IsWindows())
            {
                ServiceController[] services = ServiceController.GetServices();
                foreach (ServiceController service in services)
                {
                    if (service.ServiceName == serviceName)
                        return true;
                }
                return false;
            }

            if (OperatingSystem.IsLinux())
            {
                string result = RunCommand("systemctl", "status " + serviceName);

                return !(result.Contains("could not be found") || result.Contains(".service is masked")); // 'could not be found' = never installed, '.service is masked' = previously installed, but has been uninstalled
            }

            return false;
        }

        public static string GetMQTTStartMode()
        {
            string starttype = "unknown start type (service not found)";
            // check if the service is set to run automatically on startup
            if (OperatingSystem.IsWindows())
            {
                ServiceController sc = new() { ServiceName = serviceName };

                try
                {
                    starttype = sc.StartType.ToString();
                }
                catch { }
            }

            if (OperatingSystem.IsLinux())
            {
                string result = RunCommand("systemctl", "status " + serviceName);

                if (result.Contains(".service; enabled"))
                {
                    return "Automatic";
                }
                else
                    return "Disabled";
            }

            return starttype;
        }

        public static string InstallMosquitto()
        {
            string error = "";
            if (OperatingSystem.IsWindows())
            {
                error = RunMosquittoExecutable("install");
                if (error != String.Empty)
                    error = "Error while installing Mosquitto: " + error;

                return error;
            }

            if (OperatingSystem.IsLinux())
            {
                // install mosquitto
                RunCommand("wget", "http://repo.mosquitto.org/debian/mosquitto-repo.gpg.key");
                RunCommand("apt-key", "add mosquitto-repo.gpg.key");
                RunCommand("wget", "http://repo.mosquitto.org/debian/mosquitto-buster.list", "/etc/apt/sources.list.d/");
                RunCommand("apt-get", "update");
                RunCommand("apt-get", "-y install mosquitto");

                // copy configuration files
                RunCommand("cp", "users.txt /etc/mosquitto/users.txt");
                RunCommand("cp", "acl.txt /etc/mosquitto/acl.txt");
                RunCommand("cp", "mosquitto.conf /etc/mosquitto/mosquitto.conf");

                // just in case the service exists from a previous installation but is masked
                RunCommand("systemctl", "unmask " + serviceName);

                // restart the service (load the configuration)
                RunCommand("systemctl", "restart " + serviceName);
            }

            return error;
        }

        public static string UnInstallMosquitto()
        {
            string error = "";
            if (OperatingSystem.IsWindows())
            {
                error = RunMosquittoExecutable("uninstall");
                if (error != String.Empty)
                    error = "Error while un-installing Mosquitto: " + error;
            }

            if (OperatingSystem.IsLinux())
            {
                error = RunCommand("apt-get", "-y remove mosquitto");
            }

            return error;
        }

        public static bool EnvVarExists(out string value)
        {
            value = null;
            string mosquittoPath = Environment.GetEnvironmentVariable("MOSQUITTO_DIR", EnvironmentVariableTarget.Machine);
            if (mosquittoPath != null)
            {
                value = mosquittoPath;
                return true;
            }
            else
                return false;
        }

        public static bool MQTTPathCorrect()
        {
            string mosquittoPath = Environment.GetEnvironmentVariable("MOSQUITTO_DIR", EnvironmentVariableTarget.Machine);
            return (mosquittoPath == workingDir);
        }

        public static string SetMQTTPath()
        {
            try
            {
                Environment.SetEnvironmentVariable("MOSQUITTO_DIR", workingDir, EnvironmentVariableTarget.Machine);
            }
            catch (Exception ex)
            {
                return ex.Message;
            }

            return string.Empty;
        }

        public static string GetMQTTStatus()
        {
            string status = "unknown";
            if (OperatingSystem.IsWindows())
            {
                ServiceController sc = new() { ServiceName = serviceName };
                try
                {
                    status = sc.Status.ToString();
                }
                catch { }
            }

            if (OperatingSystem.IsLinux())
            {
                string result = RunCommand("systemctl", "status " + serviceName);
                if (result.Contains("active (running)"))
                    status = "Running";
                else
                    status = "Stopped";
            }

            return status;
        }

        public static string StopMosquittoService()
        {
            if (OperatingSystem.IsWindows())
            {
                ServiceController sc = new() { ServiceName = serviceName };
                try
                {
                    sc.Stop();
                }
                catch (Exception ex)
                {
                    return ex.Message;
                }

                sc.WaitForStatus(ServiceControllerStatus.Stopped, new TimeSpan(0, 0, 5)); // 5 second timeout for service to stop
                if (sc.Status != ServiceControllerStatus.Stopped)
                    return "service failed to stop";
                return string.Empty;
            }

            if (OperatingSystem.IsLinux())
            {
                string result = RunCommand("systemctl", "stop " + serviceName);
                return result;
            }

            return "unsupported platform"; // should never see this;
        }

        public static string StartMosquittoService()
        {
            if (OperatingSystem.IsWindows())
            {
                ServiceController sc = new() { ServiceName = serviceName };

                try
                {
                    sc.Start();
                }
                catch (Exception ex)
                {
                    return ex.Message;
                }

                sc.WaitForStatus(ServiceControllerStatus.Running, new TimeSpan(0, 0, 5)); // 5 second timeout for service to start
                if (sc.Status != ServiceControllerStatus.Running)
                    return "service was started, but it failed to initialize";
                return string.Empty;
            }

            if (OperatingSystem.IsLinux())
            {
                string result = RunCommand("systemctl", "start " + serviceName);
                return result;
            }

            return "unsupported platform"; // should never see this;
        }

        public static string RepairMQTT()
        {
            Console.WriteLine("Starting MQTT Repair");

            // utility run as admin?
            if (!MQTTUtils.ThisProcessIsAdmin())
            {
                return "Unable to repair MQTT, please run the FDA as Administrator/Superuser";
            }

            // service exists?
            bool registrationAttempted = false;
            bool serviceExists;

        RecheckServiceExists:
            serviceExists = MQTTUtils.ServiceInstalled();
            string error = "";
            if (!serviceExists)
            {
                if (!registrationAttempted)
                {
                    // try registering the service (using the mosquitto.exe install option)
                    registrationAttempted = true;
                    Console.WriteLine("Installing mosquitto");
                    MQTTUtils.InstallMosquitto();
                    goto RecheckServiceExists;
                }
            }
            if (!serviceExists)
                return "Service not installed, and an attempt to installed it failed: " + error;

            // environment variable is windows only
            if (OperatingSystem.IsWindows())
            {
                // check that the required environment variable exists
                bool repaired = false;
            RecheckVar:
                if (!EnvVarExists(out _))
                {
                    if (!repaired)
                    {
                        SetMQTTPath();
                        repaired = true;
                        goto RecheckVar;
                    }
                    else
                    {
                        return "Failed to create the MOSQUITTO_DIR environment variable";
                    }
                }

                // checking that MOSQUITTO_DIR is set to the correct path
                repaired = false;
            RecheckPath:
                if (!MQTTPathCorrect())
                {
                    if (!repaired)
                    {
                        SetMQTTPath();
                        repaired = true;
                        goto RecheckPath;
                    }
                    else
                    {
                        return "Failed to set the MOSQUITTO_DIR environment variable";
                    }
                }
            }

            // service is set to run automatically?
            string result = "";
            if (GetMQTTStartMode() != "Automatic")
            {
                if (OperatingSystem.IsWindows())
                {
                    // no way to just change the start mode, uninstall it and re-install it and the intaller will set it to automatic
                    UnInstallMosquitto();
                    result = InstallMosquitto();
                }

                if (OperatingSystem.IsLinux())
                {
                    // enable the service
                    Console.WriteLine("Enabling the service");
                    RunCommand("systemctl", "unmask " + serviceName); // just in case
                    RunCommand("systemctl", "enable " + serviceName);
                }

                if (result != "")
                    return "Error while changing MQTT service start mode to automatic: " + result;
            }

            // service is running
            if (GetMQTTStatus() != "Running")
            {
                result = StartMosquittoService();
                if (result != "")
                    return "Error while attempting to start the MQTT service: " + result;
            }

            return "";
        }

        private static string RunMosquittoExecutable(string argument)
        {
            string error = "";
            Process process = new();
            ProcessStartInfo startInfo = new() { WindowStyle = ProcessWindowStyle.Normal };

            //string path = System.IO.Path.GetDirectoryName(Assembly.GetEntryAssembly().Location) + exePath;

            startInfo.FileName = exePath;
            startInfo.Arguments = argument;

            process.StartInfo = startInfo;
            try
            {
                process.Start();
            }
            catch (Exception ex)
            {
                error = ex.Message;
            }

            return error;
        }
    }
}
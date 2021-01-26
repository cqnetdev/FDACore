using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Security.Principal;
using System.ServiceProcess;
using System.Text;
using System.Threading.Tasks;

namespace Common
{
    public static class MQTTUtils
    {
        private static string serviceName = "mosquitto";
        private static string exeRelativePath = "\\MQTT\\mosquitto.exe";
        private static string MQTTFolderRelativePath = "\\MQTT";


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

            var process = new Process();
            process.StartInfo = processStartInfo;


            process.Start();
            string output = process.StandardOutput.ReadToEnd();
            string error = process.StandardError.ReadToEnd();
            process.WaitForExit();

            if (string.IsNullOrEmpty(error)) { return output; }
            else { return error; }
        }


        public static bool ThisProcessIsAdmin()
        {

            if (Environment.OSVersion.Platform == PlatformID.Win32NT)
            {
                return new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator);
            }
            
            if (Environment.OSVersion.Platform == PlatformID.Unix)
            {
                return Environment.UserName == "root";
            }

            return false;
        }


        public static bool ServiceInstalled()
        {
            if (Environment.OSVersion.Platform == PlatformID.Win32NT)
            {
                return ServiceController.GetServices().Any(serviceController => serviceController.ServiceName.Equals(serviceName));
            }

            if (Environment.OSVersion.Platform == PlatformID.Unix)
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
            if (Environment.OSVersion.Platform == PlatformID.Win32NT)
            {
                ServiceController sc = new ServiceController();
                sc.ServiceName = serviceName;              
                try
                {
                    starttype = sc.StartType.ToString();
                }
                catch { }
            }

            if (Environment.OSVersion.Platform == PlatformID.Unix)
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
            if (Environment.OSVersion.Platform == PlatformID.Win32NT)
            {
                error = RunMosquittoExecutable("install");
                if (error != String.Empty)
                    error = "Error while installing Mosquitto: " + error;

                return error;
            }

            if (Environment.OSVersion.Platform == PlatformID.Unix)
            {
                // install mosquitto
                string result = "";
                result = RunCommand("wget", "http://repo.mosquitto.org/debian/mosquitto-repo.gpg.key") + Environment.NewLine;
                result = RunCommand("apt-key", "add mosquitto-repo.gpg.key") + Environment.NewLine;
                result = RunCommand("wget","http://repo.mosquitto.org/debian/mosquitto-buster.list", "/etc/apt/sources.list.d/");              
                result = RunCommand("apt-get", "update");
                result =  RunCommand("apt-get", "-y install mosquitto");

                // copy configuration files
                result =  RunCommand("cp", "users.txt /etc/mosquitto/users.txt");
                result = RunCommand("cp", "acl.txt /etc/mosquitto/acl.txt");
                result = RunCommand("cp", "mosquitto.conf /etc/mosquitto/mosquitto.conf");

                // just in case the service exists from a previous installation but is masked
                result = RunCommand("systemctl", "unmask " + serviceName); 

                // restart the service (load the configuration)
                result = RunCommand("systemctl", "restart " + serviceName);
            }

            return error;
        }


        public static string UnInstallMosquitto()
        {
            string error = "";
            if (Environment.OSVersion.Platform == PlatformID.Win32NT)
            {
                error = RunMosquittoExecutable("uninstall");
                if (error != String.Empty)
                    error = "Error while un-installing Mosquitto: " + error;

            }

            if (Environment.OSVersion.Platform == PlatformID.Unix)
            {
               error = RunCommand("apt-get", "-y remove mosquitto");
            }

            return error;
        }
        

        public static bool EnvVarExists(out string value)
        {
            value = null;
            string mosquittoPath = Environment.GetEnvironmentVariable("MOSQUITTO_DIR",EnvironmentVariableTarget.Machine);
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
            string correctPath = System.IO.Path.GetDirectoryName(Assembly.GetEntryAssembly().Location) + MQTTFolderRelativePath;
            string mosquittoPath = Environment.GetEnvironmentVariable("MOSQUITTO_DIR",EnvironmentVariableTarget.Machine);
            return (mosquittoPath == correctPath);
        }

        public static string SetMQTTPath()
        {

            try
            {
                string correctPath = System.IO.Path.GetDirectoryName(Assembly.GetEntryAssembly().Location) + MQTTFolderRelativePath;
                Environment.SetEnvironmentVariable("MOSQUITTO_DIR", correctPath,EnvironmentVariableTarget.Machine);
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
            if (Environment.OSVersion.Platform == PlatformID.Win32NT)
            {
                ServiceController sc = new ServiceController();
                sc.ServiceName = serviceName;

                try
                {
                    status = sc.Status.ToString();
                }
                catch { }

             
            }


            if (Environment.OSVersion.Platform == PlatformID.Unix)
            {
                string result = RunCommand("systemctl", "status " + serviceName);
                if (result.Contains("active (running)"))
                    status = "Running";
                else
                    status = "Stopped";
            }


            return status;

        }



        public static string StartMosquittoService()
        {
            if (Environment.OSVersion.Platform == PlatformID.Win32NT)
            {
                ServiceController sc = new ServiceController();
                sc.ServiceName = serviceName;
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

            if (Environment.OSVersion.Platform == PlatformID.Unix)
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
            bool serviceExists = false;

            RecheckServiceExists:
            serviceExists = MQTTUtils.ServiceInstalled();
            string error = "";
            if (!serviceExists)
            {
                if (!registrationAttempted)
                {
                    // try registering it
                    registrationAttempted = true;
                    Console.WriteLine("Installing mosquitto");
                    error = MQTTUtils.InstallMosquitto();
                    goto RecheckServiceExists;
                }
            }
            if (!serviceExists)
                return "Service not installed, and an attempt to installed it failed: " + error;

            // environment variable is windows only
            if (Environment.OSVersion.Platform == PlatformID.Win32NT)
            {
                // check that the required environment variable exists
                string path;
                bool repaired = false;

                RecheckVar:
                if (!MQTTUtils.EnvVarExists(out path))
                {
                    if (!repaired)
                    {
                        MQTTUtils.SetMQTTPath();
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
                if (!MQTTUtils.MQTTPathCorrect())
                {
                    if (!repaired)
                    {
                        MQTTUtils.SetMQTTPath();
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
            if (MQTTUtils.GetMQTTStartMode() != "Automatic")
            {
                if (Environment.OSVersion.Platform == PlatformID.Win32NT)
                {
                    // no way to just change the start mode, uninstall it and re-install it and the intaller will set it to automatic
                    MQTTUtils.UnInstallMosquitto();
                    result = MQTTUtils.InstallMosquitto();
                }

                if (Environment.OSVersion.Platform == PlatformID.Unix)
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
            if (MQTTUtils.GetMQTTStatus() != "Running")
            {
                result = MQTTUtils.StartMosquittoService();
                if (result != "")
                    return "Error while attempting to start the MQTT service: " + result;
            }

            return "";
           
        }


        private static string RunMosquittoExecutable(string argument)
        {
            string error = "";
            System.Diagnostics.Process process = new System.Diagnostics.Process();
            System.Diagnostics.ProcessStartInfo startInfo = new System.Diagnostics.ProcessStartInfo();
            startInfo.WindowStyle = System.Diagnostics.ProcessWindowStyle.Normal;
            string path = System.IO.Path.GetDirectoryName(Assembly.GetEntryAssembly().Location) + exeRelativePath;

            startInfo.FileName = path;
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

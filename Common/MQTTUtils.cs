using System;
using System.Collections.Generic;
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

        /* not .NET Core compatible 
        public static bool ThisProcessIsAdmin()
        {
            return new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator);
        }
        

        public static bool ServiceInstalled()
        {
            return ServiceController.GetServices().Any(serviceController => serviceController.ServiceName.Equals(serviceName));
        }

        public static string GetMQTTStartMode()
        {
            // check if the service is set to run automatically on startup
            ServiceController sc = new ServiceController();
            sc.ServiceName = serviceName;
            string starttype = "unknown start type (service not found)";
            try
            {
                starttype = sc.StartType.ToString();
            }
            catch { }

            return starttype;
        }

        public static string InstallMosquitto()
        {
            string error = RunMosquittoExecutable("install");
            if (error != String.Empty)
                error = "Error while installing Mosquitto: " + error;

            return error;
        }


        public static string UnInstallMosquitto()
        {
            string error = RunMosquittoExecutable("uninstall");
            if (error != String.Empty)
                error = "Error while un-installing Mosquitto: " + error;

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
            ServiceController sc = new ServiceController();
            sc.ServiceName = serviceName;
            string status = "unknown";
            try
            {
                status = sc.Status.ToString();
            }
            catch { }

            return status;
        }



        public static string StartMosquittoService()
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



        public static string RepairMQTT()
        {
            // utility run as admin?
            if (!MQTTUtils.ThisProcessIsAdmin())
            {
                return "Unable to repair MQTT, please run in Administrator mode";
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
                    error = MQTTUtils.InstallMosquitto();
                    goto RecheckServiceExists;
                }
            }
            if (!serviceExists)
                return "Service not installed, and an attempt to installed it failed: " + error;


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

            // service is set to run automatically?
            string result;
            if (MQTTUtils.GetMQTTStartMode() != "Automatic")
            {
                // no way to just change the start mode, uninstall it and re-install it and the intaller will set it to automatic
                MQTTUtils.UnInstallMosquitto();
                result = MQTTUtils.InstallMosquitto();

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

        */

    }
}

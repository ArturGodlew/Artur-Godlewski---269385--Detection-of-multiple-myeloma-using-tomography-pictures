using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.IO;

namespace FinalProject
{
    public static class PythonProcessHelper
    {
        public static Process GetProcess(string pathToScript, string args, object sender)
        {
            pathToScript = Path.GetFullPath(pathToScript);
            
            if(!File.Exists(pathToScript))
            {
                var window = new ErrorWindow("*ScriptMissing*", sender);
                window.ShowDialog();
                return null;
            }

            string pythonPath = Path.GetFullPath(ConfigurationManager.AppSettings["pythonPath"]);
            
            if (!File.Exists(pythonPath))
            {
                var window = new ErrorWindow("*PythonMissing*", sender);
                window.ShowDialog();
                return null;
            }

            var cmd = $"-u \"{pathToScript}\"";
            var pyProcess = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = pythonPath,
                    Arguments = string.Format("{0} {1}", cmd, args),
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardInput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                }
            };

            try
            {
                pyProcess.Start();
            }
            catch (Exception e)
            {
                var window = new ErrorWindow(e.Message, null);
                window.ShowDialog();
                App.ParentWindowRef.ParentFrame.Navigate(window.TargetPage);
                return null;
            }
            return pyProcess;
        }

        public static bool ReadErrors(Process pyProcess, object sender)
        {
            string outputReceived = pyProcess.StandardError.ReadToEnd();
            while ((outputReceived = pyProcess.StandardError.ReadLine()) != null)
            {
                var window = new ErrorWindow(outputReceived, sender);
                window.ShowDialog();
                App.ParentWindowRef.ParentFrame.Navigate(window.TargetPage);
                return true;
            }
            return false;
        }
    }
}

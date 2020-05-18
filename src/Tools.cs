using System;
using System.Diagnostics; 
using System.IO;
using System.Threading;
using Mistware.Utils;

namespace DBTools
{
    /// Class to zip and unzip backups
    public static class Tools
    {
        /// Location of 7za executable
        public static string Path { get; set; }

        private static string Zipper
        {
            get
            {
                string zipper = (PathDelimiter == "\\") ? "7za.exe" : "7za";
                if (Path != null)
                {
                     zipper = Path + PathDelimiter + zipper;                
                    if (!File.Exists(zipper)) throw new Exception($"Cannot find {zipper} in Tools.Zipper");
                } 
                return zipper;
            }
        }

        /// <summary>
        /// Returns true if the Operating System family is Windows.
        /// </summary>
        public static string PathDelimiter
        {
            get 
            {
                return System.IO.Path.DirectorySeparatorChar.ToString(); 
            }
        }

        /// Runs: 7za a archive filename
        public static string Zip(string archive, string filename)
        {
            return ExecExternal(Zipper, "a " + archive + " " + filename, false);
        }

        /// Runs: 7za x archive -o repository 
        public static string UnZip(string archive, string outfolder)
        {
            return ExecExternal(Zipper, "x " + archive + " -o" + outfolder + " -y", false);
        }

        /// Wraps Thread.Sleep()
        public static void Sleep(int duration)
        {
            Thread.Sleep(duration); 
        }

        private static string ExecExternal(string command, string arguments, bool useShell)
        {
            string output = "";

            try
            {
                // Start the child process.
                Process p = new Process();
                if (useShell)
                {
                    p.StartInfo.UseShellExecute = true;
                    p.StartInfo.RedirectStandardOutput = false;
                    p.StartInfo.CreateNoWindow = false;
                }
                else
                {
                    // Redirect the output stream of the child process.
                    p.StartInfo.UseShellExecute = false;
                    p.StartInfo.RedirectStandardOutput = true;
                    p.StartInfo.CreateNoWindow = true;
                }

                p.StartInfo.FileName = command;
                p.StartInfo.Arguments = arguments;
                p.Start();
                if (!useShell)
                {
                    // Do not wait for the child process to exit before
                    // reading to the end of its redirected stream.
                    // Read the output stream first and then wait.
                    output = p.StandardOutput.ReadToEnd();
                }
                // p.WaitForExit();
                p.WaitForExit();
                p.Dispose();
            }
            catch (Exception err)
            {
                output = err.Message;
            }

            return output;
        }
    }
}

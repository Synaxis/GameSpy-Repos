using System;
using System.Diagnostics;
using System.IO;
using System.Threading;

namespace Server
{
    /// <summary>
    /// A simple object to handle exceptions thrown during runtime
    /// </summary>
    public static class ExceptionHandler
    {
        /// <summary>
        /// The filepath
        /// </summary>
        public static string FileName { get; } = Path.Combine(Program.RootPath, "Logs", "LoginExceptions.log");

        /// <summary>
        /// For locking while writing
        /// </summary>
        private static Object _sync = new Object();

        /// <summary>
        /// Handles an exception on the main thread.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="t"></param>
        public static void OnThreadException(object sender, ThreadExceptionEventArgs t)
        {
            // Create Trace Log
            GenerateExceptionLog(t.Exception);
        }

        /// <summary>
        /// Handles cross thread exceptions, that are unrecoverable
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        public static void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            // Create Trace Log
            GenerateExceptionLog(e.ExceptionObject as Exception);
        }

        /// <summary>
        /// Generates a trace log for an exception. If an exception is thrown here, The error
        /// will automatically be logged in the programs error log
        /// </summary>
        /// <param name="E">The exception to log</param>
        public static void GenerateExceptionLog(Exception E)
        {
            // Try to write to the log
            try
            {
                // Create a lock, allowing 1 thread to enter at a time
                lock (_sync)
                {
                    // Open the file
                    using (FileStream stream = File.Open(FileName, FileMode.Create, FileAccess.Write, FileShare.Read))
                    using (StreamWriter log = new StreamWriter(stream))
                    {
                        // set the pointer to the end of the file
                        log.BaseStream.Seek(0, SeekOrigin.End);

                        // Write the header data
                        log.WriteLine($"-------- Exception [{DateTime.Now.ToString()}]--------");

                        // Log each inner exception
                        int i = 0;
                        while (true)
                        {
                            // Create a stack trace
                            StackTrace trace = new StackTrace(E, true);
                            StackFrame frame = trace.GetFrame(0);

                            // Log the current exception
                            log.WriteLine("Type: " + E.GetType().FullName);
                            log.WriteLine("Message: " + E.Message.Replace("\n", "\n\t"));
                            log.WriteLine("Target Method: " + frame.GetMethod().Name);
                            log.WriteLine("File: " + frame.GetFileName());
                            log.WriteLine("Line: " + frame.GetFileLineNumber());
                            log.WriteLine("StackTrace:");
                            log.WriteLine(E.StackTrace.TrimEnd());

                            // If we have no more inner exceptions, end the logging
                            if (E.InnerException == null)
                                break;

                            // Prepare next inner exception data
                            log.WriteLine();
                            log.WriteLine("-------- Inner Exception ({0}) --------", i++);
                            E = E.InnerException;
                        }

                        // New line
                        log.WriteLine();
                    }
                }
            }
            catch (Exception Ex)
            {
                Program.ErrorLog.Write("FATAL: Unable to write tracelog!!! : " + Ex.ToString());
            }
        }
    }
}

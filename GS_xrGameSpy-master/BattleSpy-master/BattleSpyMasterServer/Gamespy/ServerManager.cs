using System;
using System.IO;
using System.Net;
using BattleSpy.Logging;

namespace BattlelogMaster
{
    /// <summary>
    /// The ServerManager is used as a warpper that controls all of the Gamespy Server
    /// </summary>
    public static class ServerManager
    {
        /// <summary>
        /// Returns whether the login server is running or not
        /// </summary>
        private static bool isRunning = false;

        /// <summary>
        /// Returns whether the login server is running or not
        /// </summary>
        public static bool IsRunning
        {
            get { return isRunning; }
        }

        /// <summary>
        /// The Gamespy Master Server
        /// </summary>
        private static MasterServer MstrServer;

        /// <summary>
        /// Our CD key server
        /// </summary>
        private static CDKeyServer CDKeyServer;

        /// <summary>
        /// The Login Server Log Writter
        /// </summary>
        private static LogWriter Logger;

        static ServerManager()
        {
            // Create our log file, and register for events
            Logger = new LogWriter(Path.Combine(Program.RootPath, "Logs", "LoginServer.log"), true);
        }

        /// <summary>
        /// Starts the Login Server listeners, and begins accepting new connections
        /// </summary>
        public static void StartServers()
        {
            // Make sure we arent already running!
            if (isRunning) return;
            try 
            {
                // Create debug log
                LogWriter DebugLog = new LogWriter(Path.Combine(Program.RootPath, "Logs", "MasterServer_Debug.log"), true, 1);

                // Start the DB Connection
                Console.Write("Connecting to Mysql... ");
                using (MasterDatabase Database = new MasterDatabase())
                {
                    Console.Write("Success!" + Environment.NewLine);
                }

                // Create our end point to bind to
                int port = Config.GetType<int>("Settings", "MasterServerUdpPort");
                IPAddress address = IPAddress.Parse(Config.GetValue("Settings", "ServerBindIp"));

                // Start Master Server, we write to console in the constructor because this
                // is actually 2 servers in 1
                Console.Write("<MSTR> Binding to UDP port {0}... ", port);
                MstrServer = new MasterServer(new IPEndPoint(address, port), DebugLog);
                Console.Write("Success!" + Environment.NewLine);

                // Start the CDKey server
                port = Config.GetType<int>("Settings", "CDKeyServerUdpPort");
                Console.Write("<CDKY> Binding to UDP port {0}... ", port);
                CDKeyServer = new CDKeyServer(new IPEndPoint(address, port), DebugLog);
                Console.Write("Success!" + Environment.NewLine);
            }
            catch
            {
                Console.Write("Failed!" + Environment.NewLine);
                throw;
            }

            // Let the client know we are ready for connections
            isRunning = true;
        }

        /// <summary>
        /// Shutsdown the Login Server listeners and stops accepting new connections
        /// </summary>
        public static void Shutdown()
        {
            // Shutdown Login Servers
            MstrServer.Shutdown();
            CDKeyServer.Shutdown(); 

            // Update status
            isRunning = false;
        }

        /// <summary>
        /// This method is used to store a message in the console.log file
        /// </summary>
        /// <param name="message">The message to be written to the log file</param>
        public static void Log(string message)
        {
            Logger.Write(message);
        }

        /// <summary>
        /// This method is used to store a message in the console.log file
        /// </summary>
        /// <param name="message">The message to be written to the log file</param>
        public static void Log(string message, params object[] items)
        {
            Logger.Write(String.Format(message, items));
        }
    }
}

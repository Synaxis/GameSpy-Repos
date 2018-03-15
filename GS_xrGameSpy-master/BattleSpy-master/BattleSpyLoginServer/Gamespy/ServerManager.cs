using System;
using System.IO;
using System.Net;
using BattleSpy.Logging;
using Server.Database;

namespace Server
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
        /// Gamespy GpCm Server Object
        /// </summary>
        private static GpcmServer CmServer;

        /// <summary>
        /// The Gamespy GpSp Server Object
        /// </summary>
        private static GpspServer SpServer;

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
                // Start the DB Connection
                Console.Write("Connecting to Mysql... ");
                using (GamespyDatabase Database = new GamespyDatabase())
                {
                    Console.Write("Success!" + Environment.NewLine);

                    // Reset game sessions
                    if (Config.GetType<bool>("Settings", "ResetGameSessionsOnStartup"))
                    {
                        Console.Write("Resetting all game sessions... ");
                        Database.Execute("UPDATE player SET online=0 WHERE id > 0");
                        Console.Write("Success!" + Environment.NewLine);
                    }
                }

                // Create our end point to bind to
                int port = Config.GetType<int>("Settings", "LoginServerPort");
                IPAddress address = IPAddress.Parse(Config.GetValue("Settings", "ServerBindIp"));

                // Start the Client Manager Server
                Console.Write("<GPCM> Binding to TCP port {0}... ", port);
                CmServer = new GpcmServer(new IPEndPoint(address, port));
                Console.Write("Success!" + Environment.NewLine);

                // Start Search Provider Server
                Console.Write("<GPSP> Binding to TCP port {0}... ", ++port);
                SpServer = new GpspServer(new IPEndPoint(address, port));
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
            CmServer.Shutdown();
            SpServer.Shutdown();

            // Update status
            isRunning = false;
        }

        /// <summary>
        /// Forces the logout of a connected client
        /// </summary>
        /// <param name="Pid"></param>
        /// <returns></returns>
        public static bool ForceLogout(int Pid)
        {
            return (IsRunning) ? CmServer.ForceLogout(Pid) : false;
        }

        /// <summary>
        /// Returns the number of active connections
        /// </summary>
        /// <returns></returns>
        public static int NumConnections()
        {
            return CmServer.NumClients;
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

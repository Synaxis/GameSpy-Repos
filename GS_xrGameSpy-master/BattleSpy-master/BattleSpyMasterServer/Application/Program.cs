using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using BattleSpy.Logging;

namespace BattlelogMaster
{
    public static class Program
    {
        /// <summary>
        /// When set to false, program will close
        /// </summary>
        public static bool IsRunning = true;

        /// <summary>
        /// The rootpath of the program
        /// </summary>
        public static readonly string RootPath = Application.StartupPath;

        /// <summary>
        /// The server version
        /// </summary>
        public static readonly Version Version = Version.Parse(Application.ProductVersion);

        /// <summary>
        /// The error log writter for this application
        /// </summary>
        public static LogWriter ErrorLog;

        /// <summary>
        /// Program Entry Point
        /// </summary>
        /// <param name="args"></param>
        static void Main(string[] args)
        {
            // Create our version string
            string version = String.Concat(Version.Major, ".", Version.Minor, ".", Version.Build);

            // Setup the console
            Console.Title = $"Battlefield2 Statistics Master Gamespy Emulator v{version}";
            Console.WriteLine(@"__________         __    __  .__           _________             ");
            Console.WriteLine(@"\______   \_____ _/  |__/  |_|  |   ____  /   _____/_____ ___.__.");
            Console.WriteLine(@" |    |  _/\__  \\   __\   __\  | _/ __ \ \_____  \\____ <   |  |");
            Console.WriteLine(@" |    |   \ / __ \|  |  |  | |  |_\  ___/ /        \  |_> >___  |");
            Console.WriteLine(@" |______  /(____  /__|  |__| |____/\___  >_______  /   __// ____|");
            Console.WriteLine(@"        \/      \/                     \/        \/|__|   \/     ");
            Console.WriteLine();
            Console.WriteLine("--------------------------------------------------");
            Console.WriteLine("Battlefield 2 Master Gamespy Server Emulator v" + version);
            Console.WriteLine("Created for BF2Statistics.com by Wilson212");
            Console.WriteLine();

            // Start login servers
            try
            {
                // Make sure Logs dir is created
                if (!Directory.Exists(Path.Combine(RootPath, "Logs")))
                    Directory.CreateDirectory(Path.Combine(RootPath, "Logs"));

                // Wrap error log into a writer
                ErrorLog = new LogWriter(Path.Combine(RootPath, "Logs", "MasterServer_Error.log"), false);

                // Setup Exception Handle
                AppDomain.CurrentDomain.UnhandledException += ExceptionHandler.OnUnhandledException;
                Application.ThreadException += ExceptionHandler.OnThreadException;

                // Start the Gamespy Servers
                ServerManager.StartServers();
                Console.WriteLine();
                Console.Write("Cmd > ");
            }
            catch (Exception e)
            {
                // Display error
                Console.WriteLine(e.Message);
                Console.WriteLine(" *** An exception will be logged *** ");
                Console.WriteLine();
                Console.Write("Press any key to close...");

                // Create exception log and wait for key input
                ExceptionHandler.GenerateExceptionLog(e);
                Console.ReadLine();
                return;
            }

            // Main program loop
            while (IsRunning)
            {
                CheckInput();
            }

            // Shut login servers down
            Console.WriteLine("Shutting down local Gamespy sockets...");
            ServerManager.Shutdown();
        }

        /// <summary>
        /// Gets the string contents of an embedded resource
        /// </summary>
        /// <param name="ResourceName"></param>
        /// <returns></returns>
        public static string GetResourceAsString(string ResourceName)
        {
            string Result = "";
            using (Stream ResourceStream = Assembly.GetExecutingAssembly().GetManifestResourceStream(ResourceName))
            using (StreamReader Reader = new StreamReader(ResourceStream))
                Result = Reader.ReadToEnd();

            return Result;
        }

        /// <summary>
        /// Reads the next input in the console (Blocking Method)
        /// </summary>
        private static void CheckInput()
        {
            // Get user input [Blocking]
            string Input = Console.ReadLine();

            // Make sure input is not empty
            if (String.IsNullOrWhiteSpace(Input))
            {
                Console.WriteLine("Please enter a command");
                Console.WriteLine();
                Console.Write("Cmd > ");
                return;
            }

            // Split input into an array by whitespace, empty entries are removed
            string[] InParts = Input.Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
            Dictionary<string, object> user = new Dictionary<string, object>();

            // Process Task
            try
            {
                switch (InParts[0].ToLowerInvariant())
                {
                    case "stop":
                    case "quit":
                    case "exit":
                        IsRunning = false; // Setting to false will stop program loop
                        break;
                    case "?":
                    case "help":
                        Console.Write(Environment.NewLine +
                            "stop/quit/exit          - Stops the server" + Environment.NewLine +
                            "debug {1/0}             - Enable or Disable ServerList debug Log" + Environment.NewLine +
                            "servers                 - Display the numbner of servers in the serverlist" + Environment.NewLine +
                            "listservers             - Display all the servers in the serverlist" + Environment.NewLine +
                            "clearservers            - Removes all servers from the serverlist" + Environment.NewLine
                        );
                        break;
                    case "servers":
                        Console.Write("Validated Servers in Serverlist: ");
                        Console.WriteLine(MasterServer.Servers.Values.Where(x => x.IsValidated).ToArray().Length);
                        break;
                    case "listservers":
                        foreach (GameServer server in MasterServer.Servers.Values)
                        {
                            // Skip servers with no data
                            if (!server.IsValidated) continue;

                            // List server
                            Console.WriteLine(@"[{0}] ""{1}"" {2} ({3}/{4})",
                                server.AddressInfo,
                                server.hostname,
                                server.mapname,
                                server.numplayers,
                                server.maxplayers
                            );
                        }
                        break;
                    case "clearservers":
                        MasterServer.Servers.Clear();
                        Console.WriteLine("Servers in Serverlist: " + MasterServer.Servers.Count);
                        break;
                    case "debug":
                        int debug;
                        if (!Int32.TryParse(InParts[1], out debug))
                        {
                            Console.WriteLine("Please enter a numeric only!");
                            break;
                        }

                        MasterServer.Debugging = (debug > 0);
                        Console.WriteLine("Gamespy Debuglog " + (MasterServer.Debugging ? "Enabled" : "Disabled"));
                        break;
                    case "enter":
                        // Insert a break point here in Visual Studio to and enter this command to "Enter" the program during debugging
                        IsRunning = true;
                        break;
                    default:
                        Console.WriteLine("Unrecognized input '{0}'", Input);
                        break;
                }
            }
            catch (Exception e)
            {
                Console.Write(e.Message);
            }

            // Await a new command
            if (IsRunning)
            {
                Console.WriteLine();
                Console.Write("Cmd > ");
            }
        }
    }
}

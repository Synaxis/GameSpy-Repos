using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using Server.Database;
using BattleSpy.Logging;

namespace Server
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
            Console.Title = $"Battlefield2 Statistics Gamespy Login Emulator v{version}";
            Console.WriteLine(@"__________         __    __  .__         .__   ");
            Console.WriteLine(@"\______   \_____ _/  |__/  |_|  |   ____ |  |   ____   ____");
            Console.WriteLine(@" |    |  _/\__  \\   __\   __\  | _/ __ \|  |  /  _ \ / ___\");
            Console.WriteLine(@" |    |   \ / __ \|  |  |  | |  |_\  ___/|  |_(  <_> ) /_/  >");
            Console.WriteLine(@" |______  /(____  /__|  |__| |____/\___  >____/\____/\___  / ");
            Console.WriteLine(@"        \/      \/                     \/           /_____/ ");
            Console.WriteLine();
            Console.WriteLine("--------------------------------------------------");
            Console.WriteLine($"Battlefield 2 Gamespy Login Emulator v{version}");
            Console.WriteLine("Created for BF2Statistics.com by Wilson212");
            Console.WriteLine();

            // Start login servers
            try
            {
                // Make sure Logs dir is created
                if (!Directory.Exists(Path.Combine(RootPath, "Logs")))
                    Directory.CreateDirectory(Path.Combine(RootPath, "Logs"));

                // Wrap error log into a writer
                ErrorLog = new LogWriter(Path.Combine(RootPath, "Logs", "LoginServer_Error.log"), false);

                // Setup Exception Handle
                AppDomain.CurrentDomain.UnhandledException += ExceptionHandler.OnUnhandledException;
                Application.ThreadException += ExceptionHandler.OnThreadException;

                // Start the Gamespy Servers
                ServerManager.StartServers();
                Console.WriteLine();
                Console.Write("Cmd > ");
            }
            catch(Exception e)
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
                    case "connections":
                        Console.WriteLine("Total Connections: {0}", ServerManager.NumConnections());
                        break;
                    case "accounts":
                        using(GamespyDatabase Db = new GamespyDatabase())
                            Console.WriteLine("Total Accounts: {0}", Db.GetNumAccounts());
                        break;
                    case "fetch":
                        // Prevent an out of range exception
                        if (InParts.Length < 2)
                        {
                            Console.WriteLine("Incorrect command format. Please type 'help' to see list of available commands.");
                            break;
                        }

                        // Make sure we have a nick
                        if (String.IsNullOrEmpty(InParts[1]))
                        {
                            Console.WriteLine("No account named provided. Please make sure you are providing an account name, and not a space");
                            break;
                        }

                        // Fetch user account info
                        using (GamespyDatabase Db = new GamespyDatabase())
                            user = Db.GetUser(InParts[1]);

                        if (user == null)
                        {
                            Console.WriteLine("Account '{0}' does not exist in the gamespy database.", InParts[1]);
                            break;
                        }

                        // Get BF2 PID
                        Console.WriteLine(" - PlayerID: " + user["id"].ToString());
                        Console.WriteLine(" - Email: " + user["email"].ToString());
                        Console.WriteLine(" - Country: " + user["country"].ToString());
                        break;
                    case "create":
                        // Prevent an out of range exception
                        if (InParts.Length < 4)
                        {
                            Console.WriteLine("Incorrect command format. Please type 'help' to see list of available commands.");
                            break;
                        }

                        // Make sure our strings are not empty!
                        if (String.IsNullOrEmpty(InParts[1]) || String.IsNullOrEmpty(InParts[2]) || String.IsNullOrEmpty(InParts[3]))
                        {
                            Console.WriteLine("Account name, password, or email was not provided. Please try again with the correct format.");
                            break;
                        }

                        // Disposible connection
                        using (GamespyDatabase Db = new GamespyDatabase())
                        {
                            // Make sure the account exists!
                            if (Db.UserExists(InParts[1]))
                            {
                                Console.WriteLine("Account '{0}' already exists in the gamespy database.", InParts[1]);
                               break;
                            }

                            bool r = Db.CreateUser(InParts[1], InParts[2], InParts[3], "00") > 0;
                            Console.WriteLine((r == true) ? "Account created successfully" : "Error creating account!");
                        }
                        break;
                    case "delete":
                        // Prevent an out of range exception
                        if (InParts.Length < 2)
                        {
                            Console.WriteLine("Incorrect command format. Please type 'help' to see list of available commands.");
                            break;
                        }

                        // Make sure our strings are not empty!
                        if (String.IsNullOrEmpty(InParts[1]))
                        {
                            Console.WriteLine("Account name was not provided. Please try again with the correct format.");
                            break;
                        }

                        // Disposible connection
                        using (GamespyDatabase Db = new GamespyDatabase())
                        {
                            // Make sure the account exists!
                            if (!Db.UserExists(InParts[1]))
                            {
                                break;
                            }

                            // Do a confimration
                            Console.Write("Are you sure you want to delete account '{0}'? <y/n>: ", InParts[1]);
                            string v = Console.ReadLine().ToLower().Trim();

                            // If no, stop here
                            if (v == "n" || v == "no" || v == "na" || v == "nope")
                            {
                                Console.WriteLine("Command cancelled.");
                                break;
                            }

                            // Process any command other then no
                            if (v == "y" || v == "yes" || v == "ya" || v == "yep")
                            {
                                if (Db.DeleteUser(InParts[1]) == 1)
                                    Console.WriteLine("Account deleted successfully");
                                else
                                    Console.WriteLine("Failed to remove account from database.");
                            }
                            else
                                Console.WriteLine("Incorrect repsonse. Aborting command");
                        }

                        break;
                    case "setpid":
                        // Prevent an out of range exception
                        if (InParts.Length < 3)
                        {
                            Console.WriteLine("Incorrect command format. Please type 'help' to see list of available commands.");
                            break;
                        }

                        // Make sure our strings are not empty!
                        if (String.IsNullOrEmpty(InParts[1]) || String.IsNullOrEmpty(InParts[2]))
                        {
                            Console.WriteLine("Account name or PID not provided. Please try again with the correct format.");
                            break;
                        }

                        // Disposible connection
                        using (GamespyDatabase Db = new GamespyDatabase())
                        {
                            // Make sure the account exists!
                            user = Db.GetUser(InParts[1]);
                            if (user == null)
                            {
                                Console.WriteLine("Account '{0}' does not exist in the gamespy database.", InParts[1]);
                                break;
                            }

                            // Try to make a PID out of parts 2
                            int newpid;
                            if (!Int32.TryParse(InParts[2], out newpid))
                            {
                                Console.WriteLine("Player ID must be an numeric only!");
                                break;
                            }

                            // try and set the PID
                            int result = Db.SetPID(InParts[1], newpid);
                            string message = "";
                            switch (result)
                            {
                                case 1:
                                    message = "New PID is set!";
                                    break;
                                case 0:
                                    message = "Error setting PID";
                                    break;
                                case -1:
                                    message = String.Format("Account '{0}' does not exist in the gamespy database.", InParts[1]);
                                    break;
                                case -2:
                                    message = String.Format("PID {0} is already in use.", newpid);
                                    break;
                            }
                            Console.WriteLine(" - " + message);
                        }
                        break;
                    case "?":
                    case "help":
                        Console.Write(Environment.NewLine +
                            "stop/quit/exit          - Stops the server" + Environment.NewLine +
                            "connections             - Displays the current number of connected clients" + Environment.NewLine +
                            "accounts                - Displays the current number accounts in the DB." + Environment.NewLine +
                            "create {nick} {password} {email}  - Create a new Gamespy account." + Environment.NewLine +
                            "delete {nick}           - Deletes a user account." + Environment.NewLine +
                            "fetch {nick}            - Displays the account information" + Environment.NewLine +
                            "setpid {nick} {newpid}  - Sets the BF2 Player ID of the givin account name" + Environment.NewLine
                        );
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

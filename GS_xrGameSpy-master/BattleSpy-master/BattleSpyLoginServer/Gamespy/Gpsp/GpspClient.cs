using System;
using System.Collections.Generic;
using System.Text;
using Server.Database;
using BattleSpy.Gamespy;
using BattleSpy;

namespace Server
{
    public class GpspClient : IDisposable
    {
        /// <summary>
        /// A unqie identifier for this connection
        /// </summary>
        public long ConnectionID;

        /// <summary>
        /// Indicates whether this object is disposed
        /// </summary>
        public bool Disposed { get; protected set; } = false;

        /// <summary>
        /// The clients socket network stream
        /// </summary>
        public GamespyTcpStream Stream { get; protected set; }

        /// <summary>
        /// Event fired when the connection is closed
        /// </summary>
        public static event GpspConnectionClosed OnDisconnect;

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="client"></param>
        public GpspClient(GamespyTcpStream client, long connectionId)
        {
            // Generate a unique name for this connection
            ConnectionID = connectionId;

            // Init a new client stream class
            Stream = client;
            Stream.OnDisconnect += () => Dispose();
            Stream.DataReceived += (message) =>
            {
                // Read client message, and parse it into key value pairs
                string[] recieved = message.TrimStart('\\').Split('\\');
                Dictionary<string, string> Data = ConvertToKeyValue(recieved);
                switch (recieved[0])
                {
                    case "nicks":
                        SendNicks(Data);
                        break;
                    case "check":
                        SendCheck(Data);
                        break;
                }
            };
        }

        /// <summary>
        /// Destructor
        /// </summary>
        ~GpspClient()
        {
            if (!Disposed)
                this.Dispose();
        }

        public void Dispose()
        {
            // Only dispose once
            if (Disposed) return;
            Dispose(false);
        }

        /// <summary>
        /// Dispose method to be called by the server
        /// </summary>
        public void Dispose(bool DisposeEventArgs = false)
        {
            // Only dispose once
            if (Disposed) return;
            Disposed = true;

            // If connection is still alive, disconnect user
            if (!Stream.SocketClosed)
                Stream.Close(DisposeEventArgs);

            // Call disconnect event
            if (OnDisconnect != null)
                OnDisconnect(this);
        }

        /// <summary>
        /// This method is requested by the client when logging in to fetch all the account
        /// names that have the specified email address and password combination
        /// </summary>
        /// <param name="recvData"></param>
        private void SendNicks(Dictionary<string, string> recvData)
        {
            // Make sure we have the needed data
            if (!recvData.ContainsKey("email") || (!recvData.ContainsKey("pass") && !recvData.ContainsKey("passenc")))
            {
                Stream.SendAsync(@"\error\\err\0\fatal\\errmsg\Invalid Query!\id\1\final\");
                return;
            }

            // Try to get user data from database
            try
            {
                // Get our password from the provided query
                string password = (recvData.ContainsKey("pass"))
                    ? recvData["pass"]
                    : GamespyUtils.DecodePassword(recvData["passenc"]);

                // Fetch soldiers that match this email and password
                using (GamespyDatabase Db = new GamespyDatabase())
                {
                    var Clients = Db.GetUsersByEmailPass(recvData["email"], password);
                    StringBuilder Response = new StringBuilder(@"\nr\" + Clients.Count);
                    for (int i = 0; i < Clients.Count; i++)
                        Response.AppendFormat(@"\nick\{0}\uniquenick\{0}", Clients[i]["name"]);
                    
                    Response.Append(@"\ndone\\final\");
                    Stream.SendAsync(Response.ToString());
                }
            }
            catch
            {
                Stream.SendAsync(@"\error\\err\551\fatal\\errmsg\Unable to get any associated profiles.\id\1\final\");
            }
        }

        /// <summary>
        /// This is the primary method for fetching an accounts BF2 PID
        /// </summary>
        /// <param name="recvData"></param>
        private void SendCheck(Dictionary<string, string> recvData)
        {
            // Make sure we have the needed data
            if (!recvData.ContainsKey("nick"))
            {
                Stream.SendAsync(@"\error\\err\0\fatal\\errmsg\Invalid Query!\id\1\final\");
                return;
            }

            // Try to get user data from database
            try
            {
                using (GamespyDatabase Db = new GamespyDatabase())
                {
                    int pid = Db.GetPlayerId(recvData["nick"]);
                    if(pid == 0)
                        Stream.SendAsync(@"\error\\err\265\fatal\\errmsg\Username [{0}] doesn't exist!\id\1\final\", recvData["nick"]);
                    else
                        Stream.SendAsync(@"\cur\0\pid\{0}\final\", pid);
                }
            }
            catch
            {
                Stream.SendAsync(@"\error\\err\265\fatal\\errmsg\Database service is Offline!\id\1\final\");
                //Dispose();
            }
        }

        /// <summary>
        /// Converts a recived parameter array from the client string to a keyValue pair dictionary
        /// </summary>
        /// <param name="parts">The array of data from the client</param>
        /// <returns></returns>
        private static Dictionary<string, string> ConvertToKeyValue(string[] parts)
        {
            Dictionary<string, string> Data = new Dictionary<string, string>();
            try
            {
                for (int i = 0; i < parts.Length; i += 2)
                {
                    if (!Data.ContainsKey(parts[i]))
                        Data.Add(parts[i], parts[i + 1]);
                }
            }
            catch (IndexOutOfRangeException) { }

            return Data;
        }
    }
}

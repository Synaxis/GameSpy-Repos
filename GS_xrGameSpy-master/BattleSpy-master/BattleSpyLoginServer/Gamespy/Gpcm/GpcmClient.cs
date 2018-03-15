using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using Server.Database;
using BattleSpy.Gamespy;
using BattleSpy;

namespace Server
{
    /// <summary>
    /// Gamespy Client Manager
    /// This class is used to proccess the client login process,
    /// create new user accounts, and fetch profile information
    /// <remarks>gpcm.gamespy.com</remarks>
    /// </summary>
    public class GpcmClient : IDisposable, IEquatable<GpcmClient>
    {
        #region Variables

        /// <summary>
        /// Gets the current login status
        /// </summary>
        public LoginStatus Status { get; protected set; }

        /// <summary>
        /// The connected clients Player Id
        /// </summary>
        public int PlayerId { get; protected set; }

        /// <summary>
        /// The connected clients Nick
        /// </summary>
        public string PlayerNick { get; protected set; }

        /// <summary>
        /// The connected clients Email Address
        /// </summary>
        public string PlayerEmail { get; protected set; }

        /// <summary>
        /// The connected clients country code
        /// </summary>
        public string PlayerCountryCode { get; protected set; }

        /// <summary>
        /// The clients password, MD5 hashed from UTF8 bytes
        /// </summary>
        private string PasswordHash;

        /// <summary>
        /// The TcpClient's Endpoint
        /// </summary>
        public IPEndPoint RemoteEndPoint { get; protected set; }

        /// <summary>
        /// The profile id parameter that is sent back to the client is initially 2, 
        /// and then 5 everytime after that. So we set here, whether we have sent the 
        /// profile to the client initially (with \id\2) yet.
        /// </summary>
        private bool ProfileSent = false;

        /// <summary>
        /// The users session key
        /// </summary>
        private ushort SessionKey;

        /// <summary>
        /// The Servers challange key, sent when the client first connects.
        /// This is used as part of the hash used to "proove" to the client
        /// that the password in our database matches what the user enters
        /// </summary>
        private string ServerChallengeKey;

        /// <summary>
        /// Variable that determines if the client is disconnected,
        /// and this object can be cleared from memory
        /// </summary>
        public bool Disposed { get; protected set; }

        /// <summary>
        /// Indicates the connection ID for this connection
        /// </summary>
        public long ConnectionId { get; protected set; }

        /// <summary>
        /// Indicates the date and time this connection was created
        /// </summary>
        public readonly DateTime Created = DateTime.Now;

        /// <summary>
        /// The clients socket network stream
        /// </summary>
        public GamespyTcpStream Stream { get; protected set; }

        /// <summary>
        /// A random... random
        /// </summary>
        private Random RandInstance = new Random((int)DateTime.Now.Ticks);

        /// <summary>
        /// The date time of when this connection was created. Used to disconnect user
        /// connections that hang
        /// </summary>
        private DateTime ConnectionCreated = DateTime.Now;

        /// <summary>
        /// Our CRC16 object for generating Checksums
        /// </summary>
        protected static Crc16 Crc = new Crc16(Crc16Mode.Standard);

        /// <summary>
        /// Array of characters used in generating a signiture
        /// </summary>
        private static char[] AlphaChars = {
                'A', 'B', 'C', 'D', 'E', 'F', 'G', 'H', 'I', 'J', 'K', 'L', 'M',
                'N', 'O', 'P', 'Q', 'R', 'S', 'T', 'U', 'V', 'W', 'X', 'Y', 'Z'
            };

        /// <summary>
        /// An array of Alpha Numeric characters used in generating a random string
        /// </summary>
        private static char[] AlphaNumChars = { 
                '0', '1', '2', '3', '4', '5', '6', '7', '8', '9',
                'a', 'b', 'c', 'd', 'e', 'f', 'g', 'h', 'i', 'j', 'k', 'l', 'm',
                'n', 'o', 'p', 'q', 'r', 's', 't', 'u', 'v', 'w', 'x', 'y', 'z',
                'A', 'B', 'C', 'D', 'E', 'F', 'G', 'H', 'I', 'J', 'K', 'L', 'M',
                'N', 'O', 'P', 'Q', 'R', 'S', 'T', 'U', 'V', 'W', 'X', 'Y', 'Z'
            };

        /// <summary>
        /// Array of Hex cahracters, with no leading 0
        /// </summary>
        private static char[] HexChars = {
                '1', '2', '3', '4', '5', '6', '7', '8', '9',
                'a', 'b', 'c', 'd', 'e', 'f'
            };

        /// <summary>
        /// An Event that is fired when the client successfully logs in.
        /// </summary>
        public static event ConnectionUpdate OnSuccessfulLogin;

        /// <summary>
        /// Event fired when that remote connection logs out, or
        /// the socket gets disconnected. This event will not fire
        /// unless OnSuccessfulLogin event was fired first.
        /// </summary>
        public static event GpcmConnectionClosed OnDisconnect;

        #endregion Variables

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="ReadArgs">The Tcp Client connection</param>
        public GpcmClient(GamespyTcpStream ConnectionStream, long ConnectionId)
        {
            // Set default variable values
            PlayerNick = "Connecting...";
            PlayerId = 0;
            RemoteEndPoint = (IPEndPoint)ConnectionStream.RemoteEndPoint;
            Disposed = false;
            Status = LoginStatus.Connected;

            // Set the connection ID
            this.ConnectionId = ConnectionId;

            // Create our Client Stream
            Stream = ConnectionStream;
            Stream.OnDisconnect += Stream_OnDisconnect;
            Stream.DataReceived += Stream_DataReceived;
            Stream.BeginReceive();
        }

        /// <summary>
        /// Destructor
        /// </summary>
        ~GpcmClient()
        {
            if (!Disposed)
                this.Dispose();
        }

        /// <summary>
        /// Disposes of the client object. The connection is no longer
        /// closed here and the Disconnect even is NO LONGER fired
        /// </summary>
        public void Dispose()
        {
            // Preapare to be unloaded from memory
            this.Disposed = true;
        }

        /// <summary>
        /// Logs the client out of the game client, and closes the stream
        /// </summary>
        /// <param name="reason">
        /// The disconnect reason code. 
        /// </param>
        /// <remarks>
        /// If set the <paramref name="reason"/> is set to <see cref="DisconnectReason.ForcedServerShutdown"/>, 
        /// the OnDisconect event will not be called, the database will not be updated to reset everyone's session code, 
        /// and the EventArgs objects will NOT be returned to the IO pool. You should only set to
        /// <see cref="DisconnectReason.ForcedServerShutdown"/> for a planned server shutdown.
        /// </remarks>
        public void Disconnect(DisconnectReason reason)
        {
            // Update database session
            if (Status == LoginStatus.Completed && reason != DisconnectReason.ForcedServerShutdown)
            {
                try
                {
                    using (GamespyDatabase Database = new GamespyDatabase())
                        Database.Execute("UPDATE player SET online=0 WHERE id=" + PlayerId);
                }
                catch { }
            }

            // If connection is still alive, disconnect user
            try
            {
                Stream.OnDisconnect -= Stream_OnDisconnect;
                Stream.DataReceived -= Stream_DataReceived;
                Stream.Close(reason == DisconnectReason.ForcedServerShutdown);
            }
            catch { }

            // Set status and log
            if (Status == LoginStatus.Completed)
            {
                if (reason == DisconnectReason.NormalLogout)
                {
                    ServerManager.Log(
                        "Client Logout:  {0} - {1} - {2}",
                        PlayerNick,
                        PlayerId,
                        RemoteEndPoint
                    );
                }
                else
                {
                    ServerManager.Log(
                        "Client Disconnected:  {0} - {1} - {2}, Code={3}",
                        PlayerNick,
                        PlayerId,
                        RemoteEndPoint,
                        Enum.GetName(typeof(DisconnectReason), reason)
                    );
                }
            }

            // Preapare to be unloaded from memory
            Status = LoginStatus.Disconnected;
            Dispose();

            // Call disconnect event
            OnDisconnect?.Invoke(this);
        }

        #region Stream Callbacks

        /// <summary>
        /// Main listner loop. Keeps an open stream between the client and server while
        /// the client is logged in / playing
        /// </summary>
        private void Stream_DataReceived(string message)
        {
            // Read client message, and parse it into key value pairs
            string[] recieved = message.TrimStart('\\').Split('\\');
            switch (recieved[0])
            {
                case "newuser":
                    CreateNewUser(ConvertToKeyValue(recieved));
                    break;
                case "login":
                    ProcessLogin(ConvertToKeyValue(recieved));
                    break;
                case "getprofile":
                    SendProfile();
                    break;
                case "updatepro":
                    UpdateUser(ConvertToKeyValue(recieved));
                    break;
                case "logout":
                    Disconnect(DisconnectReason.NormalLogout);
                    break;
                default:
                    Stream.SendAsync(@"\error\\err\0\fatal\\errmsg\Invalid Query!\id\1\final\");
                    Program.ErrorLog.Write("NOTICE: [GpcmClient.Stream_DataReceived] Unkown Message Passed: {0}", message);
                    break;
            }
        }

        /// <summary>
        /// Event fired when the stream disconnects unexpectedly
        /// </summary>
        private void Stream_OnDisconnect()
        {
            Disconnect(DisconnectReason.Disconnected);
        }

        #endregion Stream Callbacks

        #region Login Steps

        /// <summary>
        ///  This method starts off by sending a random string 10 characters
        ///  in length, known as the Server challenge key. This is used by 
        ///  the client to return a client challenge key, which is used
        ///  to validate login information later.
        ///  </summary>
        public void SendServerChallenge()
        {
            // Create a string builder for this next operation
            StringBuilder builder = new StringBuilder(10);

            // Only send the login challenge once
            if (Status != LoginStatus.Connected)
            {
                // Create an exception message
                TimeSpan ts = DateTime.Now - Created;
                builder.AppendLine("The server challenge has already been sent. Cannot send another login challenge.");
                builder.Append($"\tChallenge was sent \"{ts.ToString()}\" ago.");

                // Disconnect user
                Disconnect(DisconnectReason.ClientChallengeAlreadySent);

                // Throw the error
                throw new Exception(builder.ToString());
            }

            // First we need to create a random string the length of 10 characters
            for (int i = 0; i < 10; i++)
                builder.Append(AlphaChars[RandInstance.Next(AlphaChars.Length)]);

            // Next we send the client the challenge key
            ServerChallengeKey = builder.ToString();
            Status = LoginStatus.Processing;
            Stream.SendAsync(@"\lc\1\challenge\{0}\id\1\final\", ServerChallengeKey);
        }

        /// <summary>
        /// This method verifies the login information sent by
        /// the client, and returns encrypted data for the client
        /// to verify as well
        /// </summary>
        public void ProcessLogin(Dictionary<string, string> Recv)
        {
            // Make sure we have all the required data to process this login
            if (!Recv.ContainsKey("uniquenick") || !Recv.ContainsKey("challenge") || !Recv.ContainsKey("response"))
            {
                Stream.SendAsync(@"\error\\err\0\fatal\\errmsg\Invalid Query!\id\1\final\");
                Disconnect(DisconnectReason.InvalidLoginQuery);
                return;
            }

            // Dispose connection after use
            try
            {
                using (GamespyDatabase Conn = new GamespyDatabase())
                {
                    // Try and fetch the user from the database
                    Dictionary<string, object> User = Conn.GetUser(Recv["uniquenick"]);
                    if (User == null)
                    {
                        Stream.SendAsync(@"\error\\err\265\fatal\\errmsg\The uniquenick provided is incorrect!\id\1\final\");
                        Disconnect(DisconnectReason.InvalidUsername);
                        return;
                    }

                    // Check if user is banned
                    bool banned = Int32.Parse(User["permban"].ToString()) > 0;
                    if (banned)
                    {
                        Stream.SendAsync(@"\error\\err\265\fatal\\errmsg\You account has been permanently suspended.\id\1\final\");
                        Disconnect(DisconnectReason.PlayerIsBanned);
                        return;
                    }

                    // Set player variables
                    PlayerId = Int32.Parse(User["id"].ToString());
                    PlayerNick = Recv["uniquenick"];
                    PlayerEmail = User["email"].ToString();
                    PlayerCountryCode = User["country"].ToString();
                    PasswordHash = User["password"].ToString().ToLowerInvariant();

                    // Use the GenerateProof method to compare with the "response" value. This validates the given password
                    if (Recv["response"] == GenerateProof(Recv["challenge"], ServerChallengeKey))
                    {
                        // Create session key
                        SessionKey = Crc.ComputeChecksum(PlayerNick);

                        // Password is correct
                        Stream.SendAsync(
                            @"\lc\2\sesskey\{0}\proof\{1}\userid\{2}\profileid\{2}\uniquenick\{3}\lt\{4}__\id\1\final\",
                            SessionKey,
                            GenerateProof(ServerChallengeKey, Recv["challenge"]), // Do this again, Params are reversed!
                            PlayerId,
                            PlayerNick,
                            GenerateRandomString(22) // Generate LT whatever that is (some sort of random string, 22 chars long)
                        );

                        // Log Incoming Connections
                        ServerManager.Log("Client Login:   {0} - {1} - {2}", PlayerNick, PlayerId, RemoteEndPoint);
                        Conn.Execute(
                            "UPDATE player SET online=1, lastip=@P0, lastonline=@P1 WHERE id=@P2", 
                            RemoteEndPoint.Address, 
                            DateTime.UtcNow.ToUnixTimestamp(),
                            PlayerId
                        );

                        // Update status last, and call success login
                        Status = LoginStatus.Completed;
                        OnSuccessfulLogin?.Invoke(this);
                    }
                    else
                    {
                        // Log Incoming Connections
                        ServerManager.Log("Failed Login Attempt: {0} - {1} - {2}", PlayerNick, PlayerId, RemoteEndPoint);

                        // Password is incorrect with database value
                        Stream.SendAsync(@"\error\\err\260\fatal\\errmsg\The password provided is incorrect.\id\1\final\");
                        Disconnect(DisconnectReason.InvalidPassword);
                    }
                }
            }
            catch (Exception ex)
            {
                ExceptionHandler.GenerateExceptionLog(ex);
                Disconnect(DisconnectReason.GeneralError);
                return;
            }
        }

        /// <summary>
        /// This method is called when the client requests for the Account profile
        /// </summary>
        private void SendProfile()
        {
            Stream.SendAsync(
                @"\pi\\profileid\{0}\nick\{1}\userid\{0}\email\{2}\sig\{3}\uniquenick\{1}\pid\0\firstname\\lastname\" +
                @"\countrycode\{4}\birthday\16844722\lon\0.000000\lat\0.000000\loc\\id\{5}\\final\",
                PlayerId, PlayerNick, PlayerEmail, GenerateSig(), PlayerCountryCode, (ProfileSent ? "5" : "2")
            );

            // Set that we send the profile initially
            if (!ProfileSent) ProfileSent = true;
        }

        #endregion Steps

        #region User Methods

        /// <summary>
        /// Whenever the "newuser" command is recieved, this method is called to
        /// add the new users information into the database
        /// </summary>
        /// <param name="Recv">Array of parms sent by the server</param>
        private void CreateNewUser(Dictionary<string, string> Recv)
        {
            // Make sure the user doesnt exist already
            try
            {
                using (GamespyDatabase Database = new GamespyDatabase())
                {
                    // Check to see if user exists
                    if (Database.UserExists(Recv["nick"]))
                    {
                        Stream.SendAsync(@"\error\\err\516\fatal\\errmsg\This account name is already in use!\id\1\final\");
                        Disconnect(DisconnectReason.CreateFailedUsernameExists);
                        return;
                    }

                    // We need to decode the Gamespy specific encoding for the password
                    string Password = GamespyUtils.DecodePassword(Recv["passwordenc"]);
                    string Cc = (RemoteEndPoint.AddressFamily == AddressFamily.InterNetwork)
                        ? Ip2nation.GetCountryCode(RemoteEndPoint.Address)
                        : "US";

                    // Attempt to create account. If Pid is 0, then we couldnt create the account
                    if ((PlayerId = Database.CreateUser(Recv["nick"], Password, Recv["email"], Cc)) == 0)
                    {
                        Stream.SendAsync(@"\error\\err\516\fatal\\errmsg\Error creating account!\id\1\final\");
                        Disconnect(DisconnectReason.CreateFailedDatabaseError);
                        return;
                    }

                    Stream.SendAsync(@"\nur\\userid\{0}\profileid\{0}\id\1\final\", PlayerId);
                }
            }
            catch (Exception e)
            {
                // Check for invalid query params
                if (e is KeyNotFoundException)
                {
                    Stream.SendAsync(@"\error\\err\0\fatal\\errmsg\Invalid Query!\id\1\final\");
                }
                else
                {
                    Stream.SendAsync(@"\error\\err\516\fatal\\errmsg\Error creating account!\id\1\final\");
                    Program.ErrorLog.Write("ERROR: [GpcmClient.CreateNewUser] An error occured while trying to create a new User account :: " + e.Message);
                }

                Disconnect(DisconnectReason.GeneralError);
                return;
            }
        }


        /// <summary>
        /// Updates the Users Country code when sent by the client
        /// </summary>
        /// <param name="recv">Array of information sent by the server</param>
        private void UpdateUser(Dictionary<string, string> Recv)
        {
            // Set clients country code
            try
            {
                using (GamespyDatabase Conn = new GamespyDatabase())
                    Conn.UpdateUser(PlayerId, Recv["countrycode"]);
            }
            catch (Exception e)
            {
                Program.ErrorLog.Write("ERROR: [GpcmClient.UpdateUser] " + e.Message);
            }
        }

        /// <summary>
        /// Polls the connection, and checks for drops
        /// </summary>
        public void SendKeepAlive()
        {
            if (Status == LoginStatus.Completed)
            {
                // Try and send a Keep-Alive
                try
                {
                    Stream.SendAsync(@"\ka\\final\");
                }
                catch
                {
                    Disconnect(DisconnectReason.KeepAliveFailed);
                }
            }
        }

        #endregion

        #region Misc Methods

        /// <summary>
        /// Converts a recived parameter array from the client string to a keyValue pair dictionary
        /// </summary>
        /// <param name="parts">The array of data from the client</param>
        /// <returns></returns>
        private static Dictionary<string, string> ConvertToKeyValue(string[] parts)
        {
            Dictionary<string, string> Dic = new Dictionary<string, string>();
            try
            {
                for (int i = 0; i < parts.Length; i += 2)
                {
                    if (!Dic.ContainsKey(parts[i]))
                        Dic.Add(parts[i], parts[i + 1]);
                }
            }
            catch (IndexOutOfRangeException) { }

            return Dic;
        }

        /// <summary>
        /// Generates an MD5 hash, which is used to verify the clients login information
        /// </summary>
        /// <param name="challenge1">First challenge key</param>
        /// <param name="challenge2">Second challenge key</param>
        /// <returns>
        ///     The proof verification MD5 hash string that can be compared to what the client sends,
        ///     to verify that the users entered password matches the password in the database.
        /// </returns>
        private string GenerateProof(string challenge1, string challenge2)
        {
            // Generate our string to be hashed
            StringBuilder HashString = new StringBuilder(PasswordHash);
            HashString.Append(' ', 48); // 48 spaces
            HashString.Append(PlayerNick);
            HashString.Append(challenge1);
            HashString.Append(challenge2);
            HashString.Append(PasswordHash);
            return HashString.ToString().GetMD5Hash();
        }

        /// <summary>
        /// Generates a random alpha-numeric string
        /// </summary>
        /// <param name="length">The lenght of the string to be generated</param>
        /// <returns></returns>
        private string GenerateRandomString(int length)
        {
            StringBuilder Response = new StringBuilder();
            for (int i = 0; i < length; i++)
                Response.Append(AlphaNumChars[RandInstance.Next(62)]);

            return Response.ToString();
        }

        /// <summary>
        /// Generates a random signature
        /// </summary>
        /// <returns></returns>
        private string GenerateSig()
        {
            StringBuilder Response = new StringBuilder();
            for (int length = 0; length < 32; length++)
                Response.Append(HexChars[RandInstance.Next(14)]);

            return Response.ToString();
        }

        #endregion

        public bool Equals(GpcmClient other)
        {
            if (other == null) return false;
            return (PlayerId == other.PlayerId || PlayerNick == other.PlayerNick);
        }

        public override bool Equals(object obj)
        {
            return this.Equals(obj as GpcmClient);
        }

        public override int GetHashCode()
        {
            return PlayerId;
        }
    }
}


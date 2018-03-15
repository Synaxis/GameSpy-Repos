using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using BattleSpy.Gamespy;
using BattleSpy.Logging;

namespace BattlelogMaster
{
    /// <summary>
    /// This class is used to replicate the Gamespy CK key servers.
    /// All cd keys received are automatically considered valid
    /// </summary>
    public class CDKeyServer : GamespyUdpSocket
    {
        /// <summary>
        /// Debugging for Development
        /// </summary>
        private bool Debugging;

        /// <summary>
        /// The Debug log (GamespyDebug.log)
        /// </summary>
        private LogWriter DebugLog;

        public CDKeyServer(IPEndPoint bindTo, LogWriter DebugLog) : base(bindTo, 48)
        {
            // Debugging
            this.Debugging = Config.GetType<bool>("Debug", "DebugServerlist");
            this.DebugLog = DebugLog;

            // Start accepting remote connections
            base.StartAcceptAsync();
        }

        /// <summary>
        /// Called when a connection comes in on the CDKey server
        /// </summary>
        /// known messages
        ///  \ka\ = keep alive from the game server every 20s, we don't care about this
        ///  \auth\ ... = authenticate cd key, this is what we care about
        ///  \disc\ ... = disconnect cd key, because there's checks if the cd key is in use, which we don't care about really, but we could if we wanted to
        /// </remarks>
        protected override void ProcessAccept(GamespyUdpPacket Packet)
        {
            // If we dont reply, we must manually release the EventArgs back to the pool
            bool replied = false;

            try
            {
                // Decrypt message
                IPEndPoint remote = (IPEndPoint)Packet.AsyncEventArgs.RemoteEndPoint;
                string decrypted = Xor(Encoding.UTF8.GetString(Packet.BytesRecieved)).Trim('\\');

                // Ignore keep alive pings
                if (!decrypted.StartsWith("ka"))
                {
                    Dictionary<string, string> recv = ConvertToKeyValue(decrypted.Split('\\'));
                    if (recv.ContainsKey("auth") && recv.ContainsKey("resp") && recv.ContainsKey("skey"))
                    {
                        if (Debugging) DebugLog.Write("CDKey Check Requested from: {0}:{1}", remote.Address, remote.Port);

                        // Normally you would check the CD key database for the CD key MD5, but we arent Gamespy, we dont care
                        string reply = String.Format(@"\uok\\cd\{0}\skey\{1}", recv["resp"].Substring(0, 32), recv["skey"]);

                        // Set new packet contents, and send a reply
                        Packet.SetBufferContents(Encoding.UTF8.GetBytes(Xor(reply)));
                        base.ReplyAsync(Packet);
                        replied = true;
                    }
                    else if (recv.ContainsKey("disc"))
                    {
                        // Handle, User disconnected from server
                    }
                    else
                    {
                        if (Debugging) DebugLog.Write("Incomplete or Invalid CDKey Packet Received: " + decrypted);
                    }
                }
            }
            catch (Exception E)
            {
                Program.ErrorLog.Write("ERROR: [MasterServer.CDKeySocket_OnDataReceived] " + E.Message);
            }
            finally
            {
                // Release so that we can pool the EventArgs to be used on another connection
                if (!replied)
                    base.Release(Packet.AsyncEventArgs);
            }
        }

        /// <summary>
        /// Closes the underlying socket
        /// </summary>
        public void Shutdown()
        {
            base.ShutdownSocket();
            base.Dispose();
        }

        /// <summary>
        /// Encrypts / Descrypts the CDKey Query String
        /// </summary>
        /// <param name="s"></param>
        /// <returns></returns>
        private static string Xor(string s)
        {
            const string gamespy = "gamespy";
            int length = s.Length;
            char[] data = s.ToCharArray();
            int index = 0;

            for (int i = 0; length > 0; length--)
            {
                if (i >= gamespy.Length)
                    i = 0;

                data[index++] ^= gamespy[i++];
            }

            return new String(data);
        }

        /// <summary>
        /// Converts a received parameter array from the client string to a keyValue pair dictionary
        /// </summary>
        /// <param name="parts">The array of data from the client</param>
        /// <returns></returns>
        private static Dictionary<string, string> ConvertToKeyValue(string[] parts)
        {
            Dictionary<string, string> Dic = new NiceDictionary<string, string>();

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

        protected override void OnException(Exception e) => ExceptionHandler.GenerateExceptionLog(e);
    }
}

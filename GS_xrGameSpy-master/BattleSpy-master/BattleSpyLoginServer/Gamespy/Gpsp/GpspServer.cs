using System;
using System.Collections.Concurrent;
using System.Net;
using System.Threading;
using BattleSpy.Gamespy;

namespace Server
{
    /// <summary>
    /// This class emulates the Gamespy Search Provider Server on port 29901.
    /// This server is responsible for fetching all associates accounts with
    /// the provided email and password, as well as verifying a player account ID.
    /// </summary>
    public class GpspServer : GamespyTcpSocket
    {
        /// <summary>
        /// Max number of concurrent open and active connections.
        /// </summary>
        /// <remarks>Connections to the Gpsp server are short lived</remarks>
        public const int MaxConnections = 48;

        /// <summary>
        /// A connection counter, used to create unique connection id's
        /// </summary>
        private static long ConnectionCounter = 0;

        /// <summary>
        /// A List of sucessfully active connections (Name => Client Obj) on the MasterServer TCP line
        /// </summary>
        private static ConcurrentDictionary<long, GpspClient> Clients = new ConcurrentDictionary<long, GpspClient>();

        public GpspServer(IPEndPoint bindTo) : base(bindTo, MaxConnections)
        {
            // Register for disconnect
            GpspClient.OnDisconnect += GpspClient_OnDisconnect;

            // Begin accepting connections
            base.StartAcceptAsync();
        }

        /// <summary>
        /// Shutsdown the GPSP server and socket
        /// </summary>
        public void Shutdown()
        {
            // Stop accepting new connections
            base.IgnoreNewConnections = true;

            // Unregister events so we dont get a shit ton of calls
            GpspClient.OnDisconnect -= GpspClient_OnDisconnect;

            // Disconnected all connected clients
            foreach (GpspClient C in Clients.Values)
                C.Dispose(true);

            // clear clients
            Clients.Clear();

            // Shutdown the listener socket
            base.ShutdownSocket();

            // Tell the base to dispose all free objects
            base.Dispose();
        }

        /// <summary>
        /// When a new connection is established, we the parent class are responsible
        /// for handling the processing
        /// </summary>
        /// <param name="Stream">A GamespyTcpStream object that wraps the I/O AsyncEventArgs and socket</param>
        protected override void ProcessAccept(GamespyTcpStream Stream)
        {
            // Get our connection id
            long ConID = Interlocked.Increment(ref ConnectionCounter);
            GpspClient client;

            try
            {
                // Convert the TcpClient to a MasterClient
                client = new GpspClient(Stream, ConID);
                Clients.TryAdd(ConID, client);

                // Start receiving data
                Stream.BeginReceive();
            }
            catch
            {
                // Remove pending connection
                Clients.TryRemove(ConID, out client);
            }
        }

        /// <summary>
        /// Callback for when a connection had disconnected
        /// </summary>
        /// <param name="sender">The client object whom is disconnecting</param>
        private void GpspClient_OnDisconnect(GpspClient client)
        {
            // Release this stream's AsyncEventArgs to the object pool
            base.Release(client.Stream);
            if (Clients.TryRemove(client.ConnectionID, out client) && !client.Disposed)
                client.Dispose();
        }

        protected override void OnException(Exception e) => ExceptionHandler.GenerateExceptionLog(e);
    }
}

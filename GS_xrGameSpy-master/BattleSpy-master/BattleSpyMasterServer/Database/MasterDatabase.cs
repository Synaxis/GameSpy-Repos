using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using BattleSpy;
using BattleSpy.Database;
using MySql.Data.MySqlClient;

namespace BattlelogMaster
{
    /// <summary>
    /// A class to provide common tasks against the Gamespy Master Database
    /// </summary>
    public sealed class MasterDatabase : DatabaseDriver
    {
        /// <summary>
        /// Our Database connection parameters
        /// </summary>
        private static MySqlConnectionStringBuilder Builder;

        /// <summary>
        /// Builds the conenction string statically, and just once
        /// </summary>
        static MasterDatabase()
        {
            Builder = new MySqlConnectionStringBuilder();
            Builder.Server = Config.GetValue("Database", "Hostname");
            Builder.Port = Config.GetType<uint>("Database", "Port");
            Builder.UserID = Config.GetValue("Database", "Username");
            Builder.Password = Config.GetValue("Database", "Password");
            Builder.Database = Config.GetValue("Database", "MasterDatabase");
            Builder.ConvertZeroDateTime = true;
        }

        /// <summary>
        /// Constructor
        /// </summary>
        public MasterDatabase() : base("Mysql", Builder.ConnectionString)
        {
            // Try and Reconnect
            base.Connect();
        }

        /// <summary>
        /// Destructor
        /// </summary>
        ~MasterDatabase()
        {
            if (!base.IsDisposed)
                base.Dispose();
        }

        public void SyncServerList(ref ConcurrentDictionary<string, GameServer> Servers)
        {
            var Rows = base.Query("SELECT ip, queryport FROM server WHERE online=1");

            foreach (Dictionary<string, object> Row in Rows)
            {
                IPAddress addy;
                if (IPAddress.TryParse(Row["ip"].ToString(), out addy))
                {
                    IPEndPoint endPoint = new IPEndPoint(addy, Int32.Parse(Row["queryport"].ToString()));
                    if(!Servers.ContainsKey(endPoint.ToString()))
                        Servers.TryAdd(endPoint.ToString(), new GameServer(endPoint));
                }
            }
        }

        public void AddOrUpdateServer(GameServer server)
        {
            // Check if server exists in database
            if (base.ExecuteScalar<int>(
                "SELECT COUNT(*) FROM server WHERE ip=@P0 AND queryport=@P1", 
                server.AddressInfo.Address, 
                server.QueryPort) > 0)
            {
                // Update
                base.Execute(
                    "UPDATE server SET online=1, port=@P2, `name`=@P3, lastupdate=@P4 WHERE ip=@P0 AND queryport=@P1",
                    server.AddressInfo.Address,
                    server.QueryPort,
                    server.hostport,
                    server.hostname,
                    server.LastRefreshed.ToUnixTimestamp()
                );
            }
            else
            {
                // Add
                base.Execute(
                    "INSERT INTO server(`name`, `ip`, `port`, `queryport`, `lastupdate`, `authorized`, `online`) VALUES (@P0, @P1, @P2, @P3, @P4, 0, 1)",
                    server.hostname,
                    server.AddressInfo.Address,
                    server.hostport,
                    server.QueryPort,
                    server.LastRefreshed.ToUnixTimestamp()
                );
            }
        }

        public void UpdateServerOffline(GameServer server)
        {
            // Check if server exists in database
            if (base.ExecuteScalar<int>(
                "SELECT COUNT(*) FROM server WHERE ip=@P0 AND queryport=@P1",
                server.AddressInfo.Address,
                server.QueryPort) == 0)
            {
                return;
            }

            // Update
            base.Execute(
                "UPDATE server SET online=0, lastupdate=@P2 WHERE ip=@P0 AND queryport=@P1",
                server.AddressInfo.Address,
                server.QueryPort,
                server.LastRefreshed.ToUnixTimestamp()
            );
        }
    }
}

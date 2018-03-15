using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.IO;
using System.Net;
using BattleSpy.Database;
using Server.Database;

namespace Server
{
    public static class Ip2nation
    {
        /// <summary>
        /// The connection string to the Ip2Nation.db
        /// </summary>
        private static string ConnectionString;

        static Ip2nation()
        {
            try
            {
                // Dont attempt to create, just quit
                string file = Path.Combine(Program.RootPath, "Ip2nation.db");
                if (!File.Exists(file))
                    throw new Exception("Ip2nation.db file is missing!");

                // Create connection string
                SQLiteConnectionStringBuilder Builder = new SQLiteConnectionStringBuilder();
                Builder.DataSource = file;
                Builder.Version = 3;
                Builder.LegacyFormat = false;
                Builder.DefaultTimeout = 500;
                ConnectionString = Builder.ConnectionString;
            }
            catch (Exception e)
            {
                Program.ErrorLog.Write("WARNING: [Ip2nation..ctor] " + e.Message);
            }
        }
        
        /// <summary>
        /// Gets the country code for a string IP address
        /// </summary>
        /// <param name="IP"></param>
        /// <returns></returns>
        public static string GetCountryCode(IPAddress IP)
        {
            try
            {
                using (DatabaseDriver Driver = new DatabaseDriver("Sqlite", ConnectionString))
                {
                    // Fetch country code from Ip2Nation
                    Driver.Connect();
                    List<Dictionary<string, object>> Rows = Driver.Query(
                        "SELECT country FROM ip2nation WHERE ip < @P0 ORDER BY ip DESC LIMIT 1",
                        IP2Long(IP.ToString())
                    );
                    return (Rows.Count == 0) ? "??" : Rows[0]["country"].ToString();
                }
            }
            catch
            {
                return "??";
            }
        }

        /// <summary>
        /// Fethces the full country name from a country code supplied from GetCountryCode()
        /// </summary>
        /// <param name="Code"></param>
        /// <returns></returns>
        public static string GetCountyNameFromCode(string Code)
        {
            try
            {
                using (DatabaseDriver Driver = new DatabaseDriver("Sqlite", ConnectionString))
                {
                    // Fetch country code from Ip2Nation
                    Driver.Connect();
                    List<Dictionary<string, object>> Rows = Driver.Query(
                        "SELECT country FROM ip2nationcountries WHERE iso_code_2 = @P0", Code.ToUpper()
                    );

                    return (Rows.Count == 0) ? Code: Rows[0]["country"].ToString();
                }
            }
            catch
            {
                return Code;
            }
        }

        /// <summary>
        /// Converts a string IP address into MySQL INET_ATOA long
        /// </summary>
        /// <param name="ip">THe IP Address</param>
        /// <see cref="http://geekswithblogs.net/rgupta/archive/2009/04/29/convert-ip-to-long-and-vice-versa-c.aspx"/>
        /// <returns></returns>
        public static long IP2Long(string ip)
        {
            string[] ipBytes;
            double num = 0;
            if (!string.IsNullOrEmpty(ip))
            {
                ipBytes = ip.Split('.');
                for (int i = ipBytes.Length - 1; i >= 0; i--)
                    num += ((int.Parse(ipBytes[i]) % 256) * Math.Pow(256, (3 - i)));
            }

            return (long)num;
        }
    }
}

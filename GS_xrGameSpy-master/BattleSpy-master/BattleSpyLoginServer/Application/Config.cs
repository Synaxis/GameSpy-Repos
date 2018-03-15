using System;
using System.Collections.Generic;
using System.Text;
using System.Runtime.InteropServices;
using System.IO;
using System.Globalization;

namespace Server
{
    /// <summary>
    /// A static config class used to Read and Write to a "Config.ini" file
    /// </summary>
    public static class Config
    {
        // Tapping into the Win32 API to write to INI files.
        [DllImport("kernel32")]
        private static extern long WritePrivateProfileString(string Section, string Key, string Value, string FilePath);

        // Tapping into the Win32 API to read INI files.
        [DllImport("kernel32")]
        private static extern int GetPrivateProfileString(string Section, string Key, string Default, StringBuilder Value, int Length, string FilePath);

        /// <summary>
        /// Ini File Location
        /// </summary>
        private static string IniLocation;

        /// <summary>
        /// Statis constructor makes sure the config file exists, and creates a default
        /// one if need be
        /// </summary>
        static Config()
        {
            // Check if it exists, if not, create the default one.
            IniLocation = Path.Combine(Program.RootPath, "Config.ini");
            if (!File.Exists(IniLocation))
            {
                // Important! INI file must be encoded as ASCII or this wont work!
                string IniString = Program.GetResourceAsString("Server.Config.ini");
                File.WriteAllText(IniLocation, IniString, Encoding.ASCII);
            }
        }

        /// <summary>
        /// Returns a config value as a string
        /// </summary>
        /// <param name="Section">The ini section of this setting</param>
        /// <param name="Key">The setting name</param>
        /// <returns></returns>
        public static string GetValue(string Section, string Key)
        {
            StringBuilder Value = new StringBuilder(1024);
            Config.GetPrivateProfileString(Section, Key, "", Value, 1024, IniLocation);
            return Value.ToString();
        }

        /// <summary>
        /// Returns a config value as the Type indicated
        /// </summary>
        /// <typeparam name="T">The value type (IE: String, Int, Bool)</typeparam>
        /// <param name="Section">The ini section of this setting</param>
        /// <param name="Key">The setting name</param>
        /// <returns></returns>
        public static T GetType<T>(string Section, string Key) where T : IConvertible
        {
            StringBuilder Value = new StringBuilder(1024);
            Config.GetPrivateProfileString(Section, Key, "", Value, 1024, IniLocation);

            return (T)Convert.ChangeType(Value.ToString(), typeof(T), CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// Sets a config value as the Type Indicated
        /// </summary>
        /// <param name="Section">The ini section of this setting</param>
        /// <param name="Key">The setting name</param>
        /// <param name="Value">The new setting value</param>
        public static void SetValue(string Section, string Key, object Value)
        {
            Config.WritePrivateProfileString(Section, Key, Value.ToString(), IniLocation);
        }
    }
}

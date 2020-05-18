using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace DBTools
{
    /// Configuration class based on Mistware.Utils.Config - this code should be merged back in at next Utils update
    public static class Configuration
    {
        private static Dictionary<string,string> settings = new Dictionary<string, string>();

        /// Name of the json configuration file
        public static string ConfigFile { get; private set; }

        /// <summary>
        /// Add or update a setting in the settings dictionary.
        /// </summary>
        /// <param name="key">Name of the setting.</param>        
        /// <param name="value">Value of the setting.</param>
        public static void Set(string key, string value)
        {
            if (key != null || value != null)
            {
                if (settings.ContainsKey(key)) settings[key] = value;
                else                           settings.Add(key, value);
            } 
        }

        /// <summary>
        /// Get a setting. If it is not in the settings dictionary, then the environment variables dictionary
        /// will also be searched.
        /// </summary>
        /// <param name="key">Name of the setting.</param>        
        /// <returns>Value of the setting, or null if not set.</returns>
        public static string Get(string key)
        {
            if (key == null) return null;

            if (settings.ContainsKey(key)) return (string)settings[key];
            else                           return null;
        }

         /// Load the configuration
        public static void ReadJsonConfig(string configFile)
        {
            if (File.Exists(configFile))
            {
                string json = File.ReadAllText(configFile);
            
                using (JsonDocument document = JsonDocument.Parse(json))
                {
                    foreach (JsonProperty prop in document.RootElement.EnumerateObject())
                    {
                        string key = prop.Name;
                        string valuekind = prop.Value.ValueKind.ToString();
                        if      (valuekind == "String")  Set(key, prop.Value.GetString());
                        else if (valuekind == "Number")  Set(key, prop.Value.GetInt64().ToString());
                    }
                }
            } 
            ConfigFile = configFile;   
        }

        /// Update the configuration
        public static void WriteJsonConfig()
        {
            var options = new JsonSerializerOptions { WriteIndented = true };
            string json = JsonSerializer.Serialize(settings, options);
            File.WriteAllText(ConfigFile, json);
        }
    }
}
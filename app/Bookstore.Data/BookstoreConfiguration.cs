using System;
using System.Collections.Generic;
using Microsoft.Extensions.Configuration;

namespace BobsBookstoreClassic.Data
{
    public sealed class BookstoreConfiguration
    {
        private static readonly Lazy<BookstoreConfiguration> Lazy = new Lazy<BookstoreConfiguration>(() => new BookstoreConfiguration());

        private static BookstoreConfiguration Instance => Lazy.Value;

        private readonly Dictionary<string, string> _appSettings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, string> _connectionStrings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        private BookstoreConfiguration() { }

        /// <summary>
        /// Initialise from ASP.NET Core IConfiguration. Call once at startup from Program.cs.
        /// </summary>
        public static void Initialize(IConfiguration configuration)
        {
            foreach (var kvp in configuration.AsEnumerable())
            {
                if (kvp.Value == null) continue;
                Instance._appSettings[kvp.Key] = kvp.Value;
            }

            // Load connection strings section
            var connStrings = configuration.GetSection("ConnectionStrings");
            if (connStrings != null)
            {
                foreach (var child in connStrings.GetChildren())
                {
                    Instance._connectionStrings[child.Key] = child.Value ?? string.Empty;
                }
            }

            // Also apply environment variable overrides for keys that match appSettings
            foreach (var key in Instance._appSettings.Keys)
            {
                var envKey = key.Replace("/", "__").Replace(":", "__");
                var envValue = Environment.GetEnvironmentVariable(envKey);
                if (envValue != null)
                {
                    Instance._appSettings[key] = envValue;
                }
            }
        }

        public static void AddSetting(string key, string value)
        {
            Instance._appSettings[key] = value;
        }

        public static string GetSetting(string key)
        {
            if (Instance._appSettings.TryGetValue(key, out var value))
                return value;

            // Fallback: try environment variable
            var envKey = key.Replace("/", "__").Replace(":", "__");
            return Environment.GetEnvironmentVariable(envKey) ?? string.Empty;
        }

        public static T GetSetting<T>(string key)
        {
            var value = GetSetting(key);
            return (T)Convert.ChangeType(value, typeof(T));
        }

        public static void AddConnectionString(string key, string value)
        {
            Instance._connectionStrings[key] = value;
        }

        public static string GetConnectionString(string key)
        {
            if (Instance._connectionStrings.TryGetValue(key, out var value))
                return value;
            return string.Empty;
        }
    }
}

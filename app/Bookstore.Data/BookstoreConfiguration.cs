using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;

namespace BobsBookstoreClassic.Data
{
    public sealed class BookstoreConfiguration
    {
        private static readonly Lazy<BookstoreConfiguration> Lazy = new Lazy<BookstoreConfiguration>(() => new BookstoreConfiguration());

        private static BookstoreConfiguration Instance => Lazy.Value;

        private readonly Dictionary<string, string> _appSettings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, string> _connectionStrings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        private BookstoreConfiguration() { }

        public static void Configure(IConfiguration configuration)
        {
            foreach (var child in configuration.GetChildren())
            {
                if (child.Key.Equals("ConnectionStrings", StringComparison.OrdinalIgnoreCase))
                {
                    foreach (var cs in child.GetChildren())
                    {
                        Instance._connectionStrings[cs.Key] = cs.Value ?? string.Empty;
                    }
                }
                else
                {
                    RecurseSection(child, string.Empty);
                }
            }

            foreach (var entry in Environment.GetEnvironmentVariables().Keys)
            {
                var key = entry?.ToString() ?? string.Empty;
                var value = Environment.GetEnvironmentVariable(key);
                if (!string.IsNullOrEmpty(key) && value != null)
                {
                    Instance._appSettings[key] = value;
                }
            }
        }

        private static void RecurseSection(IConfigurationSection section, string prefix)
        {
            var children = section.GetChildren();
            bool hasChildren = false;
            foreach (var child in children)
            {
                hasChildren = true;
                var childKey = string.IsNullOrEmpty(prefix) ? $"{section.Key}/{child.Key}" : $"{prefix}/{section.Key}/{child.Key}";
                RecurseSection(child, string.IsNullOrEmpty(prefix) ? section.Key : $"{prefix}/{section.Key}");
            }

            if (!hasChildren && section.Value != null)
            {
                var fullKey = string.IsNullOrEmpty(prefix) ? section.Key : $"{prefix}/{section.Key}";
                Instance._appSettings[fullKey] = section.Value;
            }
        }

        public static void AddSetting(string key, string value)
        {
            Instance._appSettings[key] = value;
        }

        public static string GetSetting(string key)
        {
            return Instance._appSettings.TryGetValue(key, out var val) ? val : string.Empty;
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
            return Instance._connectionStrings.TryGetValue(key, out var val) ? val : string.Empty;
        }
    }
}

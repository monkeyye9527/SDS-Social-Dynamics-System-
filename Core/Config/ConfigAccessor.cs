using ONIModPack.Core.Services;
using System;

namespace ONIModPack.Core.Config
{
    public static class ConfigAccessor
    {
        private static IConfigManager _configManager;

        private static IConfigManager ConfigManager
        {
            get
            {
                if (_configManager == null)
                {
                    _configManager = ServiceResolver.OptionalService<IConfigManager>();
                }
                return _configManager;
            }
        }

        public static bool IsModuleEnabled(string moduleId, bool defaultValue = false)
        {
            return ConfigManager?.IsModuleEnabled(moduleId, defaultValue) ?? defaultValue;
        }

        public static void SetModuleEnabled(string moduleId, bool enabled)
        {
            ConfigManager?.SetModuleEnabled(moduleId, enabled);
        }

        public static T GetConfigValue<T>(string moduleId, string key, T defaultValue = default)
        {
            var manager = ConfigManager;
            return manager != null ? manager.GetConfigValue(moduleId, key, defaultValue) : defaultValue;
        }

        public static void SetConfigValue<T>(string moduleId, string key, T value)
        {
            ConfigManager?.SetConfigValue(moduleId, key, value);
        }

        public static string GetString(string moduleId, string key, string defaultValue = "")
        {
            return GetConfigValue(moduleId, key, defaultValue);
        }

        public static int GetInt(string moduleId, string key, int defaultValue = 0)
        {
            return GetConfigValue(moduleId, key, defaultValue);
        }

        public static float GetFloat(string moduleId, string key, float defaultValue = 0f)
        {
            return GetConfigValue(moduleId, key, defaultValue);
        }

        public static bool GetBool(string moduleId, string key, bool defaultValue = false)
        {
            return GetConfigValue(moduleId, key, defaultValue);
        }

        public static T GetEnum<T>(string moduleId, string key, T defaultValue = default) where T : struct, Enum
        {
            var stringValue = GetString(moduleId, key);
            if (Enum.TryParse(stringValue, true, out T result))
            {
                return result;
            }
            return defaultValue;
        }

        public static void SetString(string moduleId, string key, string value)
        {
            SetConfigValue(moduleId, key, value);
        }

        public static void SetInt(string moduleId, string key, int value)
        {
            SetConfigValue(moduleId, key, value);
        }

        public static void SetFloat(string moduleId, string key, float value)
        {
            SetConfigValue(moduleId, key, value);
        }

        public static void SetBool(string moduleId, string key, bool value)
        {
            SetConfigValue(moduleId, key, value);
        }

        public static void SetEnum<T>(string moduleId, string key, T value) where T : struct, Enum
        {
            SetString(moduleId, key, value.ToString());
        }

        public static T GetSection<T>(string moduleId) where T : class, new()
        {
            return ConfigManager?.GetConfig<T>(moduleId, string.Empty, new T()) ?? new T();
        }

        public static void SubscribeToChanges(Action<string> handler)
        {
            if (ConfigManager != null)
            {
                ConfigManager.OnConfigChanged += handler;
            }
        }

        public static void UnsubscribeFromChanges(Action<string> handler)
        {
            if (ConfigManager != null)
            {
                ConfigManager.OnConfigChanged -= handler;
            }
        }

        public static void Reload()
        {
            ConfigManager?.Reload();
        }

        public static void Save()
        {
            ConfigManager?.Save();
        }
    }
}
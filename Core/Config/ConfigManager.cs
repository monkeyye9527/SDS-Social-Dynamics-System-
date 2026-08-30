using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using ONIModPack.Core.Utils;

namespace ONIModPack.Core.Config
{
    public class ConfigManager : IConfigManager
    {
        public string ConfigDirectory { get; private set; }

        private readonly Dictionary<string, bool> _moduleStates = new Dictionary<string, bool>();
        private readonly Dictionary<string, ModuleConfigData> _moduleConfigs = new Dictionary<string, ModuleConfigData>();
        private readonly object _configLock = new object();
        private FileSystemWatcher _watcher;
        private bool _disposed;
        private CancellationTokenSource _reloadToken;
        private bool _isInitialized;

        public event Action<string> OnConfigChanged;
        
        public bool IsInitialized => _isInitialized;

        private const string GlobalConfigFile = "global.yaml";

        public void Initialize()
        {
            Load();
            _isInitialized = true;
        }

        public void Shutdown()
        {
            SaveAll();
        }

        public void Load()
        {
            ConfigDirectory = Path.Combine(Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location), "config");

            if (!Directory.Exists(ConfigDirectory))
            {
                Directory.CreateDirectory(ConfigDirectory);
            }

            var globalPath = Path.Combine(ConfigDirectory, GlobalConfigFile);

            if (File.Exists(globalPath))
            {
                LoadGlobalConfig(globalPath);
            }
            else
            {
                CreateDefaultConfig(globalPath);
            }

            ONIModPack.Core.Logger.Info($"Config loaded from: {ConfigDirectory}");
        }

        public void EnableHotReload()
        {
            if (_watcher != null) return;

            _watcher = new FileSystemWatcher(ConfigDirectory, "*.yaml")
            {
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName,
                EnableRaisingEvents = true
            };

            _watcher.Changed += OnConfigFileChanged;
            _watcher.Created += OnConfigFileChanged;
            ONIModPack.Core.Logger.Info("Config hot-reload enabled");
        }

        public bool IsModuleEnabled(string moduleId, bool defaultValue = false)
        {
            lock (_configLock)
            {
                return _moduleStates.TryGetValue(moduleId, out var enabled) ? enabled : defaultValue;
            }
        }

        public void SetModuleEnabled(string moduleId, bool enabled)
        {
            lock (_configLock)
            {
                _moduleStates[moduleId] = enabled;
            }
            SaveGlobalConfig();
        }

        public T GetConfig<T>(string moduleId, string key, T defaultValue = default) where T : class
        {
            lock (_configLock)
            {
                if (_moduleConfigs.TryGetValue(moduleId, out var config))
                {
                    return config.GetValue(key, defaultValue);
                }
            }
            return defaultValue;
        }

        public T GetConfigValue<T>(string moduleId, string key, T defaultValue = default)
        {
            lock (_configLock)
            {
                if (_moduleConfigs.TryGetValue(moduleId, out var config))
                {
                    return config.GetValue(key, defaultValue);
                }
            }
            return defaultValue;
        }

        public void SetConfigValue<T>(string moduleId, string key, T value)
        {
            lock (_configLock)
            {
                if (!_moduleConfigs.TryGetValue(moduleId, out var config))
                {
                    config = new ModuleConfigData();
                    _moduleConfigs[moduleId] = config;
                }
                config.SetValue(key, value);
            }
        }

        public void Save()
        {
            SaveAll();
        }

        public void Reload()
        {
            _moduleStates.Clear();
            _moduleConfigs.Clear();
            Load();
        }

        public void SaveAll()
        {
            SaveGlobalConfig();
            SaveModuleConfigs();
        }

        private async void OnConfigFileChanged(object sender, FileSystemEventArgs e)
        {
            _reloadToken?.Cancel();
            _reloadToken = new CancellationTokenSource();

            try
            {
                await Task.Delay(200, _reloadToken.Token);

                ONIModPack.Core.Logger.Info($"Config file changed: {e.Name}");

                var configId = Path.GetFileNameWithoutExtension(e.Name);

                ConfigMigrationManager.Reset(configId);

                if (e.Name == GlobalConfigFile)
                {
                    lock (_configLock)
                    {
                        LoadGlobalConfig(e.FullPath);
                    }
                }
                else
                {
                    var moduleId = Path.GetFileNameWithoutExtension(e.Name);
                    lock (_configLock)
                    {
                        LoadModuleConfig(moduleId, e.FullPath);
                    }
                }

                ConfigMigrationManager.Apply(configId);
                OnConfigChanged?.Invoke(configId);
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                ONIModPack.Core.Logger.Exception(ex, "Error reloading config");
            }
        }

        private void LoadGlobalConfig(string path)
        {
            try
            {
                var content = File.ReadAllText(path);
                var config = YamlLoader.Deserialize<GlobalConfig>(content);

                lock (_configLock)
                {
                    if (config?.Modules != null)
                    {
                        foreach (var module in config.Modules)
                        {
                            _moduleStates[module.Key] = module.Value.Enabled;
                        }
                    }
                }

                ApplyLoggingConfig(config?.Logging);

                ONIModPack.Core.Logger.Debug("Global config loaded");
            }
            catch (Exception ex)
            {
                ONIModPack.Core.Logger.Exception(ex, "Failed to load global config");
            }
        }

        private void LoadModuleConfig(string moduleId, string path)
        {
            try
            {
                var content = File.ReadAllText(path);
                var dynamicData = YamlLoader.DeserializeDynamic(content) as Dictionary<string, object>;
                var config = ModuleConfigData.FromDictionary(dynamicData);
                
                lock (_configLock)
                {
                    _moduleConfigs[moduleId] = config;
                }
                
                ONIModPack.Core.Logger.Debug($"Module config loaded: {moduleId}");
            }
            catch (Exception ex)
            {
                ONIModPack.Core.Logger.Exception(ex, $"Failed to load config for {moduleId}");
            }
        }

        private static void ApplyLoggingConfig(LoggingConfig logging)
        {
            logging = logging ?? new LoggingConfig();
            var level = Enum.TryParse(logging.Level, true, out LogLevel parsed) ? parsed : LogLevel.Info;
            long maxBytes = Math.Max(1, logging.MaxFileSizeMB) * 1024L * 1024L;
            ModLogger.Configure(level, maxBytes);
        }

        private void CreateDefaultConfig(string path)
        {
            var defaultConfig = new GlobalConfig
            {
                Version = "1.0",
                Modules = new Dictionary<string, ModuleStateEntry>(),
                Logging = new LoggingConfig()
            };

            defaultConfig.Modules["QoL.Automation"] = new ModuleStateEntry { Enabled = true };
            defaultConfig.Modules["QoL.UI"] = new ModuleStateEntry { Enabled = true };
            defaultConfig.Modules["QoL.Convenience"] = new ModuleStateEntry { Enabled = true };
            defaultConfig.Modules["Balance.Difficulty"] = new ModuleStateEntry { Enabled = false };
            defaultConfig.Modules["Balance.Resources"] = new ModuleStateEntry { Enabled = false };
            defaultConfig.Modules["Content.Buildings"] = new ModuleStateEntry { Enabled = false };

            var yaml = YamlSerializer.Serialize(defaultConfig);
            File.WriteAllText(path, yaml);
        }

        private void SaveGlobalConfig()
        {
            try
            {
                var config = new GlobalConfig
                {
                    Version = "1.0",
                    Modules = new Dictionary<string, ModuleStateEntry>()
                };

                lock (_configLock)
                {
                    foreach (var state in _moduleStates)
                    {
                        config.Modules[state.Key] = new ModuleStateEntry { Enabled = state.Value };
                    }
                }

                var path = Path.Combine(ConfigDirectory, GlobalConfigFile);
                File.WriteAllText(path, YamlSerializer.Serialize(config));
            }
            catch (Exception ex)
            {
                ONIModPack.Core.Logger.Exception(ex, "Failed to save global config");
            }
        }

        private void SaveModuleConfigs()
        {
            lock (_configLock)
            {
                foreach (var kvp in _moduleConfigs)
                {
                    try
                    {
                        var path = Path.Combine(ConfigDirectory, $"{kvp.Key}.yaml");
                        File.WriteAllText(path, YamlSerializer.Serialize(kvp.Value.ToDictionary()));
                    }
                    catch (Exception ex)
                    {
                        ONIModPack.Core.Logger.Exception(ex, $"Failed to save config for {kvp.Key}");
                    }
                }
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            _reloadToken?.Cancel();
            _reloadToken?.Dispose();
            _reloadToken = null;

            if (_watcher != null)
            {
                _watcher.EnableRaisingEvents = false;
                _watcher.Dispose();
                _watcher = null;
            }

            SaveAll();
            ONIModPack.Core.Logger.Debug("ConfigManager disposed");
        }
    }

    [Serializable]
    public class GlobalConfig
    {
        public string Version { get; set; }
        public Dictionary<string, ModuleStateEntry> Modules { get; set; }
        public LoggingConfig Logging { get; set; }
    }

    [Serializable]
    public class LoggingConfig
    {
        public string Level { get; set; } = "Info";
        public int MaxFileSizeMB { get; set; } = 10;
    }

    [Serializable]
    public class ModuleStateEntry
    {
        public bool Enabled { get; set; }
    }

    [Serializable]
    public class ModuleConfigData
    {
        private readonly Dictionary<string, object> _data = new Dictionary<string, object>();

        public T GetValue<T>(string key, T defaultValue = default)
        {
            if (_data.TryGetValue(key, out var value))
            {
                if (value == null)
                {
                    return defaultValue;
                }

                var targetType = typeof(T);

                if (value is T directValue)
                {
                    return directValue;
                }

                try
                {
                    if (targetType.IsEnum)
                    {
                        if (value is string enumString)
                        {
                            return (T)Enum.Parse(targetType, enumString);
                        }
                        if (value is int enumInt)
                        {
                            return (T)Enum.ToObject(targetType, enumInt);
                        }
                    }

                    return (T)Convert.ChangeType(value, targetType);
                }
                catch (Exception ex)
                {
                    ONIModPack.Core.Logger.Warning($"[Config] Failed to convert value for key '{key}': {ex.Message}. Using default.");
                    return defaultValue;
                }
            }
            return defaultValue;
        }

        public void SetValue<T>(string key, T value)
        {
            if (value == null)
            {
                _data.Remove(key);
                return;
            }

            if (typeof(T).IsEnum)
            {
                _data[key] = value.ToString();
            }
            else
            {
                _data[key] = value;
            }
        }

        public static ModuleConfigData FromDictionary(Dictionary<string, object> data)
        {
            var config = new ModuleConfigData();
            if (data != null)
            {
                foreach (var kv in data)
                    config._data[kv.Key] = kv.Value;
            }
            return config;
        }

        public Dictionary<string, object> ToDictionary()
        {
            return new Dictionary<string, object>(_data);
        }
    }
}
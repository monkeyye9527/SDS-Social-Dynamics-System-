using System;
using ONIModPack.Core.Services;

namespace ONIModPack.Core.Config
{
    public interface IConfigManager : IService, IDisposable
    {
        string ConfigDirectory { get; }
        
        bool IsModuleEnabled(string moduleId, bool defaultValue = false);
        void SetModuleEnabled(string moduleId, bool enabled);
        
        T GetConfig<T>(string moduleId, string key, T defaultValue = default) where T : class;
        T GetConfigValue<T>(string moduleId, string key, T defaultValue = default);
        void SetConfigValue<T>(string moduleId, string key, T value);
        
        void Load();
        void Save();
        void Reload();
        
        event Action<string> OnConfigChanged;
    }
    
    public interface IModuleConfig<T> where T : class
    {
        string ModuleId { get; }
        T GetConfig();
        void LoadFromConfig();
        void SaveToConfig();
    }
}
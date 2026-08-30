using System;
using System.Collections.Generic;

namespace ONIModPack.Core.Services
{
    public interface ISaveable
    {
        string SaveId { get; }
        void Save(Dictionary<string, object> data);
        void Load(Dictionary<string, object> data);
        void Reset();
    }

    public class SaveDataManagerV2 : IService
    {
        private readonly List<ISaveable> _saveables = new List<ISaveable>();
        public bool IsInitialized { get; private set; }

        public void Initialize()
        {
            if (IsInitialized) return;
            ModLogger.Info("[SaveDataManager] Initialized v2");
            IsInitialized = true;
        }

        public void Shutdown()
        {
            _saveables.Clear();
            IsInitialized = false;
        }

        public void RegisterSaveable(ISaveable saveable)
        {
            if (!_saveables.Contains(saveable))
            {
                _saveables.Add(saveable);
                ModLogger.Debug($"[SaveDataManager] Registered saveable: {saveable.SaveId}");
            }
        }

        public Dictionary<string, Dictionary<string, object>> SaveAll()
        {
            var data = new Dictionary<string, Dictionary<string, object>>();
            foreach (var saveable in _saveables)
            {
                var saveData = new Dictionary<string, object>();
                saveable.Save(saveData);
                data[saveable.SaveId] = saveData;
            }
            return data;
        }

        public void LoadAll(Dictionary<string, Dictionary<string, object>> data)
        {
            foreach (var saveable in _saveables)
            {
                if (data.TryGetValue(saveable.SaveId, out var saveData))
                {
                    saveable.Load(saveData);
                }
            }
        }
    }
}


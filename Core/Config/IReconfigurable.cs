using System;
using System.Collections.Generic;

namespace ONIModPack.Core
{
    public interface IReconfigurable
    {
        void ApplyConfig();
        void Rollback();
        string ConfigId { get; }
    }

    public interface IConfigSubscriber
    {
        void UnsubscribeFromConfig();
        void ResubscribeToConfig();
    }

    public class ConfigMigration
    {
        private readonly IReconfigurable _target;
        private readonly IConfigSubscriber _subscriber;
        private bool _isInitialized;

        public ConfigMigration(IReconfigurable target, IConfigSubscriber subscriber)
        {
            _target = target;
            _subscriber = subscriber;
        }

        public void Migrate()
        {
            if (_isInitialized && _subscriber != null)
            {
                _subscriber.UnsubscribeFromConfig();
            }

            _target.ApplyConfig();
            _isInitialized = true;
        }

        public void Reset()
        {
            _target.Rollback();
            _isInitialized = false;
        }
    }

    public class ConfigMigrationManager
    {
        private static readonly Dictionary<string, ConfigMigration> _migrations = new Dictionary<string, ConfigMigration>();

        public static void Register(string configId, IReconfigurable target, IConfigSubscriber subscriber)
        {
            if (_migrations.ContainsKey(configId))
            {
                Logger.Warning($"[ConfigMigration] ConfigId '{configId}' already registered, skipping");
                return;
            }

            _migrations[configId] = new ConfigMigration(target, subscriber);
        }

        public static void Apply(string configId)
        {
            if (_migrations.TryGetValue(configId, out var migration))
            {
                migration.Migrate();
            }
        }

        public static void ApplyAll()
        {
            foreach (var migration in _migrations.Values)
            {
                migration.Migrate();
            }
        }

        public static void Reset(string configId)
        {
            if (_migrations.TryGetValue(configId, out var migration))
            {
                migration.Reset();
            }
        }

        public static void ResetAll()
        {
            foreach (var migration in _migrations.Values)
            {
                migration.Reset();
            }
        }

        public static void Unregister(string configId)
        {
            _migrations.Remove(configId);
        }

        public static void Clear()
        {
            _migrations.Clear();
        }
    }

    public interface IConfigValidator
    {
        bool Validate(string configId, object configData);
        string GetValidationError();
    }

    public class ConfigTransaction : IDisposable
    {
        private readonly string _configId;
        private readonly IReconfigurable _target;
        private readonly IConfigValidator _validator;
        private object _snapshot;
        private object _pendingData;
        private bool _disposed;
        private bool _committed;

        public string ConfigId => _configId;
        public bool IsValid { get; private set; }
        public string ValidationError { get; private set; }
        public bool IsDisposed => _disposed;

        public ConfigTransaction(string configId, IReconfigurable target, IConfigValidator validator = null)
        {
            _configId = configId;
            _target = target;
            _validator = validator;
            _snapshot = TakeSnapshot();
        }

        public void Load(object configData)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(ConfigTransaction));
            _pendingData = configData;
        }

        public bool Validate()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(ConfigTransaction));
            if (_pendingData == null)
            {
                IsValid = false;
                ValidationError = "No config data loaded";
                return false;
            }

            if (_validator != null)
            {
                IsValid = _validator.Validate(_configId, _pendingData);
                ValidationError = IsValid ? null : _validator.GetValidationError();
            }
            else
            {
                IsValid = true;
            }

            return IsValid;
        }

        public bool Commit()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(ConfigTransaction));
            if (_committed) return true;
            if (!IsValid) return false;

            try
            {
                _target.ApplyConfig();
                _committed = true;
                _snapshot = null;
                _pendingData = null;
                return true;
            }
            catch (Exception ex)
            {
                Logger.Error($"[ConfigTransaction] Commit failed for {_configId}: {ex.Message}");
                Rollback();
                return false;
            }
        }

        public void Rollback()
        {
            if (_disposed) return;
            if (_snapshot != null)
            {
                try
                {
                    _target.Rollback();
                }
                catch (Exception ex)
                {
                    Logger.Error($"[ConfigTransaction] Rollback failed for {_configId}: {ex.Message}");
                }
            }
            _pendingData = null;
            _committed = false;
        }

        private object TakeSnapshot()
        {
            return null;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            if (!_committed && _snapshot != null)
            {
                Rollback();
            }
            _snapshot = null;
            _pendingData = null;
        }
    }

    public class ConfigTransactionManager
    {
        private static readonly Dictionary<string, ConfigTransaction> _activeTransactions = new Dictionary<string, ConfigTransaction>();

        public static ConfigTransaction BeginTransaction(string configId, IReconfigurable target, IConfigValidator validator = null)
        {
            if (_activeTransactions.ContainsKey(configId))
            {
                Logger.Warning($"[ConfigTransactionManager] Transaction already active for {configId}");
                return null;
            }

            var transaction = new ConfigTransaction(configId, target, validator);
            _activeTransactions[configId] = transaction;
            return transaction;
        }

        public static ConfigTransaction GetTransaction(string configId)
        {
            return _activeTransactions.TryGetValue(configId, out var tx) ? tx : null;
        }

        public static bool CommitTransaction(string configId)
        {
            if (!_activeTransactions.TryGetValue(configId, out var tx)) return false;
            bool result = tx.Commit();
            if (result || tx.IsDisposed)
            {
                _activeTransactions.Remove(configId);
                tx.Dispose();
            }
            return result;
        }

        public static void RollbackTransaction(string configId)
        {
            if (!_activeTransactions.TryGetValue(configId, out var tx)) return;
            tx.Rollback();
            _activeTransactions.Remove(configId);
            tx.Dispose();
        }

        public static void DisposeTransaction(string configId)
        {
            if (_activeTransactions.TryGetValue(configId, out var tx))
            {
                _activeTransactions.Remove(configId);
                tx.Dispose();
            }
        }
    }
}


using System;

namespace ONIModPack.Core
{
    public interface ISaveMigration
    {
        int FromVersion { get; }
        int ToVersion { get; }
        bool Migrate(ISaveData data);
    }

    public interface ISaveData
    {
        int Version { get; set; }
        string SerializedData { get; set; }
    }

    public interface ISaveSection
    {
        string SectionId { get; }
        void Serialize(ISaveWriter writer);
        void Deserialize(ISaveReader reader);
    }

    public interface ISaveWriter
    {
        void WriteString(string key, string value);
        void WriteInt(string key, int value);
        void WriteFloat(string key, float value);
        void WriteBool(string key, bool value);
        void WriteSection(string sectionId, Action<ISaveWriter> writer);
    }

    public interface ISaveReader
    {
        string ReadString(string key, string defaultValue = null);
        int ReadInt(string key, int defaultValue = 0);
        float ReadFloat(string key, float defaultValue = 0f);
        bool ReadBool(string key, bool defaultValue = false);
        void ReadSection(string sectionId, Action<ISaveReader> reader);
    }

    public class SaveMigrationManager
    {
        private static readonly ISaveMigration[] _migrations = new ISaveMigration[0];
        private static readonly int _currentVersion = 64;

        public static int CurrentVersion => _currentVersion;

        public static bool MigrateIfNeeded(ISaveData saveData)
        {
            if (saveData == null) return false;

            int fromVersion = saveData.Version;
            if (fromVersion >= _currentVersion) return true;

            var migrations = GetMigrationsForRange(fromVersion, _currentVersion);
            foreach (var migration in migrations)
            {
                if (!migration.Migrate(saveData))
                {
                    Logger.Error($"[SaveMigration] Failed to migrate from {migration.FromVersion} to {migration.ToVersion}");
                    return false;
                }

                saveData.Version = migration.ToVersion;
                Logger.Info($"[SaveMigration] Migrated from {migration.FromVersion} to {migration.ToVersion}");
            }

            return true;
        }

        private static ISaveMigration[] GetMigrationsForRange(int from, int to)
        {
            return new ISaveMigration[0];
        }

        public static ISaveData CreateSaveData(string serializedData)
        {
            return new SaveDataWrapper
            {
                Version = _currentVersion,
                SerializedData = serializedData
            };
        }

        private class SaveDataWrapper : ISaveData
        {
            public int Version { get; set; }
            public string SerializedData { get; set; }
        }
    }
}


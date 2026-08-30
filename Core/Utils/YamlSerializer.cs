using System;
using System.IO;
using YamlDotNet.Serialization;

namespace ONIModPack.Core.Utils
{
    /// <summary>
    /// YAML serializer using game-embedded YamlDotNet.
    /// </summary>
    public static class YamlSerializer
    {
        private static readonly Serializer _serializer;

        static YamlSerializer()
        {
            // Use game-embedded YamlDotNet (older API)
            _serializer = new Serializer();
        }

        /// <summary>Serialize object to YAML string.</summary>
        public static string Serialize(object obj)
        {
            if (obj == null)
                return "null\n";

            try
            {
                return _serializer.Serialize(obj);
            }
            catch (Exception ex)
            {
                ModLogger.Exception(ex, $"[YamlSerializer] Serialize failed for {obj.GetType().Name}");
                return string.Empty;
            }
        }

        /// <summary>Save object to YAML file.</summary>
        public static void SaveToFile(object obj, string path)
        {
            try
            {
                string directory = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                string yaml = Serialize(obj);
                File.WriteAllText(path, yaml);
            }
            catch (Exception ex)
            {
                ModLogger.Exception(ex, $"[YamlSerializer] SaveToFile failed: {path}");
            }
        }
    }
}
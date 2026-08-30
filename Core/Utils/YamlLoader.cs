using System;
using System.Collections.Generic;
using System.IO;
using YamlDotNet.Serialization;

namespace ONIModPack.Core.Utils
{
    /// <summary>
    /// YAML loader using game-embedded YamlDotNet for robust parsing.
    /// Supports: anchors, references, flow style, block scalars, multi-document.
    /// </summary>
    public static class YamlLoader
    {
        private static readonly Deserializer _deserializer;
        private static readonly Deserializer _dynamicDeserializer;

        static YamlLoader()
        {
            // Use game-embedded YamlDotNet (older API)
            _deserializer = new Deserializer();
            _dynamicDeserializer = new Deserializer();
        }

        /// <summary>Load YAML from file and deserialize to type T.</summary>
        public static T LoadFromFile<T>(string path) where T : new()
        {
            if (!File.Exists(path))
            {
                ModLogger.Warning($"[YamlLoader] File not found: {path}");
                return new T();
            }

            try
            {
                string content = File.ReadAllText(path);
                return Deserialize<T>(content);
            }
            catch (Exception ex)
            {
                ModLogger.Exception(ex, $"[YamlLoader] Failed to load: {path}");
                return default;
            }
        }

        /// <summary>Deserialize YAML string to type T.</summary>
        public static T Deserialize<T>(string yaml) where T : new()
        {
            if (string.IsNullOrWhiteSpace(yaml))
                return new T();

            try
            {
                return _deserializer.Deserialize<T>(yaml);
            }
            catch (Exception ex)
            {
                ModLogger.Exception(ex, $"[YamlLoader] Deserialize<{typeof(T).Name}> failed");
                return default;
            }
        }

        /// <summary>Deserialize YAML to dynamic object (Dictionary or List).</summary>
        public static object DeserializeDynamic(string yaml)
        {
            if (string.IsNullOrWhiteSpace(yaml))
                return null;

            try
            {
                return _dynamicDeserializer.Deserialize<object>(yaml);
            }
            catch (Exception ex)
            {
                ModLogger.Exception(ex, "[YamlLoader] DeserializeDynamic failed");
                return null;
            }
        }

        /// <summary>Parse simple YAML list.</summary>
        public static List<string> ParseYamlList(string yaml)
        {
            if (string.IsNullOrWhiteSpace(yaml))
                return new List<string>();

            try
            {
                var list = _deserializer.Deserialize<List<string>>(yaml);
                return list ?? new List<string>();
            }
            catch (Exception ex)
            {
                ModLogger.Warning($"[YamlLoader] ParseYamlList fallback due to: {ex.Message}");
                // Fallback to simple parsing
                var result = new List<string>();
                var lines = yaml.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (var line in lines)
                {
                    string trimmed = line.Trim();
                    if (trimmed.StartsWith("- "))
                        result.Add(trimmed.Substring(2).Trim());
                }
                return result;
            }
        }

        /// <summary>Parse simple YAML dictionary.</summary>
        public static Dictionary<string, string> ParseYamlDictionary(string yaml)
        {
            if (string.IsNullOrWhiteSpace(yaml))
                return new Dictionary<string, string>();

            try
            {
                var dict = _deserializer.Deserialize<Dictionary<string, string>>(yaml);
                return dict ?? new Dictionary<string, string>();
            }
            catch (Exception ex)
            {
                ModLogger.Warning($"[YamlLoader] ParseYamlDictionary fallback due to: {ex.Message}");
                // Fallback to simple parsing
                var result = new Dictionary<string, string>();
                var lines = yaml.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (var line in lines)
                {
                    string trimmed = line.Trim();
                    if (trimmed.StartsWith("#"))
                        continue;

                    if (trimmed.Contains(":"))
                    {
                        int colonIndex = trimmed.IndexOf(':');
                        string key = trimmed.Substring(0, colonIndex).Trim();
                        string value = trimmed.Substring(colonIndex + 1).Trim();
                        result[key] = value;
                    }
                }
                return result;
            }
        }
    }
}
// =====================================================================================================
// DEPRECATED: This file is obsolete and kept for backward compatibility only.
// =====================================================================================================
// Please use ONIModPack.Core.Utils namespace instead:
//   - YamlLoader    : Load/Parse YAML files
//   - YamlSerializer: Serialize objects to YAML
//   - YamlValidator : Validate YAML structure
//   - YamlCache     : Cache loaded YAML data
// =====================================================================================================

#pragma warning disable CS0618 // Type or member is obsolete

using System;
using System.Collections.Generic;
using System.IO;

namespace ONIModPack.Core
{
    [Obsolete("Use ONIModPack.Core.Utils namespace instead")]
    public static class YamlHelper
    {
        [Obsolete("Use YamlLoader.Deserialize<T>()")]
        public static T Deserialize<T>(string yaml) where T : new()
            => Utils.YamlLoader.Deserialize<T>(yaml);

        [Obsolete("Use YamlLoader.DeserializeDynamic()")]
        public static object DeserializeDynamic(string yaml)
            => Utils.YamlLoader.DeserializeDynamic(yaml);

        [Obsolete("Use YamlSerializer.Serialize()")]
        public static string Serialize(object obj)
            => Utils.YamlSerializer.Serialize(obj);

        [Obsolete("Use YamlLoader.LoadFromFile<T>()")]
        public static T LoadFromFile<T>(string path) where T : new()
            => Utils.YamlLoader.LoadFromFile<T>(path);

        [Obsolete("Use YamlSerializer.SaveToFile()")]
        public static void SaveToFile(object obj, string path)
            => Utils.YamlSerializer.SaveToFile(obj, path);

        [Obsolete("Use YamlLoader.ParseYamlList()")]
        public static List<string> ParseYamlList(string yaml)
            => Utils.YamlLoader.ParseYamlList(yaml);

        [Obsolete("Use YamlLoader.ParseYamlDictionary()")]
        public static Dictionary<string, string> ParseYamlDictionary(string yaml)
            => Utils.YamlLoader.ParseYamlDictionary(yaml);
    }
}

#pragma warning restore CS0618
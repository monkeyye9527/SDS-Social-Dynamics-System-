using System;
using System.Collections.Generic;

namespace ONIModPack.Core.Utils
{
    public static class YamlValidator
    {
        public static ValidationResult ValidateYaml(string yaml)
        {
            var result = new ValidationResult();
            
            if (string.IsNullOrWhiteSpace(yaml))
            {
                result.Errors.Add("YAML content is empty");
                return result;
            }

            var lines = yaml.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            ValidateSyntax(lines, result);
            ValidateStructure(lines, result);

            return result;
        }

        private static void ValidateSyntax(string[] lines, ValidationResult result)
        {
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i];
                string trimmed = line.Trim();
                
                if (string.IsNullOrWhiteSpace(trimmed) || trimmed.StartsWith("#"))
                    continue;

                int currentIndent = line.Length - trimmed.Length;

                if (trimmed.StartsWith("- "))
                {
                    if (currentIndent % 2 != 0)
                    {
                        result.Warnings.Add($"Line {i + 1}: List item indentation should be multiple of 2");
                    }
                }
                else if (trimmed.Contains(":"))
                {
                    int colonIndex = trimmed.IndexOf(':');
                    string key = trimmed.Substring(0, colonIndex).Trim();
                    
                    if (string.IsNullOrWhiteSpace(key))
                    {
                        result.Errors.Add($"Line {i + 1}: Empty key before colon");
                    }

                    if (currentIndent % 2 != 0)
                    {
                        result.Warnings.Add($"Line {i + 1}: Map entry indentation should be multiple of 2");
                    }
                }
            }
        }

        private static void ValidateStructure(string[] lines, ValidationResult result)
        {
            var contextStack = new Stack<string>();
            
            foreach (var line in lines)
            {
                string trimmed = line.Trim();
                if (string.IsNullOrWhiteSpace(trimmed) || trimmed.StartsWith("#"))
                    continue;

                if (trimmed.EndsWith(":") && !trimmed.StartsWith("- "))
                {
                    string key = trimmed.Substring(0, trimmed.Length - 1).Trim();
                    contextStack.Push(key);
                }
            }
        }

        public static bool ValidateRequiredFields<T>(T obj, params string[] requiredFields) where T : class
        {
            if (obj == null)
                return false;

            var type = obj.GetType();
            foreach (var field in requiredFields)
            {
                var property = type.GetProperty(field, 
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.IgnoreCase);
                
                if (property == null)
                {
                    ModLogger.Warning($"Required field '{field}' not found in {type.Name}");
                    return false;
                }

                object value = property.GetValue(obj);
                if (value == null || 
                    (value is string str && string.IsNullOrWhiteSpace(str)) ||
                    (value is ICollection<object> col && col.Count == 0))
                {
                    ModLogger.Warning($"Required field '{field}' is empty in {type.Name}");
                    return false;
                }
            }

            return true;
        }

        public class ValidationResult
        {
            public List<string> Errors { get; } = new List<string>();
            public List<string> Warnings { get; } = new List<string>();
            
            public bool IsValid => Errors.Count == 0;
        }
    }
}
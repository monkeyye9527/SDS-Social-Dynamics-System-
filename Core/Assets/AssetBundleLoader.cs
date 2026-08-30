using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;
using ONIModPack.Core;

namespace ONIModPack.Core.Assets
{
    public class AssetBundleLoader : IAssetBundleLoader
    {
        private readonly Dictionary<string, UnityEngine.Object> _cachedAssets = new Dictionary<string, UnityEngine.Object>();
        private string _basePath;
        private bool _initialized;

        public bool IsInitialized => _initialized;
        public string BasePath => _basePath;

        public void Initialize()
        {
            Initialize(null);
        }

        public void Initialize(string modRootPath = null)
        {
            if (_initialized) return;

            _basePath = modRootPath ?? Path.Combine(
                Path.GetDirectoryName(typeof(AssetBundleLoader).Assembly.Location) ?? string.Empty,
                "assets");

            if (!Directory.Exists(_basePath))
            {
                ModLogger.Warning($"[AssetBundleLoader] Assets directory not found: {_basePath}");
            }

            _initialized = true;
            ModLogger.Info($"[AssetBundleLoader] Initialized (base: {_basePath})");
        }

        public void Shutdown()
        {
            UnloadAll();
            ModLogger.Debug("[AssetBundleLoader] Shutdown");
        }

        public T LoadBundle<T>(string bundleName) where T : UnityEngine.Object
        {
            return null;
        }

        public T LoadAsset<T>(string bundleName, string assetName) where T : UnityEngine.Object
        {
            var cacheKey = $"{bundleName}:{assetName}";
            if (_cachedAssets.TryGetValue(cacheKey, out var cached) && cached is T typed)
                return typed;

            var path = Path.Combine(_basePath, assetName);
            if (!File.Exists(path))
            {
                ModLogger.Debug($"[AssetBundleLoader] Asset not found: {path}");
                return null;
            }

            try
            {
                var asset = UnityEngine.Resources.Load<T>(assetName);
                if (asset != null)
                {
                    _cachedAssets[cacheKey] = asset;
                    ModLogger.Debug($"[AssetBundleLoader] Loaded asset: {assetName}");
                }
                return asset;
            }
            catch (Exception ex)
            {
                ModLogger.Error($"[AssetBundleLoader] Failed to load {assetName}: {ex.Message}");
                return null;
            }
        }

        public Texture2D LoadSpriteTexture(string buildingId, string spriteName)
        {
            var path = Path.Combine(_basePath, "buildings", "social", buildingId, "sprites", $"{spriteName}.png");
            if (!File.Exists(path))
            {
                ModLogger.Debug($"[AssetBundleLoader] Sprite not found: {path}");
                return CreatePlaceholderTexture(buildingId);
            }

            try
            {
                var bytes = File.ReadAllBytes(path);
                var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                
                var loadImageMethod = typeof(Texture2D).GetMethod("LoadImage", new[] { typeof(byte[]) });
                if (loadImageMethod != null)
                {
                    loadImageMethod.Invoke(tex, new object[] { bytes });
                }
                else
                {
                    var imageConversionType = Type.GetType("UnityEngine.ImageConversion, UnityEngine.ImageConversionModule");
                    if (imageConversionType != null)
                    {
                        var staticLoadImageMethod = imageConversionType.GetMethod("LoadImage", 
                            BindingFlags.Public | BindingFlags.Static, 
                            null, 
                            new[] { typeof(Texture2D), typeof(byte[]) }, 
                            null);
                        if (staticLoadImageMethod != null)
                        {
                            staticLoadImageMethod.Invoke(null, new object[] { tex, bytes });
                        }
                        else
                        {
                            ModLogger.Warning($"[AssetBundleLoader] LoadImage method not found in UnityEngine.ImageConversion");
                        }
                    }
                    else
                    {
                        ModLogger.Warning($"[AssetBundleLoader] UnityEngine.ImageConversion type not found");
                    }
                }
                
                return tex;
            }
            catch (Exception ex)
            {
                ModLogger.Warning($"[AssetBundleLoader] Failed to load sprite {path}: {ex.Message}");
            }

            return CreatePlaceholderTexture(buildingId);
        }

        public string ResolveBuildingConfigPath(string buildingId, string configFile)
        {
            return Path.Combine(_basePath, "buildings", "social", buildingId, "configs", configFile);
        }

        public void UnloadAll()
        {
            _cachedAssets.Clear();
            _initialized = false;
            ModLogger.Debug("[AssetBundleLoader] All assets unloaded");
        }

        public void ClearCache()
        {
            _cachedAssets.Clear();
            ModLogger.Debug("[AssetBundleLoader] Cache cleared");
        }

        private static Texture2D CreatePlaceholderTexture(string label)
        {
            var tex = new Texture2D(128, 128, TextureFormat.RGBA32, false);
            var color = new Color(0.5f, 0.5f, 0.55f, 1f);
            var pixels = new Color[128 * 128];
            for (int i = 0; i < pixels.Length; i++)
                pixels[i] = color;
            tex.SetPixels(pixels);
            tex.Apply();
            tex.name = $"placeholder_{label}";
            return tex;
        }
    }
}

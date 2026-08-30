using System.Collections.Generic;
using UnityEngine;
using ONIModPack.Core.Services;

namespace ONIModPack.Core.Assets
{
    public interface IAssetBundleLoader : IService
    {
        string BasePath { get; }
        
        T LoadAsset<T>(string bundleName, string assetName) where T : UnityEngine.Object;
        Texture2D LoadSpriteTexture(string buildingId, string spriteName);
        void UnloadAll();
        void ClearCache();
    }
}

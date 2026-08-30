using System;
using System.Collections.Generic;
using ONIModPack.Core.Services;

namespace ONIModPack.Core.ModManagement
{
    public interface IModConflictResolver : IService
    {
        event Action<ConflictInfo> OnConflictDetected;
        event Action<ConflictInfo> OnConflictResolved;

        void RegisterMod(ModInfo modInfo);
        void UnregisterMod(string modId);
        List<ConflictInfo> CheckForConflicts(ModInfo modInfo);
        List<ConflictInfo> GetAllConflicts();
        List<ConflictInfo> GetUnresolvedConflicts();
        void ResolveConflict(string conflictId, ConflictResolutionType resolution);
        List<ModInfo> GetRegisteredMods();
    }
}

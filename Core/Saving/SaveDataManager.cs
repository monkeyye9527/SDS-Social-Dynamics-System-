﻿﻿﻿﻿﻿﻿﻿﻿﻿﻿﻿﻿﻿﻿﻿﻿﻿﻿using System;
using System.Collections.Generic;
using System.Linq;
using KSerialization;
using ONIModPack.Content.SocialDynamics;
using ONIModPack.Content.SocialDynamics.Politics;
using ONIModPack.Content.SocialResearch;
using ONIModPack.Core.Services;

namespace ONIModPack.Core
{
    /// <summary>
    /// Save data manager for serializing and deserializing mod state.
    /// Implements IModModuleLifecycle to receive OnLoad/OnSave callbacks.
    /// </summary>
    [SerializationConfig(MemberSerialization.OptIn)]
    public class SaveDataManager : IService, IModModuleLifecycle
    {
        // Research state
        [Serialize] private Dictionary<string, bool> _researchState = new Dictionary<string, bool>();

        // Faction data
        [Serialize] private FactionSaveData _factionData = new FactionSaveData();

        // Law system data
        [Serialize] private LawSaveData _lawData = new LawSaveData();

        // Leader data
        [Serialize] private LeaderSaveData _leaderData = new LeaderSaveData();

        // Trait system data
        [Serialize] private TraitSaveData _traitData = new TraitSaveData();

        // Social state data
        [Serialize] private SocialStateSaveData _socialStateData = new SocialStateSaveData();

        // Strike system data
        [Serialize] private StrikeSaveData _strikeData = new StrikeSaveData();

        // Faith grid snapshot
        [Serialize] private FaithGridSaveData _faithGridData = new FaithGridSaveData();

        public bool IsInitialized { get; private set; }

        public void Initialize()
        {
            ONIModPack.Core.Logger.Debug("[SaveDataManager] Initializing");
            IsInitialized = true;
        }

        public void Shutdown()
        {
            _researchState.Clear();
            IsInitialized = false;
            ONIModPack.Core.Logger.Debug("[SaveDataManager] Shutdown");
        }

        // IModModuleLifecycle implementation
        public void OnGameLoaded() => ONIModPack.Core.Logger.Debug("[SaveDataManager] OnGameLoaded");
        public void OnWorldLoaded() => ONIModPack.Core.Logger.Debug("[SaveDataManager] OnWorldLoaded");
        public void OnUnload() => Shutdown();

        /// <summary></summary>
        public void OnSave()
        {
            ONIModPack.Core.Logger.Info("[SaveDataManager] Saving mod data");
            SaveResearchState();
            SaveFactionData();
            SaveLawData();
            SaveLeaderData();
            SaveTraitData();
            SaveSocialStateData();
            SaveStrikeData();
            SaveFaithGridData();
        }

        /// <summary></summary>
        public void OnLoad()
        {
            ONIModPack.Core.Logger.Info("[SaveDataManager] Loading mod data");
            LoadResearchState();
            LoadFactionData();
            LoadLawData();
            LoadLeaderData();
            LoadTraitData();
            LoadSocialStateData();
            LoadStrikeData();
            LoadFaithGridData();
        }

        #region Research State
        private void SaveResearchState()
        {
            var researchTree = ServiceResolver.OptionalService<SocialResearchTree>();
            if (researchTree == null) return;

            _researchState.Clear();
            foreach (var node in researchTree.AllNodes)
            {
                _researchState[node.Id] = node.IsUnlocked;
            }
        }

        private void LoadResearchState()
        {
            var researchTree = ServiceResolver.OptionalService<SocialResearchTree>();
            if (researchTree == null) return;

            foreach (var node in researchTree.AllNodes)
            {
                if (_researchState.TryGetValue(node.Id, out var unlocked))
                {
                    node.IsUnlocked = unlocked;
                }
            }
        }

        public bool IsResearchUnlocked(string id)
        {
            return _researchState.TryGetValue(id, out var unlocked) && unlocked;
        }
        #endregion

        #region Faction Data
        private void SaveFactionData()
        {
            var factionSystem = ServiceResolver.OptionalService<Content.SocialDynamics.Politics.FactionSystem>();
            if (factionSystem == null) return;

            _factionData.Factions.Clear();

            foreach (var faction in GetAllFactionsFromSystem(factionSystem))
            {
                _factionData.Factions.Add(new FactionData
                {
                    Id = faction.Type.ToString(),
                    Name = faction.Type.ToString(),
                    MemberIds = new List<string>(faction.Members),
                    Influence = faction.Influence
                });
            }

            ONIModPack.Core.Logger.Debug("[SaveDataManager] Saved faction data");
        }

        private void LoadFactionData()
        {
            var factionSystem = ServiceResolver.OptionalService<Content.SocialDynamics.Politics.FactionSystem>();
            if (factionSystem == null) return;

            foreach (var data in _factionData.Factions)
            {
                if (Enum.TryParse<Content.SocialDynamics.Politics.FactionType>(data.Id, out var type))
                {
                    var faction = factionSystem.GetFaction(type);
                    if (faction != null)
                    {
                        faction.Influence = data.Influence;
                        if (data.MemberIds != null)
                        {
                            faction.Members.Clear();
                            faction.Members.AddRange(data.MemberIds);
                            faction.MemberCount = faction.Members.Count;
                        }
                    }
                }
            }

            ONIModPack.Core.Logger.Debug("[SaveDataManager] Loaded faction data");
        }

        private IEnumerable<Content.SocialDynamics.Politics.FactionData> GetAllFactionsFromSystem(
            Content.SocialDynamics.Politics.FactionSystem system)
        {
            var repositoryField = system.GetType().GetField("_repository", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (repositoryField != null)
            {
                var repository = repositoryField.GetValue(system);
                var getAllMethod = repository.GetType().GetMethod("GetAllFactions");
                if (getAllMethod != null)
                {
                    return getAllMethod.Invoke(repository, null) as IEnumerable<Content.SocialDynamics.Politics.FactionData>;
                }
            }
            return Enumerable.Empty<Content.SocialDynamics.Politics.FactionData>();
        }
        #endregion

        #region Law Data
        private void SaveLawData()
        {
            var lawSystem = ServiceResolver.OptionalService<Content.SocialDynamics.LawSystem>();
            if (lawSystem == null) return;

            _lawData.ActiveLaws.Clear();

            foreach (var law in lawSystem.GetActiveLaws())
            {
                _lawData.ActiveLaws.Add(new ActiveLawData
                {
                    LawId = law.Id,
                    EnactmentCycle = law.TurnPassed
                });
            }

            ONIModPack.Core.Logger.Debug("[SaveDataManager] Saved law data");
        }

        private void LoadLawData()
        {
            var lawSystem = ServiceResolver.OptionalService<Content.SocialDynamics.LawSystem>();
            if (lawSystem == null) return;

            foreach (var data in _lawData.ActiveLaws)
            {
                lawSystem.EnactLaw(data.LawId);
                
                var lawField = lawSystem.GetType().GetField("_laws", 
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (lawField != null)
                {
                    var laws = lawField.GetValue(lawSystem) as Dictionary<string, Content.SocialDynamics.LawDefinition>;
                    if (laws != null && laws.TryGetValue(data.LawId, out var law))
                    {
                        law.TurnPassed = data.EnactmentCycle;
                    }
                }
            }

            ONIModPack.Core.Logger.Debug("[SaveDataManager] Loaded law data");
        }
        #endregion

        #region Leader Data
        private void SaveLeaderData()
        {
            var factionSystem = ServiceResolver.OptionalService<Content.SocialDynamics.Politics.FactionSystem>();
            if (factionSystem == null) return;

            foreach (var faction in GetAllFactionsFromSystem(factionSystem))
            {
                var leader = factionSystem.GetLeader(faction.Type);
                if (leader != null)
                {
                    _leaderData.FactionType = faction.Type.ToString();
                    _leaderData.CurrentLeaderId = leader.Id;
                    _leaderData.ElectionCycle = 0;
                    break;
                }
            }

            ONIModPack.Core.Logger.Debug("[SaveDataManager] Saved leader data");
        }

        private void LoadLeaderData()
        {
            var factionSystem = ServiceResolver.OptionalService<Content.SocialDynamics.Politics.FactionSystem>();
            if (factionSystem == null) return;

            if (!string.IsNullOrEmpty(_leaderData.FactionType) && 
                !string.IsNullOrEmpty(_leaderData.CurrentLeaderId) &&
                Enum.TryParse<Content.SocialDynamics.Politics.FactionType>(_leaderData.FactionType, out var factionType))
            {
                factionSystem.RestoreLeader(factionType, _leaderData.CurrentLeaderId);
                ONIModPack.Core.Logger.Debug($"[SaveDataManager] Restored leader {_leaderData.CurrentLeaderId} for faction {_leaderData.FactionType}");
            }

            ONIModPack.Core.Logger.Debug("[SaveDataManager] Loaded leader data");
        }
        #endregion

        #region Trait Data
        private void SaveTraitData()
        {
            var traitManager = ServiceResolver.OptionalService<Content.TraitSystem.TraitManager>();
            if (traitManager == null) return;

            _traitData.DuplicantTraits.Clear();

            var duplicantTraitsField = traitManager.GetType().GetField("duplicantTraits",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (duplicantTraitsField == null) return;

            var duplicantTraits = duplicantTraitsField.GetValue(traitManager) as Dictionary<int, HashSet<string>>;
            if (duplicantTraits == null) return;

            foreach (var kvp in duplicantTraits)
            {
                _traitData.DuplicantTraits.Add(new DuplicantTraitData
                {
                    DuplicantId = kvp.Key,
                    TraitIds = new List<string>(kvp.Value)
                });
            }

            ONIModPack.Core.Logger.Debug($"[SaveDataManager] Saved trait data for {_traitData.DuplicantTraits.Count} duplicants");
        }

        private void LoadTraitData()
        {
            var traitManager = ServiceResolver.OptionalService<Content.TraitSystem.TraitManager>();
            if (traitManager == null) return;

            var duplicantTraitsField = traitManager.GetType().GetField("duplicantTraits",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (duplicantTraitsField == null) return;

            var duplicantTraits = duplicantTraitsField.GetValue(traitManager) as Dictionary<int, HashSet<string>>;
            if (duplicantTraits == null) return;

            duplicantTraits.Clear();

            foreach (var data in _traitData.DuplicantTraits)
            {
                duplicantTraits[data.DuplicantId] = new HashSet<string>(data.TraitIds);
            }

            ONIModPack.Core.Logger.Debug($"[SaveDataManager] Loaded trait data for {_traitData.DuplicantTraits.Count} duplicants");
        }
        #endregion

        #region Social State Data
        private void SaveSocialStateData()
        {
            var stateManager = ServiceResolver.OptionalService<Content.SocialDynamics.SocialStateManager>();
            if (stateManager == null) return;

            _socialStateData.States.Clear();

            foreach (var data in stateManager.GetAll())
            {
                _socialStateData.States.Add(new SocialStateRecord
                {
                    DuplicantId = data.DuplicantId,
                    Stress = data.State.Stress,
                    Radicalism = data.State.Radicalism,
                    PrimaryBelief = data.State.PrimaryBelief ?? "Neutral",
                    FactionTendency = data.State.PrimaryFaction ?? "Neutral"
                });
            }

            ONIModPack.Core.Logger.Debug($"[SaveDataManager] Saved social state data for {_socialStateData.States.Count} duplicants");
        }

        private void LoadSocialStateData()
        {
            var stateManager = ServiceResolver.OptionalService<Content.SocialDynamics.SocialStateManager>();
            if (stateManager == null) return;

            foreach (var record in _socialStateData.States)
            {
                var data = stateManager.GetOrCreate(record.DuplicantId);
                data.State.Stress = record.Stress;
                data.State.Radicalism = record.Radicalism;
                data.State.PrimaryBelief = record.PrimaryBelief;
                data.State.PrimaryFaction = record.FactionTendency;
            }

            ONIModPack.Core.Logger.Debug($"[SaveDataManager] Loaded social state data for {_socialStateData.States.Count} duplicants");
        }
        #endregion

        #region Strike Data
        private void SaveStrikeData()
        {
            var strikeSystem = ServiceResolver.OptionalService<Content.SocialDynamics.StrikeSystem>();
            if (strikeSystem == null) return;

            _strikeData.ActiveStrikes.Clear();

            var activeStrikes = strikeSystem.GetActiveStrikes();
            foreach (var strike in activeStrikes)
            {
                _strikeData.ActiveStrikes.Add(new StrikeRecord
                {
                    DuplicantId = strike.DuplicantId,
                    StartCycle = strike.StartCycle,
                    Reason = strike.Reason ?? "Unknown",
                    Intensity = strike.Intensity
                });
            }

            _strikeData.StrikeProbabilityModifier = strikeSystem.StrikeProbabilityModifier;

            ONIModPack.Core.Logger.Debug($"[SaveDataManager] Saved strike data: {_strikeData.ActiveStrikes.Count} active strikes");
        }

        private void LoadStrikeData()
        {
            var strikeSystem = ServiceResolver.OptionalService<Content.SocialDynamics.StrikeSystem>();
            if (strikeSystem == null) return;

            strikeSystem.StrikeProbabilityModifier = _strikeData.StrikeProbabilityModifier;

            foreach (var record in _strikeData.ActiveStrikes)
            {
                strikeSystem.RestoreStrike(record.DuplicantId, record.StartCycle, record.Reason, record.Intensity);
            }

            ONIModPack.Core.Logger.Debug($"[SaveDataManager] Loaded strike data: {_strikeData.ActiveStrikes.Count} strikes restored");
        }
        #endregion

        #region Faith Grid Data
        private void SaveFaithGridData()
        {
            var faithGrid = ServiceResolver.OptionalService<Content.BeliefSystem.IFaithGrid>();
            if (faithGrid == null) return;

            _faithGridData.SourceCount = 0;
            _faithGridData.PulseCount = 0;

            if (faithGrid is Content.BeliefSystem.FaithPropagationGrid grid)
            {
                var sources = grid.GetActiveFaithSources();
                _faithGridData.SourceCount = sources.Count;

                var pulses = grid.GetActivePulses();
                _faithGridData.PulseCount = pulses.Count;
            }

            ONIModPack.Core.Logger.Debug($"[SaveDataManager] Saved faith grid data: {_faithGridData.SourceCount} sources, {_faithGridData.PulseCount} pulses");
        }

        private void LoadFaithGridData()
        {
            // Faith grid sources are recreated from BeliefSystemCore states
            // Pulses are transient and don't need restoration
            ONIModPack.Core.Logger.Debug("[SaveDataManager] Faith grid will be restored from BeliefSystemCore states");
        }
        #endregion
    }

    [SerializationConfig(MemberSerialization.OptIn)]
    public class FactionSaveData
    {
        [Serialize] public List<FactionData> Factions = new List<FactionData>();
    }

    [SerializationConfig(MemberSerialization.OptIn)]
    public class FactionData
    {
        [Serialize] public string Id;
        [Serialize] public string Name;
        [Serialize] public List<string> MemberIds;
        [Serialize] public float Influence;
    }

    [SerializationConfig(MemberSerialization.OptIn)]
    public class LawSaveData
    {
        [Serialize] public List<ActiveLawData> ActiveLaws = new List<ActiveLawData>();
    }

    [SerializationConfig(MemberSerialization.OptIn)]
    public class ActiveLawData
    {
        [Serialize] public string LawId;
        [Serialize] public int EnactmentCycle;
    }

    [SerializationConfig(MemberSerialization.OptIn)]
    public class LeaderSaveData
    {
        [Serialize] public string FactionType;
        [Serialize] public string CurrentLeaderId;
        [Serialize] public int ElectionCycle;
    }

    [SerializationConfig(MemberSerialization.OptIn)]
    public class TraitSaveData
    {
        [Serialize] public List<DuplicantTraitData> DuplicantTraits = new List<DuplicantTraitData>();
    }

    [SerializationConfig(MemberSerialization.OptIn)]
    public class DuplicantTraitData
    {
        [Serialize] public int DuplicantId;
        [Serialize] public List<string> TraitIds = new List<string>();
    }

    [SerializationConfig(MemberSerialization.OptIn)]
    public class SocialStateSaveData
    {
        [Serialize] public List<SocialStateRecord> States = new List<SocialStateRecord>();
    }

    [SerializationConfig(MemberSerialization.OptIn)]
    public class SocialStateRecord
    {
        [Serialize] public int DuplicantId;
        [Serialize] public float Stress;
        [Serialize] public float Radicalism;
        [Serialize] public string PrimaryBelief;
        [Serialize] public string FactionTendency;
    }

    [SerializationConfig(MemberSerialization.OptIn)]
    public class StrikeSaveData
    {
        [Serialize] public List<StrikeRecord> ActiveStrikes = new List<StrikeRecord>();
        [Serialize] public float StrikeProbabilityModifier;
    }

    [SerializationConfig(MemberSerialization.OptIn)]
    public class StrikeRecord
    {
        [Serialize] public int DuplicantId;
        [Serialize] public int StartCycle;
        [Serialize] public string Reason;
        [Serialize] public float Intensity;
    }

    [SerializationConfig(MemberSerialization.OptIn)]
    public class FaithGridSaveData
    {
        [Serialize] public int SourceCount;
        [Serialize] public int PulseCount;
    }
}

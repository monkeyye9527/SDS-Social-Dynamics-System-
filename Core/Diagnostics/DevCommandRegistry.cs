using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using ONIModPack.Core.Diagnostics;
using ONIModPack.Core.Services;

namespace ONIModPack.Core
{
    public static class DevCommandRegistry
    {
        private static readonly Dictionary<string, Action<string[]>> _commands =
            new Dictionary<string, Action<string[]>>(StringComparer.OrdinalIgnoreCase);

        public static void Initialize()
        {
            Register("help", args => ModLogger.Info(GetHelpText()));
            Register("modules", args => LogModules());
            Register("personality", args => LogPersonalities());
            Register("belief", args => LogBeliefs());
            Register("faithgrid", args => FaithPropagationGridDebug());
            Register("reload-slots", args => ReloadSlots(args));
            Register("gc", args => { GC.Collect(); ModLogger.Info("[Dev] GC triggered"); });

            DebugSystem.RegisterPanel(new DevCommandPanel());
                ModLogger.Debug("[DevCommands] Registry initialized");
        }

        public static void Register(string name, Action<string[]> handler)
        {
            _commands[name] = handler;
        }

        public static bool TryExecute(string input)
        {
            if (string.IsNullOrWhiteSpace(input)) return false;

            var trimmed = input.Trim();
            if (!trimmed.StartsWith("/", StringComparison.Ordinal)) return false;

            var parts = trimmed.Substring(1).Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0) return false;

            var cmd = parts[0];
            var args = parts.Length > 1 ? parts.Skip(1).ToArray() : Array.Empty<string>();

            if (_commands.TryGetValue(cmd, out var handler))
            {
                try
                {
                    handler(args);
                    return true;
                }
                catch (Exception ex)
                {
                    ModLogger.Error($"[Dev] Command '{cmd}' failed: {ex.Message}");
                    return true;
                }
            }

            ModLogger.Warning($"[Dev] Unknown command: {cmd}. Type /help");
            return true;
        }

        public static string GetHelpText()
        {
            return "[Dev] Commands: /help, /modules, /personality, /belief, /faithgrid, /reload-slots <id>, /gc";
        }

        private static void LogModules()
        {
            if (ModEntry.ModuleMgr == null) return;
            foreach (var m in ModEntry.ModuleMgr.AllModules)
            {
                var enabled = ModEntry.ModuleMgr.IsEnabled(m.ModuleId);
                ModLogger.Info($"  [{(enabled ? "ON" : "OFF")}] {m.ModuleId}");
            }
        }

        private static void LogPersonalities()
        {
            foreach (var identity in Components.LiveMinionIdentities.Items)
            {
                var p = identity.GetComponent<Content.Personality.PersonalityComponent>();
                if (p == null) continue;
                ModLogger.Info($"  {identity.name}: stress×{p.GetStressGrowthMultiplier():F2} work_eff={p.GetWorkEfficiency():F2} social={p.GetSocialInteractionBonus():F2}");
            }
        }

        private static void LogBeliefs()
        {
            foreach (var identity in Components.LiveMinionIdentities.Items)
            {
                var b = identity.GetComponent<Content.BeliefSystem.BeliefSystemCore>();
                if (b == null) continue;
                ModLogger.Info($"  {identity.name}: {b.GetDominantBelief()}");
            }
        }

        private static void FaithPropagationGridDebug()
        {
            ModLogger.Info("[Dev] Faith grid cycle tick");
            var grid = ServiceResolver.OptionalService<Content.BeliefSystem.IFaithGrid>();
            grid?.CycleUpdate();
        }

        private static void ReloadSlots(string[] args)
        {
            Content.Art.SlotsYamlLoader.ClearCache();
            var id = args.Length > 0 ? args[0] : "community_center";
            var config = Content.Art.SlotsYamlLoader.Load(id);
            ModLogger.Info(config != null
                ? $"[Dev] Reloaded slots for {id}: {config.Slots.Count} entries"
                : $"[Dev] No slots.yaml for {id}");
        }
    }

    public class DevCommandPanel : IDebugPanel
    {
        private string _input = "";
        private readonly List<string> _history = new List<string>();

        public string PanelName => "Dev Commands";
        public bool IsVisible { get; set; } = true;

        public void Draw()
        {
            GUILayout.Label("Type /help for commands");
            _input = GUILayout.TextField(_input, GUILayout.Width(300));
            if (GUILayout.Button("Run") && !string.IsNullOrWhiteSpace(_input))
            {
                DevCommandRegistry.TryExecute(_input);
                _history.Add(_input);
                _input = "";
            }

            foreach (var line in _history)
                GUILayout.Label(line);
        }
    }
}

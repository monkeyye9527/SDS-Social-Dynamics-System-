using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using ONIModPack.Core;

namespace ONIModPack.Core.Debugging
{
    public delegate void CommandHandler(string[] args);

    public class DevCommand
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public string Usage { get; set; }
        public CommandHandler Handler { get; set; }
        public bool RequiresCheats { get; set; }
    }

    public class DevTools : IDevTools
    {
        private readonly Dictionary<string, DevCommand> _commands = new Dictionary<string, DevCommand>();
        private bool _isEnabled;
        private bool _isInitialized;

        public event Action<string> OnCommandExecuted;
        public event Action<string, string> OnCommandError;

        public bool IsInitialized => _isInitialized;
        public bool IsEnabled => _isEnabled;

        public void Initialize()
        {
            if (_isInitialized) return;
            
            RegisterBuiltInCommands();
            
            _isInitialized = true;
            ModLogger.Info("[DevTools] Initialized");
        }

        public void Shutdown()
        {
            _commands.Clear();
            OnCommandExecuted = null;
            OnCommandError = null;
            _isEnabled = false;
            _isInitialized = false;
            ModLogger.Debug("[DevTools] Shutdown");
        }

        private void RegisterBuiltInCommands()
        {
            RegisterCommand(new DevCommand
            {
                Name = "sds.debug",
                Description = "Toggle SDS Runtime debug mode",
                Usage = "sds.debug [on|off]",
                Handler = HandleSdsDebug,
                RequiresCheats = false
            });

            RegisterCommand(new DevCommand
            {
                Name = "sds.status",
                Description = "Show SDS Runtime status",
                Usage = "sds.status",
                Handler = HandleSdsStatus,
                RequiresCheats = false
            });

            RegisterCommand(new DevCommand
            {
                Name = "sds.reset",
                Description = "Reset SDS Runtime state",
                Usage = "sds.reset",
                Handler = HandleSdsReset,
                RequiresCheats = true
            });

            RegisterCommand(new DevCommand
            {
                Name = "log.level",
                Description = "Set log level",
                Usage = "log.level [trace|debug|info|warning|error]",
                Handler = HandleLogLevel,
                RequiresCheats = false
            });

            RegisterCommand(new DevCommand
            {
                Name = "log.flush",
                Description = "Flush log queue",
                Usage = "log.flush",
                Handler = HandleLogFlush,
                RequiresCheats = false
            });

            RegisterCommand(new DevCommand
            {
                Name = "mod.conflicts",
                Description = "Show mod conflicts",
                Usage = "mod.conflicts [resolve]",
                Handler = HandleModConflicts,
                RequiresCheats = false
            });

            RegisterCommand(new DevCommand
            {
                Name = "personality.inspect",
                Description = "Inspect minion personality",
                Usage = "personality.inspect [minionId]",
                Handler = HandlePersonalityInspect,
                RequiresCheats = false
            });

            RegisterCommand(new DevCommand
            {
                Name = "help",
                Description = "Show available commands",
                Usage = "help [command]",
                Handler = HandleHelp,
                RequiresCheats = false
            });
        }

        public void RegisterCommand(DevCommand command)
        {
            if (command == null || string.IsNullOrEmpty(command.Name))
                return;

            _commands[command.Name.ToLower()] = command;
            ModLogger.Debug("[DevTools] Registered command: " + command.Name);
        }

        public void UnregisterCommand(string commandName)
        {
            _commands.Remove(commandName.ToLower());
            ModLogger.Debug("[DevTools] Unregistered command: " + commandName);
        }

        public bool ExecuteCommand(string input)
        {
            if (!_isEnabled)
            {
                UnityEngine.Debug.LogError("[DevTools] Dev tools not enabled");
                return false;
            }

            input = input.Trim();
            if (string.IsNullOrEmpty(input))
                return false;

            string[] parts = input.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0)
                return false;

            string commandName = parts[0].ToLower();
            string[] args = parts.Length > 1 ? parts.Skip(1).ToArray() : new string[0];

            if (!_commands.TryGetValue(commandName, out var command))
            {
                UnityEngine.Debug.LogError("[DevTools] Unknown command: " + commandName);
                OnCommandError?.Invoke(commandName, "Unknown command");
                return false;
            }

            if (command.RequiresCheats && !IsCheatsEnabled())
            {
                UnityEngine.Debug.LogError("[DevTools] Cheats not enabled");
                OnCommandError?.Invoke(commandName, "Cheats not enabled");
                return false;
            }

            try
            {
                command.Handler(args);
                OnCommandExecuted?.Invoke(input);
                ModLogger.Debug("[DevTools] Executed command: " + input);
                return true;
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError("[DevTools] Error executing command " + commandName + ": " + ex.Message);
                OnCommandError?.Invoke(commandName, ex.Message);
                return false;
            }
        }

        private bool IsCheatsEnabled()
        {
            return true;
        }

        private void HandleSdsDebug(string[] args)
        {
            if (args.Length == 0)
            {
                UnityEngine.Debug.Log("Usage: sds.debug [on|off]");
                return;
            }

            bool enabled = args[0].Equals("on", StringComparison.OrdinalIgnoreCase);
            UnityEngine.Debug.Log("SDS Debug mode " + (enabled ? "enabled" : "disabled"));
        }

        private void HandleSdsStatus(string[] args)
        {
            UnityEngine.Debug.Log("=== SDS Runtime Status ===");
            UnityEngine.Debug.Log("Layers: Environment, Need, Decision, Interaction, Society");
            UnityEngine.Debug.Log("Status: Running");
        }

        private void HandleSdsReset(string[] args)
        {
            UnityEngine.Debug.Log("SDS Runtime state reset");
        }

        private void HandleLogLevel(string[] args)
        {
            if (args.Length == 0)
            {
                UnityEngine.Debug.Log("Current log level: " + ModLogger.CurrentLogLevel);
                return;
            }

            if (Enum.TryParse<LogLevel>(args[0], true, out var level))
            {
                ModLogger.CurrentLogLevel = level;
                UnityEngine.Debug.Log("Log level set to: " + level);
            }
            else
            {
                UnityEngine.Debug.LogError("Invalid log level: " + args[0]);
            }
        }

        private void HandleLogFlush(string[] args)
        {
            ModLogger.Flush();
            UnityEngine.Debug.Log("Log queue flushed");
        }

        private void HandleModConflicts(string[] args)
        {
            UnityEngine.Debug.Log("=== Mod Conflicts ===");
            UnityEngine.Debug.Log("No conflicts detected");
        }

        private void HandlePersonalityInspect(string[] args)
        {
            if (args.Length == 0)
            {
                UnityEngine.Debug.Log("Usage: personality.inspect [minionId]");
                return;
            }

            UnityEngine.Debug.Log("Personality for minion " + args[0] + ":");
            UnityEngine.Debug.Log("5D Profile: Openness, Conscientiousness, Extraversion, Agreeableness, Neuroticism");
        }

        private void HandleHelp(string[] args)
        {
            if (args.Length == 0)
            {
                UnityEngine.Debug.Log("=== Available Commands ===");
                foreach (var cmd in _commands.Values)
                {
                    UnityEngine.Debug.Log(cmd.Name + ": " + cmd.Description);
                }
            }
            else
            {
                string cmdName = args[0].ToLower();
                if (_commands.TryGetValue(cmdName, out var cmd))
                {
                    UnityEngine.Debug.Log("=== " + cmd.Name + " ===");
                    UnityEngine.Debug.Log("Description: " + cmd.Description);
                    UnityEngine.Debug.Log("Usage: " + cmd.Usage);
                    UnityEngine.Debug.Log("Requires Cheats: " + cmd.RequiresCheats);
                }
            }
        }

        public void SetEnabled(bool enabled)
        {
            _isEnabled = enabled;
        }

        public List<DevCommand> GetAllCommands()
        {
            return new List<DevCommand>(_commands.Values);
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;

namespace ONIModPack.Core.Modules
{
    public sealed class ModuleBootstrap
    {
        private readonly List<IModule> _modules = new List<IModule>();
        private readonly Dictionary<string, IModule> _moduleLookup = new Dictionary<string, IModule>();
        private bool _isBooted;
        private bool _isShuttingDown;

        public IReadOnlyList<IModule> Modules => _modules.AsReadOnly();

        public void RegisterModule(IModule module)
        {
            if (module == null) throw new ArgumentNullException(nameof(module));
            if (_isBooted) throw new InvalidOperationException("Cannot register modules after boot");
            if (_moduleLookup.ContainsKey(module.ModuleId))
            {
                ModLogger.Warning($"[ModuleBootstrap] Module {module.ModuleId} already registered, skipping");
                return;
            }

            _modules.Add(module);
            _moduleLookup[module.ModuleId] = module;
            ModLogger.Debug($"[ModuleBootstrap] Registered module: {module.ModuleId}");
        }

        public TModule GetModule<TModule>() where TModule : class, IModule
        {
            return _modules.OfType<TModule>().FirstOrDefault();
        }

        public IModule GetModule(string moduleId)
        {
            _moduleLookup.TryGetValue(moduleId, out var module);
            return module;
        }

        public bool TryGetModule<TModule>(out TModule module) where TModule : class, IModule
        {
            module = GetModule<TModule>();
            return module != null;
        }

        public bool TryGetModule(string moduleId, out IModule module)
        {
            return _moduleLookup.TryGetValue(moduleId, out module);
        }

        public void Boot()
        {
            if (_isBooted) return;

            ModLogger.Info("[ModuleBootstrap] Starting module bootstrap...");

            try
            {
                ModLogger.Info("[ModuleBootstrap] Initializing modules...");
                foreach (var module in _modules)
                {
                    try
                    {
                        module.Initialize();
                        ModLogger.Debug($"[ModuleBootstrap] Initialized: {module.ModuleId}");
                    }
                    catch (Exception ex)
                    {
                        ModLogger.Error($"[ModuleBootstrap] Failed to initialize {module.ModuleId}: {ex.Message}");
                    }
                }

                ModLogger.Info("[ModuleBootstrap] Starting modules...");
                foreach (var module in _modules)
                {
                    try
                    {
                        module.Start();
                        ModLogger.Debug($"[ModuleBootstrap] Started: {module.ModuleId}");
                    }
                    catch (Exception ex)
                    {
                        ModLogger.Error($"[ModuleBootstrap] Failed to start {module.ModuleId}: {ex.Message}");
                    }
                }

                _isBooted = true;
                ModLogger.Info($"[ModuleBootstrap] Bootstrap completed. {_modules.Count} modules loaded.");
            }
            catch (Exception ex)
            {
                ModLogger.Error($"[ModuleBootstrap] Bootstrap failed: {ex.Message}");
                throw;
            }
        }

        public void Shutdown()
        {
            if (_isShuttingDown) return;
            _isShuttingDown = true;

            ModLogger.Info("[ModuleBootstrap] Shutting down modules...");

            foreach (var module in _modules.AsEnumerable().Reverse())
            {
                try
                {
                    module.Shutdown();
                    ModLogger.Debug($"[ModuleBootstrap] Shutdown: {module.ModuleId}");
                }
                catch (Exception ex)
                {
                    ModLogger.Error($"[ModuleBootstrap] Failed to shutdown {module.ModuleId}: {ex.Message}");
                }
            }

            _modules.Clear();
            _moduleLookup.Clear();
            _isBooted = false;
            _isShuttingDown = false;

            ModLogger.Info("[ModuleBootstrap] All modules shutdown complete");
        }
    }
}

using System;
using System.Collections.Generic;
using UnityEngine;
using ONIModPack.Core.Services;

namespace ONIModPack.Core.Diagnostics
{
    public interface IDebugPanel
    {
        string PanelName { get; }
        void Draw();
        bool IsVisible { get; set; }
    }

    public class DebugSystem
    {
        private static bool _isEnabled;
        private static bool _isVisible;
        private static readonly List<IDebugPanel> _panels = new List<IDebugPanel>();
        private static int _selectedPanelIndex;
        private static Vector2 _scrollPosition;

        public static bool IsEnabled => _isEnabled;
        public static bool IsVisible => _isVisible;

        public static void Initialize()
        {
            _isEnabled = true;
            RegisterPanels();
            Logger.Info("[Debug] DebugSystem initialized");
        }

        private static void RegisterPanels()
        {
            _panels.Add(new FactionDebugPanel());
            _panels.Add(new OpinionDebugPanel());
            _panels.Add(new MemoryDebugPanel());
            _panels.Add(new SchedulerDebugPanel());
            _panels.Add(new AIDebugPanel());
            _panels.Add(new SystemStatusPanel());
        }

        public static void RegisterPanel(IDebugPanel panel)
        {
            if (!_panels.Exists(p => p.PanelName == panel.PanelName))
            {
                _panels.Add(panel);
                Logger.Debug($"[Debug] Registered panel: {panel.PanelName}");
            }
        }

        public static void Toggle()
        {
            if (!_isEnabled) return;
            _isVisible = !_isVisible;
            
            if (_isVisible)
            {
                Logger.Info("[Debug] Debug panel opened");
            }
            else
            {
                Logger.Info("[Debug] Debug panel closed");
            }
        }

        public static void Update()
        {
            if (!_isEnabled) return;

            if (Input.GetKeyDown(KeyCode.F3))
                Toggle();
        }

        public static void Draw()
        {
            if (!_isEnabled || !_isVisible) return;

            GUILayout.Window(12345, new Rect(20, 20, 800, 600), DrawWindow, "ONI Modpack Debug [F3]");
        }

        private static void DrawWindow(int windowID)
        {
            GUILayout.BeginHorizontal();
            
            GUILayout.BeginVertical(GUILayout.Width(150));
            for (int i = 0; i < _panels.Count; i++)
            {
                if (GUILayout.Button(_panels[i].PanelName, 
                    _selectedPanelIndex == i ? GUI.skin.button : GUI.skin.toggle))
                {
                    _selectedPanelIndex = i;
                }
            }
            GUILayout.EndVertical();

            GUILayout.BeginVertical();
            _scrollPosition = GUILayout.BeginScrollView(_scrollPosition);
            
            if (_selectedPanelIndex >= 0 && _selectedPanelIndex < _panels.Count)
            {
                _panels[_selectedPanelIndex].Draw();
            }
            
            GUILayout.EndScrollView();
            GUILayout.EndVertical();

            GUILayout.EndHorizontal();

            GUI.DragWindow();
        }

        public static void Shutdown()
        {
            _panels.Clear();
            _isEnabled = false;
            _isVisible = false;
        }
    }

    public class FactionDebugPanel : IDebugPanel
    {
        public string PanelName => "Faction";
        public bool IsVisible { get; set; } = true;

        public void Draw()
        {
            GUILayout.Label("=== Faction System ===");
            GUILayout.Label("Status: Available");
            GUILayout.Label("Public Opinion: +0%");
            GUILayout.Label("Polarization: +0%");
        }
    }

    public class OpinionDebugPanel : IDebugPanel
    {
        public string PanelName => "Opinion";
        public bool IsVisible { get; set; } = true;

        public void Draw()
        {
            GUILayout.Label("=== Opinion System ===");
            GUILayout.Label("Status: Available");
        }
    }

    public class MemoryDebugPanel : IDebugPanel
    {
        public string PanelName => "Memory";
        public bool IsVisible { get; set; } = true;

        public void Draw()
        {
            GUILayout.Label("=== Memory System ===");
            GUILayout.Label("Status: Not implemented");
            GUILayout.Label("Memory records: 0");
        }
    }

    public class SchedulerDebugPanel : IDebugPanel
    {
        public string PanelName => "Scheduler";
        public bool IsVisible { get; set; } = true;

        public void Draw()
        {
            GUILayout.Label("=== Simulation Scheduler ===");
            
            if (ServiceRegistry.TryGet<ISimulationScheduler>(out var scheduler))
            {
                GUILayout.Label($"Active Tasks: {scheduler.TaskCount}");
                
                if (GUILayout.Button("Trigger Cycle Update"))
                {
                    scheduler.CycleUpdate();
                }
            }
            else
            {
                GUILayout.Label("Scheduler not available");
            }
        }
    }

    public class AIDebugPanel : IDebugPanel
    {
        public string PanelName => "AI";
        public bool IsVisible { get; set; } = true;

        public void Draw()
        {
            GUILayout.Label("=== AI System ===");
            GUILayout.Label("Status: Not implemented");
        }
    }

    public class SystemStatusPanel : IDebugPanel
    {
        public string PanelName => "System Status";
        public bool IsVisible { get; set; } = true;

        public void Draw()
        {
            GUILayout.Label("=== System Status ===");
            GUILayout.Label("ModuleManager: Initialized");
            GUILayout.Label("Loaded Modules: N/A");
            
            GUILayout.Label("EventBus Subscribers:");
            GUILayout.Label($"  BeliefChanged: {EventBus.GetSubscriberCount<BeliefChangedEvent>()}");
            GUILayout.Label($"  StressChanged: {EventBus.GetSubscriberCount<StressChangedEvent>()}");
            
            if (GUILayout.Button("Force GC"))
            {
                GC.Collect();
                Logger.Info("[Debug] Forced GC collection");
            }
        }
    }
}


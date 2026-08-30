using System.Reflection;
using UnityEngine;

namespace ONIModPack.Core.Compat
{
    public static class StressMonitorAdapter
    {
        private static readonly MethodInfo _addStressMethod;
        private static readonly MethodInfo _removeStressMethod;
        private static readonly PropertyInfo _stressProperty;
        private static readonly PropertyInfo _stressValueProperty;
        private static readonly FieldInfo _stressField;
        private static readonly PropertyInfo _instanceProperty;

        static StressMonitorAdapter()
        {
            var stressMonitorType = typeof(StressMonitor);
            _addStressMethod = stressMonitorType.GetMethod("AddStress", new[] { typeof(float) });
            _removeStressMethod = stressMonitorType.GetMethod("RemoveStress", new[] { typeof(float) });
            _instanceProperty = stressMonitorType.GetProperty("Instance");
            
            if (_instanceProperty != null)
            {
                var instanceType = _instanceProperty.PropertyType;
                _stressProperty = instanceType.GetProperty("stress");
                _stressValueProperty = instanceType.GetProperty("stressValue");
                _stressField = instanceType.GetField("stress");
            }
        }

        public static float GetStress(StressMonitor monitor)
        {
            if (monitor == null) return 0f;

            if (_instanceProperty != null)
            {
                var instance = _instanceProperty.GetValue(monitor);
                if (instance != null)
                {
                    if (_stressValueProperty != null)
                    {
                        return (float)_stressValueProperty.GetValue(instance);
                    }
                    if (_stressProperty != null)
                    {
                        var stressObj = _stressProperty.GetValue(instance);
                        if (stressObj != null)
                        {
                            var valueProp = stressObj.GetType().GetProperty("value");
                            if (valueProp != null)
                            {
                                return (float)valueProp.GetValue(stressObj);
                            }
                        }
                    }
                    if (_stressField != null)
                    {
                        var stressObj = _stressField.GetValue(instance);
                        if (stressObj != null)
                        {
                            var valueProp = stressObj.GetType().GetProperty("value");
                            if (valueProp != null)
                            {
                                return (float)valueProp.GetValue(stressObj);
                            }
                        }
                    }
                }
            }

            return 0f;
        }

        public static void AddStress(StressMonitor monitor, float amount)
        {
            if (monitor == null) return;

            if (_addStressMethod != null)
            {
                _addStressMethod.Invoke(monitor, new object[] { amount });
                return;
            }

            if (_instanceProperty != null)
            {
                var instance = _instanceProperty.GetValue(monitor);
                if (instance != null)
                {
                    var addStressMethod = instance.GetType().GetMethod("AddStress", new[] { typeof(float) });
                    if (addStressMethod != null)
                    {
                        addStressMethod.Invoke(instance, new object[] { amount });
                    }
                }
            }
        }

        public static void RemoveStress(StressMonitor monitor, float amount)
        {
            if (monitor == null) return;

            if (_removeStressMethod != null)
            {
                _removeStressMethod.Invoke(monitor, new object[] { amount });
                return;
            }

            if (_instanceProperty != null)
            {
                var instance = _instanceProperty.GetValue(monitor);
                if (instance != null)
                {
                    var removeStressMethod = instance.GetType().GetMethod("RemoveStress", new[] { typeof(float) });
                    if (removeStressMethod != null)
                    {
                        removeStressMethod.Invoke(instance, new object[] { amount });
                    }
                }
            }
        }

        public static void SetStressToMax(StressMonitor monitor)
        {
            if (monitor == null) return;

            try
            {
                if (_instanceProperty != null)
                {
                    var instance = _instanceProperty.GetValue(monitor);
                    if (instance != null)
                    {
                        var stressValueField = instance.GetType().GetField("stressValue",
                            BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                        if (stressValueField != null)
                        {
                            stressValueField.SetValue(instance, 1f);
                        }

                        var stressProperty = instance.GetType().GetProperty("stress",
                            BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                        if (stressProperty != null && stressProperty.CanWrite)
                        {
                            stressProperty.SetValue(instance, 1f);
                        }

                        var triggerMethod = instance.GetType().GetMethod("TriggerStress",
                            BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                        if (triggerMethod != null)
                        {
                            triggerMethod.Invoke(instance, null);
                        }
                    }
                }

                var monitorStressField = typeof(StressMonitor).GetField("stress",
                    BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                if (monitorStressField != null)
                {
                    monitorStressField.SetValue(monitor, 1f);
                }

                ModLogger.Info("[StressMonitorAdapter] Stress set to max, destructive behavior should trigger");
            }
            catch (System.Exception ex)
            {
                ModLogger.Error($"[StressMonitorAdapter] Failed to set stress to max: {ex.Message}");
            }
        }
    }
}
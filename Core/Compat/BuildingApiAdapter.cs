using UnityEngine;
using System;
using System.Reflection;
using System.Collections.Generic;
using System.Linq;

namespace ONIModPack.Core.Compat
{
    public static class BuildingApiAdapter
    {
        private static Type _effectorValuesType;
        private static Type _buildLocationRuleType;
        private static Type _overlayModesType;
        private static object _effectorValuesNone;
        private static object _buildLocationRuleOnFloor;
        private static MethodInfo _createBuildingDefMethod;
        private static MethodInfo _createAndRegisterBuildingMethod;
        private static PropertyInfo _generatorWattageRatingProp;
        private static PropertyInfo _generatorBaseCapacityProp;
        private static PropertyInfo _buildingDefRequiresPowerInputProp;
        private static PropertyInfo _buildingDefEnergyConsumptionWhenActiveProp;
        private static PropertyInfo _buildingDefViewModeProp;
        private static PropertyInfo _buildingDefAudioCategoryProp;
        private static PropertyInfo _buildingDefFloodableProp;
        private static PropertyInfo _buildingDefEntombableProp;
        private static PropertyInfo _buildingDefOverheatableProp;
        private static PropertyInfo _buildingDefSceneLayerProp;
        private static PropertyInfo _buildingDefObjectLayerProp;
        private static PropertyInfo _buildingDefCategoriesProp;
        private static PropertyInfo _buildingDefTechRequiredProp;
        private static PropertyInfo _buildingDefIdProp;
        private static PropertyInfo _buildingDefNameProp;
        private static PropertyInfo _buildingDefDescriptionProp;
        private static bool _initialized = false;

        public static void EnsureInitialized()
        {
            if (!_initialized)
            {
                InitializeReflection();
                _initialized = true;
            }
        }

        private static void InitializeReflection()
        {
            try
            {
                _effectorValuesType = Type.GetType("EffectorValues, Assembly-CSharp");
                _buildLocationRuleType = Type.GetType("BuildLocationRule, Assembly-CSharp");
                _overlayModesType = Type.GetType("OverlayModes, Assembly-CSharp");

                if (_effectorValuesType != null)
                {
                    var noneField = _effectorValuesType.GetField("none", BindingFlags.Public | BindingFlags.Static);
                    if (noneField != null)
                    {
                        _effectorValuesNone = noneField.GetValue(null);
                    }
                    else
                    {
                        _effectorValuesNone = Activator.CreateInstance(_effectorValuesType);
                    }
                }

                if (_buildLocationRuleType != null)
                {
                    var onFloorField = _buildLocationRuleType.GetField("OnFloor", BindingFlags.Public | BindingFlags.Static);
                    if (onFloorField != null)
                    {
                        _buildLocationRuleOnFloor = onFloorField.GetValue(null);
                    }
                    else
                    {
                        _buildLocationRuleOnFloor = Activator.CreateInstance(_buildLocationRuleType);
                    }
                }

                Type buildingTemplatesType = Type.GetType("BuildingTemplates, Assembly-CSharp");
                if (buildingTemplatesType != null)
                {
                    _createBuildingDefMethod = buildingTemplatesType.GetMethod("CreateBuildingDef", BindingFlags.Public | BindingFlags.Static);
                    
                    string[] methodNames = new[] { "CreateAndRegisterBuilding", "CreateAndRegister", "RegisterBuilding", "Register", "AddBuilding", "Add" };
                    foreach (var methodName in methodNames)
                    {
                        _createAndRegisterBuildingMethod = buildingTemplatesType.GetMethod(methodName, BindingFlags.Public | BindingFlags.Static);
                        if (_createAndRegisterBuildingMethod != null)
                        {
                            var paramsInfo = _createAndRegisterBuildingMethod.GetParameters();
                            string paramStr = string.Join(", ", paramsInfo.Select(p => $"{p.ParameterType.Name} {p.Name}"));
                            ONIModPack.Core.ModLogger.Info($"[BuildingApiAdapter] Found method in BuildingTemplates: {methodName}, signature: {paramStr}");
                            break;
                        }
                    }
                    
                    if (_createAndRegisterBuildingMethod == null)
                    {
                        var allMethods = buildingTemplatesType.GetMethods(BindingFlags.Public | BindingFlags.Static);
                        foreach (var method in allMethods)
                        {
                            if (method.Name.Contains("Register") || method.Name.Contains("Add"))
                            {
                                _createAndRegisterBuildingMethod = method;
                                var paramsInfo = method.GetParameters();
                                string paramStr = string.Join(", ", paramsInfo.Select(p => $"{p.ParameterType.Name} {p.Name}"));
                                ONIModPack.Core.ModLogger.Info($"[BuildingApiAdapter] Found candidate method: {method.Name}, signature: {paramStr}");
                                break;
                            }
                        }
                    }
                    
                    if (_createAndRegisterBuildingMethod == null)
                    {
                        ONIModPack.Core.ModLogger.Warning("[BuildingApiAdapter] CreateAndRegisterBuilding method not found in BuildingTemplates");
                    }
                }

                Type buildingDefType = Type.GetType("BuildingDef, Assembly-CSharp");
                if (buildingDefType != null)
                {
                    _generatorWattageRatingProp = buildingDefType.GetProperty("GeneratorWattageRating");
                    _generatorBaseCapacityProp = buildingDefType.GetProperty("GeneratorBaseCapacity");
                    _buildingDefRequiresPowerInputProp = buildingDefType.GetProperty("RequiresPowerInput");
                    _buildingDefEnergyConsumptionWhenActiveProp = buildingDefType.GetProperty("EnergyConsumptionWhenActive");
                    _buildingDefViewModeProp = buildingDefType.GetProperty("ViewMode");
                    _buildingDefAudioCategoryProp = buildingDefType.GetProperty("AudioCategory");
                    _buildingDefFloodableProp = buildingDefType.GetProperty("Floodable");
                    _buildingDefEntombableProp = buildingDefType.GetProperty("Entombable");
                    _buildingDefOverheatableProp = buildingDefType.GetProperty("Overheatable");
                    _buildingDefSceneLayerProp = buildingDefType.GetProperty("SceneLayer");
                    _buildingDefObjectLayerProp = buildingDefType.GetProperty("ObjectLayer");
                    _buildingDefCategoriesProp = buildingDefType.GetProperty("Categories");
                    _buildingDefTechRequiredProp = buildingDefType.GetProperty("TechRequired");
                    _buildingDefIdProp = buildingDefType.GetProperty("Id");
                    _buildingDefNameProp = buildingDefType.GetProperty("Name");
                    _buildingDefDescriptionProp = buildingDefType.GetProperty("Description");
                }

                ONIModPack.Core.ModLogger.Info($"[BuildingApiAdapter] Reflection initialized. CreateBuildingDef: {_createBuildingDefMethod != null}, CreateAndRegisterBuilding: {_createAndRegisterBuildingMethod != null}, EffectorValues: {_effectorValuesType != null}, BuildLocationRule: {_buildLocationRuleType != null}");
            }
            catch (Exception ex)
            {
                ONIModPack.Core.ModLogger.Error($"[BuildingApiAdapter] Failed to initialize reflection: {ex.Message}");
            }
        }

        public static object GetEffectorValuesNone()
        {
            return _effectorValuesNone;
        }

        public static object GetBuildLocationRuleOnFloor()
        {
            return _buildLocationRuleOnFloor;
        }

        public static object GetPowerOverlayModeId()
        {
            if (_overlayModesType == null) return "Power";
            PropertyInfo powerProp = _overlayModesType.GetProperty("Power");
            if (powerProp == null) return "Power";
            object powerObj = powerProp.GetValue(null);
            PropertyInfo idProp = powerObj?.GetType().GetProperty("ID");
            return idProp?.GetValue(powerObj) ?? "Power";
        }

        public static object CreateBuildingDef(string id, int width, int height, string anim, int hitpoints, 
            float constructionTime, float[] constructionMass, string[] constructionMaterials, 
            float meltingPoint = 800f, object buildLocationRule = null, 
            object noise = null, object decor = null, float temperatureModifier = 0.2f)
        {
            EnsureInitialized();
            if (_createBuildingDefMethod == null) return null;

            try
            {
                var methodParams = _createBuildingDefMethod.GetParameters();
                
                bool usedId = false, usedAnim = false;
                bool usedWidth = false, usedHeight = false, usedHitpoints = false;
                bool usedConstructionTime = false, usedMeltingPoint = false, usedTempModifier = false;
                bool usedMass = false, usedMaterials = false;
                
                List<object> args = new List<object>();
                
                foreach (var param in methodParams)
                {
                    string typeName = param.ParameterType.Name;
                    
                    if (param.ParameterType == typeof(string))
                    {
                        if (!usedId) { args.Add(id); usedId = true; }
                        else if (!usedAnim) { args.Add(anim); usedAnim = true; }
                        else args.Add(string.Empty);
                    }
                    else if (param.ParameterType == typeof(int))
                    {
                        if (!usedWidth) { args.Add(width); usedWidth = true; }
                        else if (!usedHeight) { args.Add(height); usedHeight = true; }
                        else if (!usedHitpoints) { args.Add(hitpoints); usedHitpoints = true; }
                        else args.Add(0);
                    }
                    else if (param.ParameterType == typeof(float))
                    {
                        if (!usedConstructionTime) { args.Add(constructionTime); usedConstructionTime = true; }
                        else if (!usedMeltingPoint) { args.Add(meltingPoint); usedMeltingPoint = true; }
                        else if (!usedTempModifier) { args.Add(temperatureModifier); usedTempModifier = true; }
                        else args.Add(0f);
                    }
                    else if (param.ParameterType.IsArray && param.ParameterType.GetElementType() == typeof(float) && !usedMass)
                    {
                        args.Add(constructionMass);
                        usedMass = true;
                    }
                    else if (param.ParameterType.IsArray && param.ParameterType.GetElementType() == typeof(string) && !usedMaterials)
                    {
                        args.Add(constructionMaterials);
                        usedMaterials = true;
                    }
                    else if (typeName == "EffectorValues")
                    {
                        args.Add(GetEffectorValuesNone());
                    }
                    else if (typeName == "BuildLocationRule")
                    {
                        args.Add(GetBuildLocationRuleOnFloor());
                    }
                    else if (param.ParameterType.IsEnum)
                    {
                        args.Add(0);
                    }
                    else
                    {
                        args.Add(null);
                    }
                }

                var result = _createBuildingDefMethod.Invoke(null, args.ToArray());
                if (result != null)
                {
                    ONIModPack.Core.ModLogger.Debug($"[BuildingApiAdapter] CreateBuildingDef succeeded: {result.GetType().Name}");
                }
                return result;
            }
            catch (Exception ex)
            {
                ONIModPack.Core.ModLogger.Error($"[BuildingApiAdapter] CreateBuildingDef failed: {ex.Message}");
                return null;
            }
        }

        public static void SetGeneratorWattageRating(object buildingDef, float value)
        {
            _generatorWattageRatingProp?.SetValue(buildingDef, value);
        }

        public static void SetGeneratorBaseCapacity(object buildingDef, float value)
        {
            _generatorBaseCapacityProp?.SetValue(buildingDef, value);
        }

        public static void SetRequiresPowerInput(object buildingDef, bool value)
        {
            _buildingDefRequiresPowerInputProp?.SetValue(buildingDef, value);
        }

        public static void SetEnergyConsumptionWhenActive(object buildingDef, float value)
        {
            _buildingDefEnergyConsumptionWhenActiveProp?.SetValue(buildingDef, value);
        }

        public static void SetViewMode(object buildingDef, object value)
        {
            _buildingDefViewModeProp?.SetValue(buildingDef, value);
        }

        public static void SetAudioCategory(object buildingDef, string value)
        {
            _buildingDefAudioCategoryProp?.SetValue(buildingDef, value);
        }

        public static void SetFloodable(object buildingDef, bool value)
        {
            _buildingDefFloodableProp?.SetValue(buildingDef, value);
        }

        public static void SetEntombable(object buildingDef, bool value)
        {
            _buildingDefEntombableProp?.SetValue(buildingDef, value);
        }

        public static void SetOverheatable(object buildingDef, bool value)
        {
            _buildingDefOverheatableProp?.SetValue(buildingDef, value);
        }

        public static void SetSceneLayer(object buildingDef, object value)
        {
            _buildingDefSceneLayerProp?.SetValue(buildingDef, value);
        }

        public static void SetObjectLayer(object buildingDef, object value)
        {
            _buildingDefObjectLayerProp?.SetValue(buildingDef, value);
        }

        public static void SetCategories(object buildingDef, string[] value)
        {
            _buildingDefCategoriesProp?.SetValue(buildingDef, value);
        }

        public static void SetTechRequired(object buildingDef, string value)
        {
            _buildingDefTechRequiredProp?.SetValue(buildingDef, value);
        }

        public static string GetBuildingDefId(object buildingDef)
        {
            return _buildingDefIdProp?.GetValue(buildingDef) as string ?? string.Empty;
        }

        public static string GetBuildingDefName(object buildingDef)
        {
            return _buildingDefNameProp?.GetValue(buildingDef) as string ?? string.Empty;
        }

        public static string GetBuildingDefDescription(object buildingDef)
        {
            return _buildingDefDescriptionProp?.GetValue(buildingDef) as string ?? string.Empty;
        }

        public static GameObject CreateAndRegisterBuilding(string id, string name, string description, 
            object buildingDef, Type componentType, string[] categories, string techRequired)
        {
            EnsureInitialized();
            
            if (_createAndRegisterBuildingMethod != null)
            {
                try
                {
                    var methodParams = _createAndRegisterBuildingMethod.GetParameters();
                    List<object> args = new List<object>();
                    
                    foreach (var param in methodParams)
                    {
                        if (param.ParameterType == typeof(string))
                        {
                            if (args.Count == 0) args.Add(id);
                            else if (args.Count == 1) args.Add(name);
                            else if (args.Count == 2) args.Add(description);
                            else if (args.Count == 6) args.Add(techRequired);
                            else args.Add(string.Empty);
                        }
                        else if (buildingDef != null && param.ParameterType.IsAssignableFrom(buildingDef.GetType()))
                        {
                            args.Add(buildingDef);
                        }
                        else if (param.ParameterType == typeof(Type))
                        {
                            args.Add(componentType);
                        }
                        else if (param.ParameterType.IsArray && param.ParameterType.GetElementType() == typeof(string))
                        {
                            args.Add(categories);
                        }
                        else if (param.ParameterType.IsEnum || param.ParameterType == typeof(int))
                        {
                            args.Add(0);
                        }
                        else if (param.ParameterType == typeof(bool))
                        {
                            args.Add(false);
                        }
                        else
                        {
                            args.Add(null);
                        }
                    }

                    var result = _createAndRegisterBuildingMethod.Invoke(null, args.ToArray());
                    var go = result as GameObject;
                    if (go != null)
                    {
                        ONIModPack.Core.ModLogger.Info($"[BuildingApiAdapter] CreateAndRegisterBuilding succeeded: {go.name}");
                    }
                    return go;
                }
                catch (Exception ex)
                {
                    ONIModPack.Core.ModLogger.Error($"[BuildingApiAdapter] CreateAndRegisterBuilding failed: {ex.Message}");
                }
            }
            
            return RegisterBuildingDirect(buildingDef, id, name, description, componentType);
        }

        private static GameObject RegisterBuildingDirect(object buildingDef, string id, string name, string description, Type componentType)
        {
            try
            {
                Type dbType = Type.GetType("Db, Assembly-CSharp");
                if (dbType == null) return null;

                PropertyInfo buildingsProp = dbType.GetProperty("Buildings");
                if (buildingsProp == null) return null;

                object buildings = buildingsProp.GetValue(null);
                if (buildings == null) return null;

                MethodInfo registerMethod = buildings.GetType().GetMethod("Register", BindingFlags.Public | BindingFlags.Instance);
                if (registerMethod != null)
                {
                    var result = registerMethod.Invoke(buildings, new object[] { buildingDef });
                    if (result != null)
                    {
                        ONIModPack.Core.ModLogger.Info($"[BuildingApiAdapter] Registered building via Db.Buildings.Register: {id}");
                        return result as GameObject;
                    }
                }

                MethodInfo addMethod = buildings.GetType().GetMethod("Add", BindingFlags.Public | BindingFlags.Instance);
                if (addMethod != null)
                {
                    var result = addMethod.Invoke(buildings, new object[] { id, buildingDef });
                    if (result != null)
                    {
                        ONIModPack.Core.ModLogger.Info($"[BuildingApiAdapter] Registered building via Db.Buildings.Add: {id}");
                        return result as GameObject;
                    }
                }
            }
            catch (Exception ex)
            {
                ONIModPack.Core.ModLogger.Error($"[BuildingApiAdapter] RegisterBuildingDirect failed: {ex.Message}");
            }

            return null;
        }

        public static void AddComponentByName(GameObject go, string componentName)
        {
            Type componentType = Type.GetType($"{componentName}, Assembly-CSharp");
            if (componentType != null)
            {
                go.AddComponent(componentType);
            }
        }

        public static void RegisterBuildingStrings(string id, string displayName, string description)
        {
            Type stringsType = Type.GetType("Strings, Assembly-CSharp");
            if (stringsType != null)
            {
                MethodInfo addMethod = stringsType.GetMethod("Add", new[] { typeof(string), typeof(string) });
                if (addMethod != null)
                {
                    string idUpper = id.ToUpper();
                    try
                    {
                        addMethod.Invoke(null, new object[] { $"STRINGS.BUILDINGS.PREFABS.{idUpper}.NAME", displayName });
                        addMethod.Invoke(null, new object[] { $"STRINGS.BUILDINGS.PREFABS.{idUpper}.DESC", description });
                        addMethod.Invoke(null, new object[] { $"STRINGS.BUILDINGS.PREFABS.{idUpper}.EFFECT", description });
                    }
                    catch (Exception ex)
                    {
                        ONIModPack.Core.ModLogger.Warning($"[BuildingApiAdapter] Failed to register strings for {id}: {ex.Message}");
                    }
                }
            }
        }

        public static void AddBuildingToPlanScreen(string category, string buildingId)
        {
            Type modUtilType = Type.GetType("ModUtil, Assembly-CSharp");
            if (modUtilType != null)
            {
                MethodInfo addMethod = modUtilType.GetMethod("AddBuildingToPlanScreen", 
                    new[] { typeof(string), typeof(string) });
                if (addMethod != null)
                {
                    try
                    {
                        addMethod.Invoke(null, new object[] { category, buildingId });
                    }
                    catch (Exception ex)
                    {
                        ONIModPack.Core.ModLogger.Warning($"[BuildingApiAdapter] Failed to add {buildingId} to {category} category: {ex.Message}");
                    }
                }
            }
        }
    }

    public static class GeneratorAdapter
    {
        private static PropertyInfo _powerRatingProp;
        private static PropertyInfo _baseCapacityProp;

        static GeneratorAdapter()
        {
            Type generatorType = Type.GetType("Generator, Assembly-CSharp");
            if (generatorType != null)
            {
                _powerRatingProp = generatorType.GetProperty("PowerRating") ?? 
                    generatorType.GetProperty("nominalCapacity");
                _baseCapacityProp = generatorType.GetProperty("BaseCapacity") ?? 
                    generatorType.GetProperty("nominalCapacity");
            }
        }

        public static void SetPowerRating(object generator, float value)
        {
            _powerRatingProp?.SetValue(generator, value);
        }

        public static void SetBaseCapacity(object generator, float value)
        {
            _baseCapacityProp?.SetValue(generator, value);
        }
    }

    public static class Light2DAdapter
    {
        private static PropertyInfo _intensityProp;
        private static EventInfo _onEnabledChanged;
        private static EventInfo _onDisabledChanged;

        static Light2DAdapter()
        {
            Type light2DType = Type.GetType("Light2D, UnityEngine.Experimental.Rendering.Universal") ??
                               Type.GetType("Light2D, UnityEngine.UIElements");
            
            if (light2DType == null)
            {
                foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
                {
                    light2DType = assembly.GetType("UnityEngine.Light2D");
                    if (light2DType != null) break;
                }
            }

            if (light2DType != null)
            {
                _intensityProp = light2DType.GetProperty("intensity");
                _onEnabledChanged = light2DType.GetEvent("enabled");
                _onDisabledChanged = light2DType.GetEvent("disabled");
            }
        }

        public static void SetIntensity(object light2D, float value)
        {
            _intensityProp?.SetValue(light2D, value);
        }
    }

    public static class AssetsAdapter
    {
        private static MethodInfo _getAnimMethod;

        static AssetsAdapter()
        {
            Type assetsType = Type.GetType("Assets, Assembly-CSharp");
            if (assetsType != null)
            {
                _getAnimMethod = assetsType.GetMethod("GetAnim", new[] { typeof(string) });
            }
        }

        public static object GetAnim(string animName)
        {
            return _getAnimMethod?.Invoke(null, new object[] { animName });
        }
    }

    public static class TechAdapter
    {
        private static Type _techType;
        private static ConstructorInfo _techConstructor;
        private static PropertyInfo _nameProp;
        private static PropertyInfo _descriptionProp;
        private static PropertyInfo _costProp;
        private static MethodInfo _techsAddMethod;

        static TechAdapter()
        {
            _techType = Type.GetType("Tech, Assembly-CSharp");
            
            if (_techType != null)
            {
                var constructors = _techType.GetConstructors(BindingFlags.Public | BindingFlags.Instance);
                foreach (var ctor in constructors)
                {
                    var paramsInfo = ctor.GetParameters();
                    if (paramsInfo.Length >= 1 && paramsInfo[0].ParameterType == typeof(string))
                    {
                        _techConstructor = ctor;
                        break;
                    }
                }

                _nameProp = _techType.GetProperty("Name");
                _descriptionProp = _techType.GetProperty("Description");
                _costProp = _techType.GetProperty("Cost") ?? _techType.GetProperty("cost");
            }

            Type techsType = Type.GetType("Techs, Assembly-CSharp");
            if (techsType != null)
            {
                _techsAddMethod = techsType.GetMethod("Add", new[] { typeof(string), _techType });
            }
        }

        public static object CreateTech(string id, List<string> dependencies, object techs)
        {
            if (_techConstructor == null) return null;

            try
            {
                var paramsInfo = _techConstructor.GetParameters();
                List<object> args = new List<object>();
                args.Add(id);

                if (paramsInfo.Length > 1 && paramsInfo[1].ParameterType == typeof(List<string>))
                {
                    args.Add(dependencies);
                }

                if (paramsInfo.Length > 2 && techs != null)
                {
                    args.Add(techs);
                }

                return _techConstructor.Invoke(args.ToArray());
            }
            catch
            {
                return null;
            }
        }

        public static void SetTechName(object tech, string name)
        {
            _nameProp?.SetValue(tech, name);
        }

        public static void SetTechDescription(object tech, string description)
        {
            _descriptionProp?.SetValue(tech, description);
        }

        public static void SetTechCost(object tech, float cost)
        {
            _costProp?.SetValue(tech, cost);
        }

        public static bool AddTech(object techs, string id, object tech)
        {
            try
            {
                _techsAddMethod?.Invoke(techs, new[] { id, tech });
                return true;
            }
            catch
            {
                return false;
            }
        }

        public static bool TechExists(object techs, string id)
        {
            try
            {
                MethodInfo existsMethod = techs.GetType().GetMethod("Exists", new[] { typeof(string) });
                if (existsMethod != null)
                {
                    return (bool)existsMethod.Invoke(techs, new object[] { id });
                }
            }
            catch { }
            return false;
        }
    }
}
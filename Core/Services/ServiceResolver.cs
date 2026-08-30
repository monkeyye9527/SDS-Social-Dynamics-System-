using System;

namespace ONIModPack.Core.Services
{
    public static class ServiceResolver
    {
        public static T RequireService<T>(string context = "") where T : IService
        {
            if (ServiceRegistry.TryGet(out T service))
            {
                return service;
            }

            var typeName = typeof(T).Name;
            var contextMsg = string.IsNullOrEmpty(context) ? "" : $" (context: {context})";
            ModLogger.Fatal($"[ServiceResolver] Required service {typeName} not found{contextMsg}");
            throw new InvalidOperationException($"Required service {typeName} is not registered");
        }

        public static T OptionalService<T>() where T : IService
        {
            if (ServiceRegistry.TryGet(out T service))
            {
                return service;
            }

            return default;
        }

        public static T OptionalService<T>(string context) where T : IService
        {
            if (ServiceRegistry.TryGet(out T service))
            {
                return service;
            }

            ModLogger.Warning($"[ServiceResolver] Optional service {typeof(T).Name} not found (context: {context})");
            return default;
        }

        public static bool TryResolve<T>(out T service, string context = "") where T : IService
        {
            if (ServiceRegistry.TryGet(out service))
            {
                return true;
            }

            if (!string.IsNullOrEmpty(context))
            {
                ModLogger.Debug($"[ServiceResolver] Service {typeof(T).Name} not available (context: {context})");
            }
            return false;
        }
    }
}
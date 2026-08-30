using System.Collections.Generic;
using ONIModPack.Core.Services;

namespace ONIModPack.Core.Simulation
{
    public interface IPartitionedUpdater : IService
    {
        void RegisterSystem(IPartitionedSystem system);
        void UnregisterSystem(string systemName);
        IPartitionedSystem GetSystem(string systemName);
        List<string> GetAllSystemNames();
    }
}

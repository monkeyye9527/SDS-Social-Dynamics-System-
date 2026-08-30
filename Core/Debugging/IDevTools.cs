using System;
using System.Collections.Generic;
using ONIModPack.Core.Services;

namespace ONIModPack.Core.Debugging
{
    public interface IDevTools : IService
    {
        event Action<string> OnCommandExecuted;
        event Action<string, string> OnCommandError;

        void RegisterCommand(DevCommand command);
        void UnregisterCommand(string commandName);
        bool ExecuteCommand(string commandLine);
        List<DevCommand> GetAllCommands();
        bool IsEnabled { get; }
        void SetEnabled(bool enabled);
    }
}

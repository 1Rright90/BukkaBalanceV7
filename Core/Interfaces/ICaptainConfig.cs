using System.Collections.Generic;
using TaleWorlds.Library;

namespace YSBCaptain.Core.Interfaces
{
    public interface ICaptainConfig
    {
        string ModVersion { get; }
        bool IsDebugMode { get; set; }
        int MaxPlayers { get; set; }
        float UpdateInterval { get; set; }
        Vec3 SpawnPoint { get; set; }
        Dictionary<string, object> CustomSettings { get; }
        
        void LoadConfig(string configPath);
        void SaveConfig(string configPath);
        void ResetToDefaults();
        bool ValidateConfig();
    }
}

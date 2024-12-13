using TaleWorlds.MountAndBlade;
using TaleWorlds.Core;
using TaleWorlds.Library;
using System.Collections.Generic;

namespace TaleWorlds.MountAndBlade.Captain
{
    /// <summary>
    /// Core functionality for Captain mode
    /// </summary>
    public static class CaptainCore
    {
        public static readonly int MAX_FORMATION_SIZE = 9999;
        public static readonly float DEFAULT_FORMATION_SPACING = 1.0f;
        
        public static bool IsValidFormation(Formation formation)
        {
            return formation != null && formation.CountOfUnits <= MAX_FORMATION_SIZE;
        }

        public static float GetDefaultSpacing(Formation formation)
        {
            if (formation == null) return DEFAULT_FORMATION_SPACING;
            
            return formation.FormationIndex switch
            {
                (int)FormationClass.Infantry => DEFAULT_FORMATION_SPACING,
                (int)FormationClass.Ranged => DEFAULT_FORMATION_SPACING * 1.2f,
                (int)FormationClass.Cavalry => DEFAULT_FORMATION_SPACING * 2.0f,
                (int)FormationClass.HorseArcher => DEFAULT_FORMATION_SPACING * 2.5f,
                _ => DEFAULT_FORMATION_SPACING
            };
        }
    }
}

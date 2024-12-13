using TaleWorlds.MountAndBlade;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace TaleWorlds.MountAndBlade.Captain.Models
{
    public class CaptainFormationModel
    {
        public Formation Formation { get; }
        public Vec2 TargetPosition { get; set; }
        public float TargetRotation { get; set; }
        public float Width { get; set; }
        public float Depth { get; set; }
        public int UnitCount { get; set; }
        public bool IsValid => Formation != null && UnitCount <= CaptainCore.MAX_FORMATION_SIZE;

        public CaptainFormationModel(Formation formation)
        {
            Formation = formation;
            if (formation != null)
            {
                UnitCount = formation.CountOfUnits;
                TargetPosition = formation.OrderPosition.AsVec2;
                TargetRotation = formation.Direction;
                Width = formation.Width;
                Depth = formation.Depth;
            }
        }
    }
}

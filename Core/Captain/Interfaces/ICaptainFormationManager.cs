using System.Collections.Generic;
using TaleWorlds.MountAndBlade;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade.Captain.Models;

namespace TaleWorlds.MountAndBlade.Captain
{
    public interface ICaptainFormationManager
    {
        void UpdateFormation(Formation formation);
        void SetFormationTarget(Formation formation, Vec2 position, float direction);
        CaptainFormationModel GetFormationModel(Formation formation);
        IEnumerable<Formation> GetActiveFormations();
        bool IsFormationValid(Formation formation);
    }
}

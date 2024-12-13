using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using TaleWorlds.MountAndBlade;
using TaleWorlds.Library;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade.Captain.Models;
using Microsoft.Extensions.Logging;

namespace TaleWorlds.MountAndBlade.Captain
{
    public class CaptainFormationManager : ICaptainFormationManager
    {
        private readonly ILogger<CaptainFormationManager> _logger;
        private readonly ConcurrentDictionary<Formation, CaptainFormationModel> _formationModels;

        public CaptainFormationManager(ILogger<CaptainFormationManager> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _formationModels = new ConcurrentDictionary<Formation, CaptainFormationModel>();
        }

        public void UpdateFormation(Formation formation)
        {
            if (formation == null) return;

            try
            {
                var model = GetOrCreateModel(formation);
                model.UnitCount = formation.CountOfUnits;
                model.Width = formation.Width;
                model.Depth = formation.Depth;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error updating formation {formation.Index}");
            }
        }

        public void SetFormationTarget(Formation formation, Vec2 position, float direction)
        {
            if (formation == null) return;

            try
            {
                var model = GetOrCreateModel(formation);
                model.TargetPosition = position;
                model.TargetRotation = direction;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error setting formation target for {formation.Index}");
            }
        }

        public CaptainFormationModel GetFormationModel(Formation formation)
        {
            return formation != null ? GetOrCreateModel(formation) : null;
        }

        public IEnumerable<Formation> GetActiveFormations()
        {
            return _formationModels.Keys.Where(f => f != null && !f.IsEmpty);
        }

        public bool IsFormationValid(Formation formation)
        {
            return CaptainCore.IsValidFormation(formation);
        }

        private CaptainFormationModel GetOrCreateModel(Formation formation)
        {
            return _formationModels.GetOrAdd(formation, f => new CaptainFormationModel(f));
        }
    }
}

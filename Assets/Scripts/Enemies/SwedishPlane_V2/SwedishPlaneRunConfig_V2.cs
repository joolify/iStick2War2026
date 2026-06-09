using System;
using UnityEngine;

namespace iStick2War_V2
{
    // Spawn-time tuning for one Swedish supply pass (Survival intermission).
    public sealed class SwedishPlaneRunConfig_V2
    {
        public SwedishPlanePowerUp_V2 powerUpPrefab;
        public SurvivalPowerUpCatalog_V2 catalog;
        public bool spawnedFromLeft;
        public Camera gameplayCamera;
        public Action onPassComplete;
        public int dropsThisPass = 1;
        public SwedishPlaneSurvivalCoordinator_V2 survivalCoordinator;
        public Hero_V2 hero;
    }
}

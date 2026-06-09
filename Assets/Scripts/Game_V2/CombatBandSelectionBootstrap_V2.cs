using UnityEngine;

namespace iStick2War_V2
{
    /*
     * CombatBandSelectionBootstrap_V2
     *
     * PURPOSE:
     * Keeps aspect-based CombatBand_V2 selection alive even when every profile child starts
     * inactive. Place on the always-active CombatBands parent (not on the profile children).
     */
    [DisallowMultipleComponent]
    [ExecuteAlways]
    public sealed class CombatBandSelectionBootstrap_V2 : MonoBehaviour
    {
        private void OnEnable()
        {
            CombatBand_V2.RefreshActiveSelection(force: true);
        }

        private void Update()
        {
            CombatBand_V2.RefreshActiveSelection(force: false);
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            CombatBand_V2.RefreshActiveSelection(force: true);
        }
#endif
    }
}

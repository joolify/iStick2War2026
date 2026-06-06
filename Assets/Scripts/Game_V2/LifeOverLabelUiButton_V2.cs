using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

namespace iStick2War_V2
{
    /*
     * Forwards txt_lifeOver_* canvas label clicks to LifeOver wave actions.
     * Hand-placed TMP labels on LifeOver-canvas often sit over TextBTN_Medium* sprites whose
     * Collider2D hitboxes stay offset — wire labels so clicks on the text still work.
     */
    [AddComponentMenu("iStick2War/Life Over Label UI Button V2")]
    public sealed class LifeOverLabelUiButton_V2 : MonoBehaviour, IPointerClickHandler
    {
        public enum LifeOverLabelAction
        {
            ContinueAfterLifeLost,
            GoToShopAfterLifeLost,
            GoToMainMenuAfterLifeLost
        }

        [SerializeField] private LifeOverLabelAction _action;

        private WaveManager_V2 _waveManager;

        public void Configure(LifeOverLabelAction action)
        {
            _action = action;
        }

        private void Awake()
        {
            if (_waveManager == null)
            {
                _waveManager = FindAnyObjectByType<WaveManager_V2>(FindObjectsInactive.Exclude);
            }
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (_waveManager == null || _waveManager.State != WaveLoopState_V2.LifeOver)
            {
                return;
            }

            AudioManager_V2.PlayMenuClick();
            switch (_action)
            {
                case LifeOverLabelAction.ContinueAfterLifeLost:
                    _waveManager.TryContinueAfterLifeLost();
                    break;
                case LifeOverLabelAction.GoToShopAfterLifeLost:
                    _waveManager.TryGoToShopAfterLifeLost();
                    break;
                case LifeOverLabelAction.GoToMainMenuAfterLifeLost:
                    _waveManager.TryGoToMainMenuAfterLifeLost();
                    break;
            }
        }

        internal static void EnsureOnLabel(TMP_Text label, LifeOverLabelAction action)
        {
            if (label == null)
            {
                return;
            }

            label.raycastTarget = true;
            LifeOverLabelUiButton_V2 handler = label.GetComponent<LifeOverLabelUiButton_V2>();
            if (handler == null)
            {
                handler = label.gameObject.AddComponent<LifeOverLabelUiButton_V2>();
            }

            handler.Configure(action);
        }

        internal static void EnsureInfoLabelNonBlocking(TMP_Text label)
        {
            if (label == null)
            {
                return;
            }

            label.raycastTarget = false;
            LifeOverLabelUiButton_V2 handler = label.GetComponent<LifeOverLabelUiButton_V2>();
            if (handler != null)
            {
                Destroy(handler);
            }
        }
    }
}

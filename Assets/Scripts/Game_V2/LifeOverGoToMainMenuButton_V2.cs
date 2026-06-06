using UnityEngine;

namespace iStick2War_V2
{
    /*
     * LifeOverGoToMainMenuButton_V2 (Life lost — return to main menu hit target)
     *
     * PURPOSE:
     * Collider2D + OnMouseDown on e.g. TextBTN_MediumGoToMainMenu saves the run and loads MainMenuScene.
     * Sibling TextBTN_MediumGoToMainMenu_Pressed shows while held (same pattern as other text buttons).
     *
     * NAVIGATION: WaveManager_V2.TryGoToMainMenuAfterLifeLost; shown while WaveLoopState_V2.LifeOver.
     */
    [AddComponentMenu("iStick2War/Life Over Go To Main Menu Button V2")]
    [RequireComponent(typeof(Collider2D))]
    public sealed class LifeOverGoToMainMenuButton_V2 : MonoBehaviour
    {
        [SerializeField] private WaveManager_V2 _waveManager;
        [SerializeField] private GameObject _normalVisual;
        [SerializeField] private GameObject _pressedVisual;
        [SerializeField] private bool _debugLogs;

        private bool _isPointerDown;

        private void Awake()
        {
            if (_waveManager == null)
            {
                _waveManager = FindAnyObjectByType<WaveManager_V2>(FindObjectsInactive.Exclude);
            }

            ResolveVisualPairIfNeeded();
            ShowNormalVisual();
        }

        private void OnDisable()
        {
            _isPointerDown = false;
            ShowNormalVisual();
        }

        private void OnMouseDown()
        {
            _isPointerDown = true;
            ShowPressedVisual();
            HandleClick();
        }

        private void OnMouseUp()
        {
            _isPointerDown = false;
            ShowNormalVisual();
        }

        private void OnMouseExit()
        {
            if (!_isPointerDown)
            {
                return;
            }

            _isPointerDown = false;
            ShowNormalVisual();
        }

        private void HandleClick()
        {
            if (_waveManager != null && _waveManager.State != WaveLoopState_V2.LifeOver)
            {
                return;
            }

            AudioManager_V2.PlayMenuClick();
            if (_waveManager == null)
            {
                if (_debugLogs)
                {
                    Debug.LogWarning($"[LifeOverGoToMainMenuButton_V2] '{name}': WaveManager_V2 not found.");
                }

                return;
            }

            if (_debugLogs)
            {
                Debug.Log($"[LifeOverGoToMainMenuButton_V2] '{name}' -> TryGoToMainMenuAfterLifeLost");
            }

            _waveManager.TryGoToMainMenuAfterLifeLost();
        }

        public void TriggerAutomationClick()
        {
            HandleClick();
        }

        private void ResolveVisualPairIfNeeded()
        {
            if (_normalVisual == null)
            {
                _normalVisual = gameObject;
            }

            if (_pressedVisual != null)
            {
                return;
            }

            Transform parent = _normalVisual.transform.parent;
            if (parent == null)
            {
                return;
            }

            string pressedName = _normalVisual.name + "_Pressed";
            Transform pressed = parent.Find(pressedName);
            if (pressed != null)
            {
                _pressedVisual = pressed.gameObject;
            }
        }

        private void ShowNormalVisual()
        {
            ResolveVisualPairIfNeeded();
            if (_normalVisual != null)
            {
                _normalVisual.SetActive(true);
            }

            if (_pressedVisual != null)
            {
                _pressedVisual.SetActive(false);
            }
        }

        private void ShowPressedVisual()
        {
            ResolveVisualPairIfNeeded();
            if (_normalVisual != null)
            {
                _normalVisual.SetActive(false);
            }

            if (_pressedVisual != null)
            {
                _pressedVisual.SetActive(true);
            }
        }
    }
}

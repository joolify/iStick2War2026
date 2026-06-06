using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace iStick2War_V2
{
    /*
     * GameplayPauseButton_V2 (In-run pause → MainMenuScene)
     *
     * PURPOSE:
     * World-space or UI pause control in SampleScene: saves the active run, pauses time, switches to menu music,
     * and loads MainMenuScene so the player can Continue later or start a new run.
     *
     * ---------------------------------------------------------
     * NAVIGATION (Game_V2)
     *
     * Menu boot / Continue → MainMenu_V2.cs | Run snapshot → RunSaveService_V2.cs / WaveManager_V2.cs
     */
    [AddComponentMenu("iStick2War/Gameplay Pause Button V2")]
    public sealed class GameplayPauseButton_V2 : MonoBehaviour
    {
        private const string MainMenuSceneName = "MainMenuScene";

        [SerializeField] private WaveManager_V2 _waveManager;
        [SerializeField] private bool _hideWhenPauseUnavailable = true;
        [SerializeField] private bool _debugLogs;

        private Button _uiButton;
        private bool _listenerRegistered;
        private static bool s_loadingMainMenu;

        private void Awake()
        {
            _uiButton = GetComponent<Button>();
            EnsureWorldSpaceHitTargetIfNeeded();
        }

        private void OnEnable()
        {
            RegisterUiListenerIfNeeded();
            RefreshVisibility();
        }

        private void OnDisable()
        {
            UnregisterUiListener();
        }

        private void Update()
        {
            RefreshVisibility();
        }

        private void OnMouseDown()
        {
            if (_uiButton != null)
            {
                return;
            }

            TryPauseAndReturnToMainMenu();
        }

        public void TriggerAutomationClick()
        {
            TryPauseAndReturnToMainMenu();
        }

        private void RegisterUiListenerIfNeeded()
        {
            if (_uiButton == null || _listenerRegistered)
            {
                return;
            }

            _uiButton.onClick.AddListener(TryPauseAndReturnToMainMenu);
            _listenerRegistered = true;
        }

        private void UnregisterUiListener()
        {
            if (_uiButton == null || !_listenerRegistered)
            {
                return;
            }

            _uiButton.onClick.RemoveListener(TryPauseAndReturnToMainMenu);
            _listenerRegistered = false;
        }

        private void TryPauseAndReturnToMainMenu()
        {
            if (s_loadingMainMenu)
            {
                return;
            }

            ResolveWaveManagerIfNeeded();
            if (_waveManager != null && !CanPauseFromCurrentLoopState(_waveManager.State))
            {
                if (_debugLogs)
                {
                    Debug.Log($"[GameplayPauseButton_V2] Ignored pause while state is {_waveManager.State}.");
                }

                return;
            }

            AudioManager_V2.PlayMenuClick();
            _waveManager?.PersistActiveRunSave();

            if (_debugLogs)
            {
                Debug.Log("[GameplayPauseButton_V2] Pause -> MainMenuScene (run saved when possible).");
            }

            s_loadingMainMenu = true;
            ReturnToMainMenuScene();
        }

        internal static void ReturnToMainMenuScene()
        {
            Time.timeScale = 0f;
            AudioManager_V2.SetMenuMusic();
            SceneManager.sceneLoaded -= FinishMainMenuBootAfterSceneLoad;
            SceneManager.sceneLoaded += FinishMainMenuBootAfterSceneLoad;
            SceneManager.LoadScene(MainMenuSceneName, LoadSceneMode.Single);
        }

        private static void FinishMainMenuBootAfterSceneLoad(Scene scene, LoadSceneMode mode)
        {
            SceneManager.sceneLoaded -= FinishMainMenuBootAfterSceneLoad;
            s_loadingMainMenu = false;

            MainMenu_V2[] menus = FindObjectsByType<MainMenu_V2>(FindObjectsInactive.Include);
            for (int i = 0; i < menus.Length; i++)
            {
                if (menus[i] != null)
                {
                    menus[i].ApplyReturnToMainMenuAfterSceneReload();
                    break;
                }
            }
        }

        private void RefreshVisibility()
        {
            if (!_hideWhenPauseUnavailable)
            {
                return;
            }

            ResolveWaveManagerIfNeeded();
            bool visible = _waveManager == null || CanPauseFromCurrentLoopState(_waveManager.State);
            if (gameObject.activeSelf != visible)
            {
                gameObject.SetActive(visible);
            }
        }

        private static bool CanPauseFromCurrentLoopState(WaveLoopState_V2 state)
        {
            return state == WaveLoopState_V2.Preparing ||
                   state == WaveLoopState_V2.InWave ||
                   state == WaveLoopState_V2.Shop ||
                   state == WaveLoopState_V2.LifeOver;
        }

        private void ResolveWaveManagerIfNeeded()
        {
            if (_waveManager == null)
            {
                _waveManager = FindAnyObjectByType<WaveManager_V2>();
            }
        }

        private void EnsureWorldSpaceHitTargetIfNeeded()
        {
            if (_uiButton != null)
            {
                return;
            }

            BoxCollider2D boxCollider = GetComponent<BoxCollider2D>();
            if (boxCollider == null)
            {
                SpriteRenderer spriteRenderer = GetComponent<SpriteRenderer>();
                if (spriteRenderer == null)
                {
                    spriteRenderer = GetComponentInChildren<SpriteRenderer>(true);
                }

                if (spriteRenderer == null)
                {
                    return;
                }

                boxCollider = gameObject.AddComponent<BoxCollider2D>();
            }

            FitBoxColliderToSprite(boxCollider);
        }

        private void FitBoxColliderToSprite(BoxCollider2D boxCollider)
        {
            SpriteRenderer spriteRenderer = GetComponent<SpriteRenderer>();
            if (spriteRenderer == null || spriteRenderer.sprite == null || boxCollider == null)
            {
                return;
            }

            Bounds bounds = spriteRenderer.bounds;
            Transform t = transform;
            Vector3 localCenter = t.InverseTransformPoint(bounds.center);
            Vector3 localSize = t.InverseTransformVector(bounds.size);
            boxCollider.offset = new Vector2(localCenter.x, localCenter.y);
            boxCollider.size = new Vector2(
                Mathf.Max(0.05f, Mathf.Abs(localSize.x)),
                Mathf.Max(0.05f, Mathf.Abs(localSize.y)));
        }
    }
}

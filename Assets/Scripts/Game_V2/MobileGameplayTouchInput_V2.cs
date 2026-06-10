using UnityEngine;
using UnityEngine.EventSystems;

namespace iStick2War_V2
{
    /*
     * MobileGameplayTouchInput_V2 — touch aim + hold-to-shoot for mobile gameplay.
     * Ignores touches over UI (weapon arrows, pause, menus). Sampled before HeroInput_V2.Tick.
     *
     * NAVIGATION: HeroInput_V2.cs, HeroView_V2.cs, PhoneWeaponArrowsCanvasLayout_V2.cs
     */
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-400)]
    public sealed class MobileGameplayTouchInput_V2 : MonoBehaviour
    {
        private static MobileGameplayTouchInput_V2 s_instance;

        private bool _combatTouchHeld;
        private bool _combatTouchBeganThisFrame;
        private bool _combatTouchEndedThisFrame;
        private bool _hasCombatAimScreenPoint;
        private Vector2 _combatAimScreenPoint;
        private bool _switchNextWeaponQueued;
        private bool _switchPreviousWeaponQueued;
        private bool _reloadQueued;

        public static MobileGameplayTouchInput_V2 Instance => s_instance;

        public bool CombatTouchHeld => _combatTouchHeld;
        public bool CombatTouchBeganThisFrame => _combatTouchBeganThisFrame;
        public bool CombatTouchEndedThisFrame => _combatTouchEndedThisFrame;

        public static void EnsureInstance()
        {
            if (s_instance != null)
            {
                return;
            }

            GameObject host = new GameObject(nameof(MobileGameplayTouchInput_V2));
            host.AddComponent<MobileGameplayTouchInput_V2>();
        }

        public bool TryGetCombatAimScreenPoint(out Vector2 screenPoint)
        {
            if (!_hasCombatAimScreenPoint)
            {
                screenPoint = default;
                return false;
            }

            screenPoint = _combatAimScreenPoint;
            return true;
        }

        public void QueueSwitchNextWeapon()
        {
            _switchNextWeaponQueued = true;
        }

        public void QueueSwitchPreviousWeapon()
        {
            _switchPreviousWeaponQueued = true;
        }

        public void QueueReload()
        {
            _reloadQueued = true;
        }

        public bool ConsumeSwitchNextWeapon()
        {
            if (!_switchNextWeaponQueued)
            {
                return false;
            }

            _switchNextWeaponQueued = false;
            return true;
        }

        public bool ConsumeSwitchPreviousWeapon()
        {
            if (!_switchPreviousWeaponQueued)
            {
                return false;
            }

            _switchPreviousWeaponQueued = false;
            return true;
        }

        public bool ConsumeReload()
        {
            if (!_reloadQueued)
            {
                return false;
            }

            _reloadQueued = false;
            return true;
        }

        private void Awake()
        {
            if (s_instance != null && s_instance != this)
            {
                Destroy(gameObject);
                return;
            }

            s_instance = this;
        }

        private void OnDestroy()
        {
            if (s_instance == this)
            {
                s_instance = null;
            }
        }

        private void Update()
        {
            if (!GamePlatform_V2.UseMobileGameplayRules)
            {
                ClearCombatTouchState();
                return;
            }

            RefreshCombatTouchState();
        }

        private void RefreshCombatTouchState()
        {
            bool wasHeld = _combatTouchHeld;
            _combatTouchBeganThisFrame = false;
            _combatTouchEndedThisFrame = false;
            _combatTouchHeld = false;

            if (Input.touchCount > 0)
            {
                for (int i = 0; i < Input.touchCount; i++)
                {
                    Touch touch = Input.GetTouch(i);
                    if (IsBlockedUiTouch(touch.fingerId, touch.position))
                    {
                        continue;
                    }

                    if (touch.phase == TouchPhase.Ended || touch.phase == TouchPhase.Canceled)
                    {
                        if (wasHeld)
                        {
                            _combatTouchEndedThisFrame = true;
                        }

                        continue;
                    }

                    _combatTouchHeld = true;
                    _hasCombatAimScreenPoint = true;
                    _combatAimScreenPoint = touch.position;

                    if (touch.phase == TouchPhase.Began)
                    {
                        _combatTouchBeganThisFrame = true;
                    }

                    break;
                }
            }
#if UNITY_EDITOR
            else if (Input.GetMouseButton(0) && !IsBlockedUiTouch(-1, Input.mousePosition))
            {
                _combatTouchHeld = true;
                _hasCombatAimScreenPoint = true;
                _combatAimScreenPoint = Input.mousePosition;
                if (Input.GetMouseButtonDown(0))
                {
                    _combatTouchBeganThisFrame = true;
                }
            }
            else if (wasHeld && Input.GetMouseButtonUp(0))
            {
                _combatTouchEndedThisFrame = true;
            }
#endif

            if (!wasHeld && _combatTouchHeld)
            {
                _combatTouchBeganThisFrame = true;
            }
        }

        private static bool IsBlockedUiTouch(int fingerId, Vector2 screenPosition)
        {
            if (EventSystem.current == null)
            {
                return false;
            }

            if (fingerId >= 0)
            {
                return EventSystem.current.IsPointerOverGameObject(fingerId);
            }

            return EventSystem.current.IsPointerOverGameObject();
        }

        private void ClearCombatTouchState()
        {
            _combatTouchHeld = false;
            _combatTouchBeganThisFrame = false;
            _combatTouchEndedThisFrame = false;
        }
    }
}

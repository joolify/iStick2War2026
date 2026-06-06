using System.Collections.Generic;
using iStick2War;

namespace iStick2War_V2
{
    internal static class HeroWeaponAmmoRules_V2
    {
        // Colt45: magazine rounds are consumed; reserve is unlimited for reload + UI (∞).
        public static bool HasInfiniteReserveAmmo(WeaponType weaponType) =>
            weaponType == WeaponType.Colt45;

        // Bazooka: single-round tube auto-chambers from reserve; no manual reload step.
        public static bool SkipsManualReload(WeaponType weaponType) =>
            weaponType == WeaponType.Bazooka;
    }

    /*
 * HeroWeaponRuntimeState_V2 (Per-weapon runtime ammo bag)
 *
 * PURPOSE:
 * Holds mutable magazine + reserve counts for one entry in HeroWeaponInventory_V2, bound to a
 * single HeroWeaponDefinition_V2 (immutable tuning from the ScriptableObject).
 *
 * ❌ MUST NOT:
 * - Perform raycasts, play audio, or touch HeroModel_V2 directly (HeroWeaponSystem_V2 syncs model from here).
 */
    internal sealed class HeroWeaponRuntimeState_V2
    {
        public HeroWeaponRuntimeState_V2(HeroWeaponDefinition_V2 definition)
        {
            Definition = definition;
            CurrentAmmo = definition.StartingMagazineAmmo;
            CurrentReserveAmmo = definition.StartingReserveAmmo;
        }

        public HeroWeaponDefinition_V2 Definition { get; }
        public int CurrentAmmo { get; set; }
        public int CurrentReserveAmmo { get; set; }
    }

    /*
 * HeroWeaponInventory_V2 (Loadout + active weapon index)
 *
 * PURPOSE:
 * In-memory weapon wheel for Hero_V2: ordered list of definitions + per-weapon ammo state,
 * active slot index, and helpers to add/switch/filter weapons.
 *
 * ---------------------------------------------------------
 * RESPONSIBILITIES:
 *
 * - AddIfMissing, SetActiveByType / BySlot, SwitchNext/Previous
 * - Queries (HasWeapon, ContainsWeaponType, TryGetWeaponState*)
 * - TryGetFirstWeaponIndexWithAmmo for fallback selection
 * - RemoveAllExcept for shop / wave rules that shrink the allowed set
 *
 * ---------------------------------------------------------
 * CORE PRINCIPLE:
 *
 * Inventory answers “which weapons exist, which is active, how much ammo each magazine holds”.
 * Combat execution and model syncing stay in HeroWeaponSystem_V2 / HeroController_V2.
 *
 * ---------------------------------------------------------
 * ❌ MUST NOT DO:
 *
 * - Read Unity input or drive animations
 * - Apply damage or run hit-scan
 *
 * ---------------------------------------------------------
 * DESIGN NOTE:
 *
 * SetActiveByType returns false when already on that weapon so callers (e.g. AutoHero) do not
 * reset reload cadence every frame while holding the same selection.
 */
    internal sealed class HeroWeaponInventory_V2
    {
        private readonly List<HeroWeaponRuntimeState_V2> _weapons = new List<HeroWeaponRuntimeState_V2>();
        private int _activeIndex = -1;

        public int Count => _weapons.Count;
        public int ActiveIndex => _activeIndex;
        public HeroWeaponRuntimeState_V2 ActiveWeapon =>
            (_activeIndex >= 0 && _activeIndex < _weapons.Count) ? _weapons[_activeIndex] : null;

        public void AddIfMissing(HeroWeaponDefinition_V2 definition)
        {
            if (definition == null)
            {
                return;
            }

            if (FindIndexByType(definition.WeaponType) >= 0)
            {
                return;
            }

            _weapons.Add(new HeroWeaponRuntimeState_V2(definition));
            if (_activeIndex < 0)
            {
                _activeIndex = 0;
            }
        }

        public bool SetActiveByType(WeaponType weaponType)
        {
            int idx = FindIndexByType(weaponType);
            if (idx < 0)
            {
                return false;
            }

            // Already equipped — callers must treat this as a no-op so we do not reset
            // reload state every frame (e.g. AutoHero_V2 re-selecting Bazooka while aiming).
            if (_activeIndex == idx)
            {
                return false;
            }

            _activeIndex = idx;
            return true;
        }

        public bool SetActiveBySlot(int zeroBasedSlot)
        {
            if (zeroBasedSlot < 0 || zeroBasedSlot >= _weapons.Count)
            {
                return false;
            }

            if (_activeIndex == zeroBasedSlot)
            {
                return false;
            }

            _activeIndex = zeroBasedSlot;
            return true;
        }

        public bool SwitchNext()
        {
            if (_weapons.Count <= 1)
            {
                return false;
            }

            _activeIndex = (_activeIndex + 1) % _weapons.Count;
            return true;
        }

        public bool SwitchPrevious()
        {
            if (_weapons.Count <= 1)
            {
                return false;
            }

            _activeIndex = (_activeIndex - 1 + _weapons.Count) % _weapons.Count;
            return true;
        }

        public bool HasWeapon(HeroWeaponDefinition_V2 definition)
        {
            if (definition == null)
            {
                return false;
            }

            return FindIndexByType(definition.WeaponType) >= 0;
        }

        public bool TryGetWeaponState(HeroWeaponDefinition_V2 definition, out HeroWeaponRuntimeState_V2 state)
        {
            state = null;
            if (definition == null)
            {
                return false;
            }

            int idx = FindIndexByType(definition.WeaponType);
            if (idx < 0)
            {
                return false;
            }

            state = _weapons[idx];
            return true;
        }

        public bool ContainsWeaponType(WeaponType weaponType)
        {
            return FindIndexByType(weaponType) >= 0;
        }

        public bool TryGetWeaponStateByType(WeaponType weaponType, out HeroWeaponRuntimeState_V2 state)
        {
            state = null;
            int idx = FindIndexByType(weaponType);
            if (idx < 0)
            {
                return false;
            }

            state = _weapons[idx];
            return true;
        }

        public int WeaponCount => _weapons.Count;

        public void ClearAll()
        {
            _weapons.Clear();
            _activeIndex = -1;
        }

        public HeroWeaponRuntimeState_V2 GetWeaponStateAtIndex(int zeroBasedIndex)
        {
            if (zeroBasedIndex < 0 || zeroBasedIndex >= _weapons.Count)
            {
                return null;
            }

            return _weapons[zeroBasedIndex];
        }

        // First inventory slot (loadout order) that still has magazine or reserve rounds.
        public bool TryGetFirstWeaponIndexWithAmmo(out int index)
        {
            for (int i = 0; i < _weapons.Count; i++)
            {
                HeroWeaponRuntimeState_V2 w = _weapons[i];
                if (w != null &&
                    w.Definition != null &&
                    (HeroWeaponAmmoRules_V2.HasInfiniteReserveAmmo(w.Definition.WeaponType) ||
                     w.CurrentAmmo > 0 ||
                     w.CurrentReserveAmmo > 0))
                {
                    index = i;
                    return true;
                }
            }

            index = -1;
            return false;
        }

        private int FindIndexByType(WeaponType weaponType)
        {
            for (int i = 0; i < _weapons.Count; i++)
            {
                if (_weapons[i].Definition != null && _weapons[i].Definition.WeaponType == weaponType)
                {
                    return i;
                }
            }

            return -1;
        }

        /// <summary>Removes any weapon whose type is not in <paramref name="allowed"/>; keeps order of remaining entries.</summary>
        public void RemoveAllExcept(HashSet<WeaponType> allowed)
        {
            if (allowed == null || allowed.Count == 0)
            {
                return;
            }

            WeaponType preferredActive = WeaponType.Colt45;
            if (_activeIndex >= 0 && _activeIndex < _weapons.Count && _weapons[_activeIndex].Definition != null)
            {
                preferredActive = _weapons[_activeIndex].Definition.WeaponType;
            }

            for (int i = _weapons.Count - 1; i >= 0; i--)
            {
                WeaponType t = _weapons[i].Definition != null ? _weapons[i].Definition.WeaponType : default;
                if (!allowed.Contains(t))
                {
                    _weapons.RemoveAt(i);
                }
            }

            if (_weapons.Count == 0)
            {
                _activeIndex = -1;
                return;
            }

            int keepIdx = FindIndexByType(preferredActive);
            if (keepIdx >= 0 && allowed.Contains(preferredActive))
            {
                _activeIndex = keepIdx;
                return;
            }

            _activeIndex = 0;
        }
    }
}

using iStick2War;
using System.Collections.Generic;

namespace iStick2War_V2
{
    internal sealed class HeroWeaponRuntimeState_V2
    {
        public HeroWeaponRuntimeState_V2(HeroWeaponDefinition_V2 definition)
        {
            Definition = definition;
            CurrentAmmo = definition.MaxAmmo;
            CurrentReserveAmmo = definition.StartingReserveAmmo;
        }

        public HeroWeaponDefinition_V2 Definition { get; }
        public int CurrentAmmo { get; set; }
        public int CurrentReserveAmmo { get; set; }
    }

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

            _activeIndex = idx;
            return true;
        }

        public bool SetActiveBySlot(int zeroBasedSlot)
        {
            if (zeroBasedSlot < 0 || zeroBasedSlot >= _weapons.Count)
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
    }
}

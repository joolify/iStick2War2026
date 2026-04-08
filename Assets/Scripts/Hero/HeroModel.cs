using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace iStick2War
{
    public class HeroModel : BaseModel
    {
        public event System.Action StartAimEvent;
        public event System.Action StopAimEvent;
        public event System.Action SwitchWeaponEvent;

        #region API

        public void StartAim()
        {
            if (StartAimEvent != null) StartAimEvent();
        }

        public void StopAim()
        {
            if (StopAimEvent != null) StopAimEvent();
        }


        public void SwitchWeapon(StickmanGunState gunState)
        {
            currentGunState = gunState;

            if (SwitchWeaponEvent != null) SwitchWeaponEvent();
        }
        #endregion

    }
}

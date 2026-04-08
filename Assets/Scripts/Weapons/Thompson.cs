using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace iStick2War
{
    public class Thompson : SMG
    {
        public override void Start()
        {
            base.Start();
            gunState = StickmanGunState.Thompson;
            weaponType = WeaponType.Thompson;
        }
    }
}

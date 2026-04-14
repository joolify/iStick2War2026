using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace iStick2War
{
    public class BaseModel : MonoBehaviour
    {
        #region Inspector
        [Header("Current State")]
        public StickmanBodyState currentBodyState = StickmanBodyState.Awake;
        public StickmanGunState currentGunState;
        public bool isDead = false;
        public bool hasPlayedDead = false;
        public bool hasExploded = false;
        public bool isCrouching = false;
        public bool isShooting = false;
        public bool isReloading = false;
        public bool isInAir = false;
        public bool isOnFire = false;
        public bool isMagicSpelled = false;
        public bool isElectrocuted = false;
        public bool shouldRun = false;
        public bool isFacingRight;

        public bool shootFlameThrower = false;
        public bool shootTesla = false;

        [Range(-1f, 1f)]
        public float currentSpeed;

        public float speed = 5;

        [Header("Balance")]
        public float shootInterval = 0.12f;
        #endregion

        float lastShootTime;
        public event System.Action StopShootEvent;
        public event System.Action StartShootEvent;
        public event System.Action CrouchIdleEvent;
        public event System.Action StandEvent;
        public event System.Action StartCrouchGrenadeEvent;
        public event System.Action StartCrouchShootEvent;
        public event System.Action StopCrouchShootEvent;
        public event System.Action GrenadeEvent;
        public event System.Action StartCrouchReloadEvent;
        public event System.Action StopCrouchReloadEvent;
        public event System.Action StartReloadEvent;
        public event System.Action StopReloadEvent;
        public event System.Action ShootHeadEvent;
        public event System.Action ShootArmsEvent;
        public event System.Action ShootTorsoEvent;
        public event System.Action ShootLegsEvent;
        public event System.Action DropHelmetEvent;
        public event System.Action DropWeaponEvent;
        public event System.Action JumpEvent;
        public event System.Action IdleEvent;
        public event System.Action DieEvent;

        public void StartShoot()
        {
            isCrouching = false;
            Debug.Log("StartShoot");
            //float currentTime = Time.time;

            //if (currentTime - lastShootTime > shootInterval)
            //{
            currentBodyState = StickmanBodyState.Shoot;
            //    lastShootTime = currentTime;
            if (StartShootEvent != null) StartShootEvent();
            //}
        }

        public void Grenade()
        {
            isCrouching = false;
            Debug.Log("StartGrenade");
            //float currentTime = Time.time;

            //if (currentTime - lastShootTime > shootInterval)
            //{
            currentBodyState = StickmanBodyState.Grenade;
            //    lastShootTime = currentTime;
            if (GrenadeEvent != null) GrenadeEvent();
            //}
        }

        public void StartReload()
        {
            isReloading = true;
            Debug.Log("Reloading");
            //float currentTime = Time.time;

            //if (currentTime - lastShootTime > shootInterval)
            //{
            currentBodyState = StickmanBodyState.Reload;
            //    lastShootTime = currentTime;
            if (StartReloadEvent != null) StartReloadEvent();
            //}
        }

        public void StopReload()
        {
            isReloading = false;
            Debug.Log("Reloading");
            //float currentTime = Time.time;

            //if (currentTime - lastShootTime > shootInterval)
            //{
            currentBodyState = StickmanBodyState.Idle;
            //    lastShootTime = currentTime;
            if (StopReloadEvent != null) StopReloadEvent();
            //}
        }

        public void StartCrouchReload()
        {
            isReloading = true;
            Debug.Log("Reloading");
            //float currentTime = Time.time;

            //if (currentTime - lastShootTime > shootInterval)
            //{
            currentBodyState = StickmanBodyState.CrouchReload;
            //    lastShootTime = currentTime;
            if (StartCrouchReloadEvent != null) StartCrouchReloadEvent();
            //}
        }

        public void StopCrouchReload()
        {
            isReloading = false;
            Debug.Log("Reloading");
            //float currentTime = Time.time;

            //if (currentTime - lastShootTime > shootInterval)
            //{
            currentBodyState = StickmanBodyState.CrouchIdle;
            //    lastShootTime = currentTime;
            if (StopCrouchReloadEvent != null) StopCrouchReloadEvent();
            //}
        }

        public void CrouchIdle()
        {
            isCrouching = true;
            Debug.Log("CrouchIdle");
            //float currentTime = Time.time;

            //if (currentTime - lastShootTime > shootInterval)
            //{
            currentBodyState = StickmanBodyState.CrouchIdle;
            //    lastShootTime = currentTime;
            if (CrouchIdleEvent != null) CrouchIdleEvent();
            //}
        }

        public void Stand()
        {
            isCrouching = false;
            Debug.Log("Stand");
            //float currentTime = Time.time;

            //if (currentTime - lastShootTime > shootInterval)
            //{
            currentBodyState = StickmanBodyState.Idle;
            //    lastShootTime = currentTime;
            if (StandEvent != null) StandEvent();
            //}
        }

        public void StartCrouchGrenade()
        {
            isCrouching = true;
            Debug.Log("StartCrouchGrenade");
            //float currentTime = Time.time;

            //if (currentTime - lastShootTime > shootInterval)
            //{
            currentBodyState = StickmanBodyState.CrouchGrenade;
            //    lastShootTime = currentTime;
            if (StartCrouchGrenadeEvent != null) StartCrouchGrenadeEvent();
            //}
        }

        public void StartCrouchShoot()
        {
            isCrouching = true;
            Debug.Log("StartCrouchShoot");
            //float currentTime = Time.time;

            //if (currentTime - lastShootTime > shootInterval)
            //{
            currentBodyState = StickmanBodyState.CrouchShoot;
            //    lastShootTime = currentTime;
            if (StartCrouchShootEvent != null) StartCrouchShootEvent();
            //}
        }

        public void StopCrouchShoot()
        {
            isCrouching = true;
            Debug.Log("StopCrouchShoot");
            //float currentTime = Time.time;

            //if (currentTime - lastShootTime > shootInterval)
            //{
            currentBodyState = StickmanBodyState.CrouchIdle;
            //    lastShootTime = currentTime;
            if (StopCrouchShootEvent != null) StopCrouchShootEvent();
            //}
        }

        public void Die()
        {
            Debug.Log("Die");
            isDead = true;
            currentBodyState = StickmanBodyState.Die;
            if (DieEvent != null) DieEvent();
        }

        public void StopShoot()
        {
            Debug.Log("StopShoot");
            currentBodyState = StickmanBodyState.Idle;
            if (StopShootEvent != null) StopShootEvent();
        }

        public void ShootHead()
        {
            currentBodyState = StickmanBodyState.Die;
            if (ShootHeadEvent != null) ShootHeadEvent();
        }
        public void ShootArms()
        {
            currentBodyState = StickmanBodyState.Die;
            if (ShootArmsEvent != null) ShootArmsEvent();
        }

        public void ShootTorso()
        {
            currentBodyState = StickmanBodyState.Die;
            if (ShootTorsoEvent != null) ShootTorsoEvent();
        }

        public void ShootLegs()
        {
            currentBodyState = StickmanBodyState.Die;
            if (ShootLegsEvent != null) ShootLegsEvent();
        }

        public void Idle()
        {
            currentBodyState = StickmanBodyState.Idle;
            if (IdleEvent != null) IdleEvent();
        }

        public IEnumerator TryJump()
        {
            if (currentBodyState == StickmanBodyState.Jump) yield break;   // Don't jump when already jumping.

            currentBodyState = StickmanBodyState.Jump;

            if (JumpEvent != null) JumpEvent();

            currentBodyState = StickmanBodyState.Idle;
        }

        public void DropHelmet()
        {
            if (DropHelmetEvent != null) DropHelmetEvent();
        }
        public void DropWeapon()
        {
            if (DropWeaponEvent != null) DropWeaponEvent();
        }

        public void TryMove(float speed)
        {
            currentSpeed = speed;

            if (currentBodyState != StickmanBodyState.Jump)
            {
                currentBodyState = (speed == 0) ? StickmanBodyState.Idle : StickmanBodyState.Run;
            }
        }

        public void TryCrouchMove(float speed)
        {
            currentSpeed = speed;

            if (currentBodyState != StickmanBodyState.Jump)
            {
                currentBodyState = (speed == 0) ? StickmanBodyState.CrouchIdle : StickmanBodyState.CrouchWalk;
            }
        }

        internal void ShootFeet()
        {
            //FIXME
        }
    }
}

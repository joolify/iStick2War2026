using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace iStick2War
{
    public class ParatrooperModel : BaseModel
    {
        public new event System.Action GrenadeEvent;
        public event System.Action ExplodeEvent;
        public event System.Action MagicSpellEvent;
        public event System.Action DeployEvent;
        public event System.Action GlideEvent;
        public event System.Action LandEvent;
        public event System.Action LandDieEvent;
        public event System.Action FireDieEvent;
        public event System.Action StartElectrocuteEvent;
        public event System.Action StopElectrocuteEvent;
        public event System.Action StartFlamethrowerEvent;
        public event System.Action StopFlamethrowerEvent;
        public GameObject fireParticleSystemTrigger;

        #region API

        public void ThrowGrenade()
        {
            if (GrenadeEvent != null) GrenadeEvent();
            currentBodyState = StickmanBodyState.Grenade;
        }

        public void StartFire()
        {
            if (isDead || isOnFire)
                return;
            Debug.Log("StartFlamethrower");
            isOnFire = true;
            if (StartFlamethrowerEvent != null) StartFlamethrowerEvent();
            currentBodyState = StickmanBodyState.OnFire;
        }
        public void StopFlamethrower()
        {
            if (isDead)
                return;
            Debug.Log("StopFlamethrower");
            if (StopFlamethrowerEvent != null) StopFlamethrowerEvent();
            currentBodyState = StickmanBodyState.Die;
        }

        public void StartElectrocute()
        {
            if (isDead || isElectrocuted)
                return;
            Debug.Log(DateTime.Now + " StartElectrocute");
            isElectrocuted = true;
            if (StartElectrocuteEvent != null) StartElectrocuteEvent();
            currentBodyState = StickmanBodyState.Electrocuted;
        }
        public void StopElectrocute()
        {
            if (isDead && !isElectrocuted)
                return;
            isElectrocuted = false;
            Debug.Log(DateTime.Now + " StopElectrocute");
            if (StopElectrocuteEvent != null) StopElectrocuteEvent();
            if(!isDead)
            {
                if(isInAir)
                {
                    currentBodyState = StickmanBodyState.Glide;
                }
                else
                {
                    currentBodyState = StickmanBodyState.Idle;
                }
            }
        }

        public void Deploy()
        {
            isInAir = true;
            if (DeployEvent != null) DeployEvent();
            currentBodyState = StickmanBodyState.Deploy;
        }
        public void Explode()
        {
            if (hasExploded) return;
            hasExploded = true;
            isDead = true;
            if (ExplodeEvent != null) ExplodeEvent();
            currentBodyState = StickmanBodyState.Die;
        }

        public void MagicSpelled()
        {
            isDead = true;
            isMagicSpelled = true;
            if (MagicSpellEvent != null) MagicSpellEvent();
            currentBodyState = StickmanBodyState.MagicSpell;
        }

        public void Glide()
        {
            isInAir = true;
            if (GlideEvent != null) GlideEvent();
            currentBodyState = StickmanBodyState.Glide;
        }

        public void Land()
        {
            isInAir = false;
            if (LandEvent != null) LandEvent();
            currentBodyState = StickmanBodyState.Land;
        }

        public void LandDie()
        {
            isInAir = false;
            if (LandDieEvent != null) LandDieEvent();
            currentBodyState = StickmanBodyState.LandDie;
        }

        public void FireDie()
        {
            isOnFire = true;
            if (FireDieEvent != null) FireDieEvent();
            currentBodyState = StickmanBodyState.LandDie;
        }

        #endregion
    }
}

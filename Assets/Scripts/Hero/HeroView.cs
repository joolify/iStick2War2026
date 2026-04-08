using Spine.Unity;
using System;
using UnityEngine;

namespace iStick2War
{
    public class HeroView : BaseView
    {
        #region Inspector
        [Header("Components")]
        public HeroModel model;

        [Header("Animations")]
        public AnimationReferenceAsset aimBazookaAnim;
        public AnimationReferenceAsset crouchGrenadeBazookaAnim;
        public AnimationReferenceAsset crouchIdleBazookaAnim;
        public AnimationReferenceAsset crouchReloadBazookaAnim;
        public AnimationReferenceAsset crouchShootBazookaAnim;
        public AnimationReferenceAsset crouchWalkBazookaAnim;
        public AnimationReferenceAsset grenadeBazookaAnim;
        public AnimationReferenceAsset idleBazookaAnim;
        public AnimationReferenceAsset jumpBazookaAnim;
        public AnimationReferenceAsset reloadBazookaAnim;
        public AnimationReferenceAsset runBazookaAnim;
        public AnimationReferenceAsset shootingBazookaAnim;

        [Space]
        public AnimationReferenceAsset aimCarbineAnim;
        public AnimationReferenceAsset crouchGrenadeCarbineAnim;
        public AnimationReferenceAsset crouchIdleCarbineAnim;
        public AnimationReferenceAsset crouchReloadCarbineAnim;
        public AnimationReferenceAsset crouchShootCarbineAnim;
        public AnimationReferenceAsset crouchWalkCarbineAnim;
        public AnimationReferenceAsset grenadeCarbineAnim;
        public AnimationReferenceAsset idleCarbineAnim;
        public AnimationReferenceAsset jumpCarbineAnim;
        public AnimationReferenceAsset reloadCarbineAnim;
        public AnimationReferenceAsset runCarbineAnim;
        public AnimationReferenceAsset shootingCarbineAnim;

        [Space]
        public AnimationReferenceAsset aimColt45Anim;
        public AnimationReferenceAsset crouchGrenadeColt45Anim;
        public AnimationReferenceAsset crouchIdleColt45Anim;
        public AnimationReferenceAsset crouchReloadColt45Anim;
        public AnimationReferenceAsset crouchShootColt45Anim;
        public AnimationReferenceAsset crouchWalkColt45Anim;
        public AnimationReferenceAsset grenadeColt45Anim;
        public AnimationReferenceAsset idleColt45Anim;
        public AnimationReferenceAsset jumpColt45Anim;
        public AnimationReferenceAsset reloadColt45Anim;
        public AnimationReferenceAsset runColt45Anim;
        public AnimationReferenceAsset shootingColt45Anim;

        [Space]
        public AnimationReferenceAsset aimFlamethrowerAnim;
        public AnimationReferenceAsset crouchGrenadeFlamethrowerAnim;
        public AnimationReferenceAsset crouchIdleFlamethrowerAnim;
        public AnimationReferenceAsset crouchShootFlamethrowerAnim;
        public AnimationReferenceAsset crouchWalkFlamethrowerAnim;
        public AnimationReferenceAsset grenadeFlamethrowerAnim;
        public AnimationReferenceAsset idleFlamethrowerAnim;
        public AnimationReferenceAsset jumpFlamethrowerAnim;
        public AnimationReferenceAsset runFlamethrowerAnim;
        public AnimationReferenceAsset shootingFlamethrowerAnim;

        [Space]
        public AnimationReferenceAsset aimIthacaAnim;
        public AnimationReferenceAsset crouchGrenadeIthacaAnim;
        public AnimationReferenceAsset crouchIdleIthacaAnim;
        public AnimationReferenceAsset crouchReloadIthacaAnim;
        public AnimationReferenceAsset crouchShootIthacaAnim;
        public AnimationReferenceAsset crouchWalkIthacaAnim;
        public AnimationReferenceAsset grenadeIthacaAnim;
        public AnimationReferenceAsset idleIthacaAnim;
        public AnimationReferenceAsset jumpIthacaAnim;
        public AnimationReferenceAsset reloadIthacaAnim;
        public AnimationReferenceAsset runIthacaAnim;
        public AnimationReferenceAsset shootingIthacaAnim;

        [Space]
        public AnimationReferenceAsset aimMagicStaffAnim;
        public AnimationReferenceAsset crouchGrenadeMagicStaffAnim;
        public AnimationReferenceAsset crouchIdleMagicStaffAnim;
        public AnimationReferenceAsset crouchShootMagicStaffAnim;
        public AnimationReferenceAsset crouchWalkMagicStaffAnim;
        public AnimationReferenceAsset grenadeMagicStaffAnim;
        public AnimationReferenceAsset idleMagicStaffAnim;
        public AnimationReferenceAsset jumpMagicStaffAnim;
        public AnimationReferenceAsset runMagicStaffAnim;
        public AnimationReferenceAsset shootingMagicStaffAnim;

        [Space]
        public AnimationReferenceAsset aimSterlingL2A3Anim;
        public AnimationReferenceAsset crouchGrenadeSterlingL2A3Anim;
        public AnimationReferenceAsset crouchIdleSterlingL2A3Anim;
        public AnimationReferenceAsset crouchReloadSterlingL2A3Anim;
        public AnimationReferenceAsset crouchShootSterlingL2A3Anim;
        public AnimationReferenceAsset crouchWalkSterlingL2A3Anim;
        public AnimationReferenceAsset grenadeSterlingL2A3Anim;
        public AnimationReferenceAsset idleSterlingL2A3Anim;
        public AnimationReferenceAsset jumpSterlingL2A3Anim;
        public AnimationReferenceAsset reloadSterlingL2A3Anim;
        public AnimationReferenceAsset runSterlingL2A3Anim;
        public AnimationReferenceAsset shootingSterlingL2A3Anim;

        [Space]
        public AnimationReferenceAsset aimTeslaAnim;
        public AnimationReferenceAsset crouchGrenadeTeslaAnim;
        public AnimationReferenceAsset crouchIdleTeslaAnim;
        public AnimationReferenceAsset crouchShootTeslaAnim;
        public AnimationReferenceAsset crouchWalkTeslaAnim;
        public AnimationReferenceAsset grenadeTeslaAnim;
        public AnimationReferenceAsset idleTeslaAnim;
        public AnimationReferenceAsset jumpTeslaAnim;
        public AnimationReferenceAsset runTeslaAnim;
        public AnimationReferenceAsset shootingTeslaAnim;

        [Space]
        public AnimationReferenceAsset aimThompsonAnim;
        public AnimationReferenceAsset crouchGrenadeThompsonAnim;
        public AnimationReferenceAsset crouchIdleThompsonAnim;
        public AnimationReferenceAsset crouchReloadThompsonAnim;
        public AnimationReferenceAsset crouchShootThompsonAnim;
        public AnimationReferenceAsset crouchWalkThompsonAnim;
        public AnimationReferenceAsset grenadeThompsonAnim;
        public AnimationReferenceAsset idleThompsonAnim;
        public AnimationReferenceAsset jumpThompsonAnim;
        public AnimationReferenceAsset reloadThompsonAnim;
        public AnimationReferenceAsset runThompsonAnim;
        public AnimationReferenceAsset shootingThompsonAnim;

        #endregion

        protected override void Start()
        {
            base.Start();
            model.StartShootEvent += StartPlayingShoot;
            model.StopShootEvent += StopPlayingShoot;

            model.CrouchIdleEvent += StartPlayingCrouchIdle;
            model.StandEvent += PlayIdle;
            model.GrenadeEvent += PlayGrenade;
            model.StartCrouchReloadEvent += PlayCrouchReload;
            model.StartReloadEvent += PlayReload;
            model.StartCrouchGrenadeEvent += StartPlayingCrouchGrenade;
            model.StartCrouchShootEvent += StartPlayingCrouchShoot;
            model.StopCrouchShootEvent += StopPlayingCrouchShoot;
            //model.StartAimEvent += StartPlayingAim;
            //model.StopAimEvent += StopPlayingAim;
            model.JumpEvent += PlayJump;
            //model.IdleEvent += PlayIdle;

            model.ShootHeadEvent += PlayShootHead;
            model.ShootArmsEvent += PlayShootArms;
            model.ShootTorsoEvent += PlayShootTorso;
            model.ShootLegsEvent += PlayShootLegs;
            model.DropHelmetEvent += DropHelmet;
            model.DropWeaponEvent += DropWeapon;

            SetHat();

            ResetMuzzle();

            CheckAnimationNames();
        }

        private void CheckAnimationNames()
        {
            if (!aimBazookaAnim.name.Equals("H_bazooka_aim")) Debug.LogError(nameof(aimBazookaAnim) + " has wrong animation");
            if (!crouchGrenadeBazookaAnim.name.Equals("H_bazooka_crouch_grenade")) Debug.LogError(nameof(crouchGrenadeBazookaAnim) + " has wrong animation");
            if (!crouchIdleBazookaAnim.name.Equals("H_bazooka_crouch_idle")) Debug.LogError(nameof(crouchIdleBazookaAnim) + " has wrong animation");
            if (!crouchReloadBazookaAnim.name.Equals("H_bazooka_crouch_reload")) Debug.LogError(nameof(crouchReloadBazookaAnim) + " has wrong animation");
            if (!crouchShootBazookaAnim.name.Equals("H_bazooka_crouch_shoot")) Debug.LogError(nameof(crouchShootBazookaAnim) + " has wrong animation");
            if (!crouchWalkBazookaAnim.name.Equals("H_bazooka_crouch_walk")) Debug.LogError(nameof(crouchWalkBazookaAnim) + " has wrong animation");
            if (!grenadeBazookaAnim.name.Equals("H_bazooka_grenade")) Debug.LogError(nameof(grenadeBazookaAnim) + " has wrong animation");
            if (!idleBazookaAnim.name.Equals("H_bazooka_idle")) Debug.LogError(nameof(idleBazookaAnim) + " has wrong animation");
            if (!jumpBazookaAnim.name.Equals("H_bazooka_jump")) Debug.LogError(nameof(jumpBazookaAnim) + " has wrong animation");
            if (!reloadBazookaAnim.name.Equals("H_bazooka_reload")) Debug.LogError(nameof(reloadBazookaAnim) + " has wrong animation");
            if (!runBazookaAnim.name.Equals("H_bazooka_run")) Debug.LogError(nameof(runBazookaAnim) + " has wrong animation");
            if (!shootingBazookaAnim.name.Equals("H_bazooka_shoot")) Debug.LogError(nameof(shootingBazookaAnim) + " has wrong animation");

            if (!aimCarbineAnim.name.Equals("H_carbine_aim")) Debug.LogError(nameof(aimCarbineAnim) + " has wrong animation");
            if (!crouchGrenadeCarbineAnim.name.Equals("H_carbine_crouch_grenade")) Debug.LogError(nameof(crouchGrenadeCarbineAnim) + " has wrong animation");
            if (!crouchIdleCarbineAnim.name.Equals("H_carbine_crouch_idle")) Debug.LogError(nameof(crouchIdleCarbineAnim) + " has wrong animation");
            if (!crouchReloadCarbineAnim.name.Equals("H_carbine_crouch_reload")) Debug.LogError(nameof(crouchReloadCarbineAnim) + " has wrong animation");
            if (!crouchShootCarbineAnim.name.Equals("H_carbine_crouch_shoot")) Debug.LogError(nameof(crouchShootCarbineAnim) + " has wrong animation");
            if (!crouchWalkCarbineAnim.name.Equals("H_carbine_crouch_walk")) Debug.LogError(nameof(crouchWalkCarbineAnim) + " has wrong animation");
            if (!grenadeCarbineAnim.name.Equals("H_carbine_grenade")) Debug.LogError(nameof(grenadeCarbineAnim) + " has wrong animation");
            if (!idleCarbineAnim.name.Equals("H_carbine_idle")) Debug.LogError(nameof(idleCarbineAnim) + " has wrong animation");
            if (!jumpCarbineAnim.name.Equals("H_carbine_jump")) Debug.LogError(nameof(jumpCarbineAnim) + " has wrong animation");
            if (!reloadCarbineAnim.name.Equals("H_carbine_reload")) Debug.LogError(nameof(reloadCarbineAnim) + " has wrong animation");
            if (!runCarbineAnim.name.Equals("H_carbine_run")) Debug.LogError(nameof(runCarbineAnim) + " has wrong animation");
            if (!shootingCarbineAnim.name.Equals("H_carbine_shoot")) Debug.LogError(nameof(shootingCarbineAnim) + " has wrong animation");

            if (!aimColt45Anim.name.Equals("H_colt_aim")) Debug.LogError(nameof(aimColt45Anim) + " has wrong animation");
            if (!crouchGrenadeColt45Anim.name.Equals("H_colt_crouch_grenade")) Debug.LogError(nameof(crouchGrenadeColt45Anim) + " has wrong animation");
            if (!crouchIdleColt45Anim.name.Equals("H_colt_crouch_idle")) Debug.LogError(nameof(crouchIdleColt45Anim) + " has wrong animation");
            if (!crouchReloadColt45Anim.name.Equals("H_colt_crouch_reload")) Debug.LogError(nameof(crouchReloadColt45Anim) + " has wrong animation");
            if (!crouchShootColt45Anim.name.Equals("H_colt_crouch_shoot")) Debug.LogError(nameof(crouchShootColt45Anim) + " has wrong animation");
            if (!crouchWalkColt45Anim.name.Equals("H_colt_crouch_walk")) Debug.LogError(nameof(crouchWalkColt45Anim) + " has wrong animation");
            if (!grenadeColt45Anim.name.Equals("H_colt_grenade")) Debug.LogError(nameof(grenadeColt45Anim) + " has wrong animation");
            if (!idleColt45Anim.name.Equals("H_colt_idle")) Debug.LogError(nameof(idleColt45Anim) + " has wrong animation");
            if (!jumpColt45Anim.name.Equals("H_colt_jump")) Debug.LogError(nameof(jumpColt45Anim) + " has wrong animation");
            if (!reloadColt45Anim.name.Equals("H_colt_reload")) Debug.LogError(nameof(reloadColt45Anim) + " has wrong animation");
            if (!runColt45Anim.name.Equals("H_colt_run")) Debug.LogError(nameof(runColt45Anim) + " has wrong animation");
            if (!shootingColt45Anim.name.Equals("H_colt_shoot")) Debug.LogError(nameof(shootingColt45Anim) + " has wrong animation");

            if (!aimFlamethrowerAnim.name.Equals("H_flamethrower_aim")) Debug.LogError(nameof(aimFlamethrowerAnim) + " has wrong animation");
            if (!crouchGrenadeFlamethrowerAnim.name.Equals("H_flamethrower_crouch_grenade")) Debug.LogError(nameof(crouchGrenadeFlamethrowerAnim) + " has wrong animation");
            if (!crouchIdleFlamethrowerAnim.name.Equals("H_flamethrower_crouch_idle")) Debug.LogError(nameof(crouchIdleFlamethrowerAnim) + " has wrong animation");
            if (!crouchShootFlamethrowerAnim.name.Equals("H_flamethrower_crouch_shoot")) Debug.LogError(nameof(crouchShootFlamethrowerAnim) + " has wrong animation");
            if (!crouchWalkFlamethrowerAnim.name.Equals("H_flamethrower_crouch_walk")) Debug.LogError(nameof(crouchWalkFlamethrowerAnim) + " has wrong animation");
            if (!grenadeFlamethrowerAnim.name.Equals("H_flamethrower_grenade")) Debug.LogError(nameof(grenadeFlamethrowerAnim) + " has wrong animation");
            if (!idleFlamethrowerAnim.name.Equals("H_flamethrower_idle")) Debug.LogError(nameof(idleFlamethrowerAnim) + " has wrong animation");
            if (!jumpFlamethrowerAnim.name.Equals("H_flamethrower_jump")) Debug.LogError(nameof(jumpFlamethrowerAnim) + " has wrong animation");
            if (!runFlamethrowerAnim.name.Equals("H_flamethrower_run")) Debug.LogError(nameof(runFlamethrowerAnim) + " has wrong animation");
            if (!shootingFlamethrowerAnim.name.Equals("H_flamethrower_shoot")) Debug.LogError(nameof(shootingFlamethrowerAnim) + " has wrong animation");

            if (!aimIthacaAnim.name.Equals("H_ithaca_aim")) Debug.LogError(nameof(aimIthacaAnim) + " has wrong animation");
            if (!crouchGrenadeIthacaAnim.name.Equals("H_ithaca_crouch_grenade")) Debug.LogError(nameof(crouchGrenadeIthacaAnim) + " has wrong animation");
            if (!crouchIdleIthacaAnim.name.Equals("H_ithaca_crouch_idle")) Debug.LogError(nameof(crouchIdleIthacaAnim) + " has wrong animation");
            if (!crouchReloadIthacaAnim.name.Equals("H_ithaca_crouch_reload")) Debug.LogError(nameof(crouchReloadIthacaAnim) + " has wrong animation");
            if (!crouchShootIthacaAnim.name.Equals("H_ithaca_crouch_shoot")) Debug.LogError(nameof(crouchShootIthacaAnim) + " has wrong animation");
            if (!crouchWalkIthacaAnim.name.Equals("H_ithaca_crouch_walk")) Debug.LogError(nameof(crouchWalkIthacaAnim) + " has wrong animation");
            if (!grenadeIthacaAnim.name.Equals("H_ithaca_grenade")) Debug.LogError(nameof(grenadeIthacaAnim) + " has wrong animation");
            if (!idleIthacaAnim.name.Equals("H_ithaca_idle")) Debug.LogError(nameof(idleIthacaAnim) + " has wrong animation");
            if (!jumpIthacaAnim.name.Equals("H_ithaca_jump")) Debug.LogError(nameof(jumpIthacaAnim) + " has wrong animation");
            if (!reloadIthacaAnim.name.Equals("H_ithaca_reload")) Debug.LogError(nameof(reloadIthacaAnim) + " has wrong animation");
            if (!runIthacaAnim.name.Equals("H_ithaca_run")) Debug.LogError(nameof(runIthacaAnim) + " has wrong animation");
            if (!shootingIthacaAnim.name.Equals("H_ithaca_shoot")) Debug.LogError(nameof(shootingIthacaAnim) + " has wrong animation");

            if (!aimMagicStaffAnim.name.Equals("H_magic_staff_aim")) Debug.LogError(nameof(aimMagicStaffAnim) + " has wrong animation");
            if (!crouchGrenadeMagicStaffAnim.name.Equals("H_magic_staff_crouch_grenade")) Debug.LogError(nameof(crouchGrenadeMagicStaffAnim) + " has wrong animation");
            if (!crouchIdleMagicStaffAnim.name.Equals("H_magic_staff_crouch_idle")) Debug.LogError(nameof(crouchIdleMagicStaffAnim) + " has wrong animation");
            if (!crouchShootMagicStaffAnim.name.Equals("H_magic_staff_crouch_shoot")) Debug.LogError(nameof(crouchShootMagicStaffAnim) + " has wrong animation");
            if (!crouchWalkMagicStaffAnim.name.Equals("H_magic_staff_crouch_walk")) Debug.LogError(nameof(crouchWalkMagicStaffAnim) + " has wrong animation");
            if (!grenadeMagicStaffAnim.name.Equals("H_magic_staff_grenade")) Debug.LogError(nameof(grenadeMagicStaffAnim) + " has wrong animation");
            if (!idleMagicStaffAnim.name.Equals("H_magic_staff_idle")) Debug.LogError(nameof(idleMagicStaffAnim) + " has wrong animation");
            if (!jumpMagicStaffAnim.name.Equals("H_magic_staff_jump")) Debug.LogError(nameof(jumpMagicStaffAnim) + " has wrong animation");
            if (!runMagicStaffAnim.name.Equals("H_magic_staff_run")) Debug.LogError(nameof(runMagicStaffAnim) + " has wrong animation");
            if (!shootingMagicStaffAnim.name.Equals("H_magic_staff_shoot")) Debug.LogError(nameof(shootingMagicStaffAnim) + " has wrong animation");

            if (!aimSterlingL2A3Anim.name.Equals("H_sterlingL2A3_aim")) Debug.LogError(nameof(aimSterlingL2A3Anim) + " has wrong animation");
            if (!crouchGrenadeSterlingL2A3Anim.name.Equals("H_sterlingL2A3_crouch_grenade")) Debug.LogError(nameof(crouchGrenadeSterlingL2A3Anim) + " has wrong animation");
            if (!crouchIdleSterlingL2A3Anim.name.Equals("H_sterlingL2A3_crouch_idle")) Debug.LogError(nameof(crouchIdleSterlingL2A3Anim) + " has wrong animation");
            if (!crouchReloadSterlingL2A3Anim.name.Equals("H_sterlingL2A3_crouch_reload")) Debug.LogError(nameof(crouchReloadSterlingL2A3Anim) + " has wrong animation");
            if (!crouchShootSterlingL2A3Anim.name.Equals("H_sterlingL2A3_crouch_shoot")) Debug.LogError(nameof(crouchShootSterlingL2A3Anim) + " has wrong animation");
            if (!crouchWalkSterlingL2A3Anim.name.Equals("H_sterlingL2A3_crouch_walk")) Debug.LogError(nameof(crouchWalkSterlingL2A3Anim) + " has wrong animation");
            if (!grenadeSterlingL2A3Anim.name.Equals("H_sterlingL2A3_grenade")) Debug.LogError(nameof(grenadeSterlingL2A3Anim) + " has wrong animation");
            if (!idleSterlingL2A3Anim.name.Equals("H_sterlingL2A3_idle")) Debug.LogError(nameof(idleSterlingL2A3Anim) + " has wrong animation");
            if (!jumpSterlingL2A3Anim.name.Equals("H_sterlingL2A3_jump")) Debug.LogError(nameof(jumpSterlingL2A3Anim) + " has wrong animation");
            if (!reloadSterlingL2A3Anim.name.Equals("H_sterlingL2A3_reload")) Debug.LogError(nameof(reloadSterlingL2A3Anim) + " has wrong animation");
            if (!runSterlingL2A3Anim.name.Equals("H_sterlingL2A3_run")) Debug.LogError(nameof(runSterlingL2A3Anim) + " has wrong animation");
            if (!shootingSterlingL2A3Anim.name.Equals("H_sterlingL2A3_shoot")) Debug.LogError(nameof(shootingSterlingL2A3Anim) + " has wrong animation");

            if (!aimTeslaAnim.name.Equals("H_tesla_aim")) Debug.LogError(nameof(aimTeslaAnim) + " has wrong animation");
            if (!crouchGrenadeTeslaAnim.name.Equals("H_tesla_crouch_grenade")) Debug.LogError(nameof(crouchGrenadeTeslaAnim) + " has wrong animation");
            if (!crouchIdleTeslaAnim.name.Equals("H_tesla_crouch_idle")) Debug.LogError(nameof(crouchIdleTeslaAnim) + " has wrong animation");
            if (!crouchShootTeslaAnim.name.Equals("H_tesla_crouch_shoot")) Debug.LogError(nameof(crouchShootTeslaAnim) + " has wrong animation");
            if (!crouchWalkTeslaAnim.name.Equals("H_tesla_crouch_walk")) Debug.LogError(nameof(crouchWalkTeslaAnim) + " has wrong animation");
            if (!grenadeTeslaAnim.name.Equals("H_tesla_grenade")) Debug.LogError(nameof(grenadeTeslaAnim) + " has wrong animation");
            if (!idleTeslaAnim.name.Equals("H_tesla_idle")) Debug.LogError(nameof(idleTeslaAnim) + " has wrong animation");
            if (!jumpTeslaAnim.name.Equals("H_tesla_jump")) Debug.LogError(nameof(jumpTeslaAnim) + " has wrong animation");
            if (!runTeslaAnim.name.Equals("H_tesla_run")) Debug.LogError(nameof(runTeslaAnim) + " has wrong animation");
            if (!shootingTeslaAnim.name.Equals("H_tesla_shoot")) Debug.LogError(nameof(shootingTeslaAnim) + " has wrong animation");

            if (!aimThompsonAnim.name.Equals("H_thompson_aim")) Debug.LogError(nameof(aimThompsonAnim) + " has wrong animation");
            if (!crouchGrenadeThompsonAnim.name.Equals("H_thompson_crouch_grenade")) Debug.LogError(nameof(crouchGrenadeThompsonAnim) + " has wrong animation");
            if (!crouchIdleThompsonAnim.name.Equals("H_thompson_crouch_idle")) Debug.LogError(nameof(crouchIdleThompsonAnim) + " has wrong animation");
            if (!crouchReloadThompsonAnim.name.Equals("H_thompson_crouch_reload")) Debug.LogError(nameof(crouchReloadThompsonAnim) + " has wrong animation");
            if (!crouchShootThompsonAnim.name.Equals("H_thompson_crouch_shoot")) Debug.LogError(nameof(crouchShootThompsonAnim) + " has wrong animation");
            if (!crouchWalkThompsonAnim.name.Equals("H_thompson_crouch_walk")) Debug.LogError(nameof(crouchWalkThompsonAnim) + " has wrong animation");
            if (!grenadeThompsonAnim.name.Equals("H_thompson_grenade")) Debug.LogError(nameof(grenadeThompsonAnim) + " has wrong animation");
            if (!idleThompsonAnim.name.Equals("H_thompson_idle")) Debug.LogError(nameof(idleThompsonAnim) + " has wrong animation");
            if (!jumpThompsonAnim.name.Equals("H_thompson_jump")) Debug.LogError(nameof(jumpThompsonAnim) + " has wrong animation");
            if (!reloadThompsonAnim.name.Equals("H_thompson_reload")) Debug.LogError(nameof(reloadThompsonAnim) + " has wrong animation");
            if (!runThompsonAnim.name.Equals("H_thompson_run")) Debug.LogError(nameof(runThompsonAnim) + " has wrong animation");
            if (!shootingThompsonAnim.name.Equals("H_thompson_shoot")) Debug.LogError(nameof(shootingThompsonAnim) + " has wrong animation");
        }

        private void SetHat()
        {
            skeleton.SetAttachment("helmet", "heroHelmet");
        }

        private void ResetMuzzle()
        {
            skeleton.SetAttachment("muzzle", null);
        }

        void Update()
        {
            if (skeletonAnimation == null) return;
            if (model == null) return;

            // Detect changes in model.state
            var currentBodyModelState = model.currentBodyState;

            if (previousBodyViewState != currentBodyModelState)
            {
                PlayNewStableAnimation();
            }

            previousBodyViewState = currentBodyModelState;

            var currentGunModelState = model.currentGunState;

            if (previousGunViewState != currentGunModelState)
            {
                Debug.Log(previousGunViewState);
                PlayNewStableAnimation();
            }

            previousGunViewState = currentGunModelState;
        }

        protected virtual void PlayNewStableAnimation()
        {
            var newBodyModelState = model.currentBodyState;
            var newGunModelState = model.currentGunState;
            bool loop;
            Spine.Animation nextAnimation;

            // Add conditionals to not interrupt transient animations.

            if (previousBodyViewState == StickmanBodyState.Jump && newBodyModelState != StickmanBodyState.Jump)
            {
                PlayFootstepSound();
            }

            if (newBodyModelState == StickmanBodyState.Jump)
            {
                jumpSource.Play();
                nextAnimation = GetJumpAnimation(newGunModelState);
                loop = false;
            }
            else
            {
                if (newBodyModelState == StickmanBodyState.Run)
                {
                    loop = true;
                    nextAnimation = GetRunAnimation(newGunModelState);
                }
                else if (newBodyModelState == StickmanBodyState.CrouchIdle)
                {
                    loop = true;
                    nextAnimation = GetCrouchIdlehAnimation(newGunModelState);
                }
                else if (newBodyModelState == StickmanBodyState.CrouchGrenade)
                {
                    loop = false;
                    nextAnimation = GetCrouchGrenadeAnimation(newGunModelState);
                }
                else if (newBodyModelState == StickmanBodyState.CrouchShoot)
                {
                    loop = true;
                    nextAnimation = GetCrouchShootAnimation(newGunModelState);
                }
                else if (newBodyModelState == StickmanBodyState.CrouchWalk)
                {
                    loop = true;
                    nextAnimation = GetCrouchWalkAnimation(newGunModelState);
                }
                else if (newBodyModelState == StickmanBodyState.Grenade)
                {
                    loop = true;
                    nextAnimation = GetGrenadeAnimation(newGunModelState);
                }
                else
                {
                    loop = true;
                    nextAnimation = GetIdleAnimation(newGunModelState);
                }
            }

            ResetMuzzle();
            skeletonAnimation.AnimationState.SetAnimation(0, nextAnimation, loop);
        }

        public Spine.Animation GetJumpAnimation(StickmanGunState gunState)
        {
            switch (gunState)
            {
                case StickmanGunState.Bazooka:
                    {
                        skeleton.SetAttachment("gunSlot", "bazooka");
                        skeleton.SetAttachment("backpack", null);
                        return jumpBazookaAnim;
                    }
                case StickmanGunState.Carbine:
                    {
                        skeleton.SetAttachment("gunSlot", "carbine");
                        skeleton.SetAttachment("backpack", null);
                        return jumpCarbineAnim;
                    }
                case StickmanGunState.Colt45:
                    {
                        skeleton.SetAttachment("gunSlot", "colt45");
                        skeleton.SetAttachment("backpack", null);
                        return jumpColt45Anim;
                    }
                case StickmanGunState.Flamethrower:
                    {
                        skeleton.SetAttachment("gunSlot", "flamethrower");
                        skeleton.SetAttachment("backpack", "flamepack");
                        return jumpFlamethrowerAnim;
                    }
                case StickmanGunState.Ithaca:
                    {
                        skeleton.SetAttachment("gunSlot", "ithaca 37");
                        skeleton.SetAttachment("backpack", null);
                        return jumpIthacaAnim;
                    }
                case StickmanGunState.MagicStaff:
                    {
                        skeleton.SetAttachment("gunSlot", "magic_staff");
                        skeleton.SetAttachment("backpack", null);
                        return jumpMagicStaffAnim;
                    }
                case StickmanGunState.SterlingL2A3:
                    {
                        skeleton.SetAttachment("gunSlot", "sterlingL2A3");
                        skeleton.SetAttachment("backpack", null);
                        return jumpSterlingL2A3Anim;
                    }
                case StickmanGunState.Tesla:
                    {
                        skeleton.SetAttachment("gunSlot", "tesla");
                        skeleton.SetAttachment("backpack", null);
                        return jumpTeslaAnim;
                    }
                case StickmanGunState.Thompson:
                default:
                    {
                        skeleton.SetAttachment("gunSlot", "thompson");
                        skeleton.SetAttachment("backpack", null);
                        return jumpThompsonAnim;
                    }
            }
        }

        Spine.Animation GetReloadAnimation(StickmanGunState gunState)
        {
            switch (gunState)
            {
                case StickmanGunState.Bazooka:
                    {
                        skeleton.SetAttachment("gunSlot", "bazooka");
                        skeleton.SetAttachment("backpack", null);
                        return reloadBazookaAnim;
                    }
                case StickmanGunState.Carbine:
                    {
                        skeleton.SetAttachment("gunSlot", "carbine");
                        skeleton.SetAttachment("backpack", null);
                        return reloadCarbineAnim;
                    }
                case StickmanGunState.Colt45:
                    {
                        skeleton.SetAttachment("gunSlot", "colt45");
                        skeleton.SetAttachment("backpack", null);
                        return reloadColt45Anim;
                    }
                case StickmanGunState.Flamethrower:
                    return null;
                case StickmanGunState.Ithaca:
                    {
                        skeleton.SetAttachment("gunSlot", "ithaca 37");
                        skeleton.SetAttachment("backpack", null);
                        return reloadIthacaAnim;
                    }
                case StickmanGunState.MagicStaff:
                    return null;
                case StickmanGunState.SterlingL2A3:
                    {
                        skeleton.SetAttachment("gunSlot", "sterlingL2A3");
                        skeleton.SetAttachment("backpack", null);
                        return reloadSterlingL2A3Anim;
                    }
                case StickmanGunState.Tesla:
                    return null;
                case StickmanGunState.Thompson:
                default:
                    {
                        skeleton.SetAttachment("gunSlot", "thompson");
                        skeleton.SetAttachment("backpack", null);
                        return reloadThompsonAnim;
                    }
            }
        }

        Spine.Animation GetCrouchReloadAnimation(StickmanGunState gunState)
        {
            switch (gunState)
            {
                case StickmanGunState.Bazooka:
                    {
                        skeleton.SetAttachment("gunSlot", "bazooka");
                        skeleton.SetAttachment("backpack", null);
                        return crouchReloadBazookaAnim;
                    }
                case StickmanGunState.Carbine:
                    {
                        skeleton.SetAttachment("gunSlot", "carbine");
                        skeleton.SetAttachment("backpack", null);
                        return crouchReloadCarbineAnim;
                    }
                case StickmanGunState.Colt45:
                    {
                        skeleton.SetAttachment("gunSlot", "colt45");
                        skeleton.SetAttachment("backpack", null);
                        return crouchReloadColt45Anim;
                    }
                case StickmanGunState.Flamethrower:
                    return null;
                case StickmanGunState.Ithaca:
                    {
                        skeleton.SetAttachment("gunSlot", "ithaca 37");
                        skeleton.SetAttachment("backpack", null);
                        return crouchReloadIthacaAnim;
                    }
                case StickmanGunState.MagicStaff:
                    return null;
                case StickmanGunState.SterlingL2A3:
                    {
                        skeleton.SetAttachment("gunSlot", "sterlingL2A3");
                        skeleton.SetAttachment("backpack", null);
                        return crouchReloadSterlingL2A3Anim;
                    }
                case StickmanGunState.Tesla:
                    return null;
                case StickmanGunState.Thompson:
                default:
                    {
                        skeleton.SetAttachment("gunSlot", "thompson");
                        skeleton.SetAttachment("backpack", null);
                        return crouchReloadThompsonAnim;
                    }
            }
        }

        Spine.Animation GetRunAnimation(StickmanGunState gunState)
        {
            switch (gunState)
            {
                case StickmanGunState.Bazooka:
                    {
                        skeleton.SetAttachment("gunSlot", "bazooka");
                        skeleton.SetAttachment("backpack", null);
                        return runBazookaAnim;
                    }
                case StickmanGunState.Carbine:
                    {
                        skeleton.SetAttachment("gunSlot", "carbine");
                        skeleton.SetAttachment("backpack", null);
                        return runCarbineAnim;
                    }
                case StickmanGunState.Colt45:
                    {
                        skeleton.SetAttachment("gunSlot", "colt45");
                        skeleton.SetAttachment("backpack", null);
                        return runColt45Anim;
                    }
                case StickmanGunState.Flamethrower:
                    {
                        skeleton.SetAttachment("gunSlot", "flamethrower");
                        skeleton.SetAttachment("backpack", "flamepack");
                        return runFlamethrowerAnim;
                    }
                case StickmanGunState.Ithaca:
                    {
                        skeleton.SetAttachment("gunSlot", "ithaca 37");
                        skeleton.SetAttachment("backpack", null);
                        return runIthacaAnim;
                    }
                case StickmanGunState.MagicStaff:
                    {
                        skeleton.SetAttachment("gunSlot", "magic_staff");
                        skeleton.SetAttachment("backpack", null);
                        return runMagicStaffAnim;
                    }
                case StickmanGunState.SterlingL2A3:
                    {
                        skeleton.SetAttachment("gunSlot", "sterlingL2A3");
                        skeleton.SetAttachment("backpack", null);
                        return runSterlingL2A3Anim;
                    }
                case StickmanGunState.Tesla:
                    {
                        skeleton.SetAttachment("gunSlot", "tesla");
                        skeleton.SetAttachment("backpack", null);
                        return runTeslaAnim;
                    }
                case StickmanGunState.Thompson:
                default:
                    {
                        skeleton.SetAttachment("gunSlot", "thompson");
                        skeleton.SetAttachment("backpack", null);
                        return runThompsonAnim;
                    }
            }
        }

        Spine.Animation GetGrenadeAnimation(StickmanGunState gunState)
        {
            switch (gunState)
            {
                case StickmanGunState.Bazooka:
                    {
                        skeleton.SetAttachment("gunSlot", "bazooka");
                        skeleton.SetAttachment("backpack", null);
                        return grenadeBazookaAnim;
                    }
                case StickmanGunState.Carbine:
                    {
                        skeleton.SetAttachment("gunSlot", "carbine");
                        skeleton.SetAttachment("backpack", null);
                        return grenadeCarbineAnim;
                    }
                case StickmanGunState.Colt45:
                    {
                        skeleton.SetAttachment("gunSlot", "colt45");
                        skeleton.SetAttachment("backpack", null);
                        return grenadeColt45Anim;
                    }
                case StickmanGunState.Flamethrower:
                    {
                        skeleton.SetAttachment("gunSlot", "flamethrower");
                        skeleton.SetAttachment("backpack", "flamepack");
                        return grenadeFlamethrowerAnim;
                    }
                case StickmanGunState.Ithaca:
                    {
                        skeleton.SetAttachment("gunSlot", "ithaca 37");
                        skeleton.SetAttachment("backpack", null);
                        return grenadeIthacaAnim;
                    }
                case StickmanGunState.MagicStaff:
                    {
                        skeleton.SetAttachment("gunSlot", "magic_staff");
                        skeleton.SetAttachment("backpack", null);
                        return grenadeMagicStaffAnim;
                    }
                case StickmanGunState.SterlingL2A3:
                    {
                        skeleton.SetAttachment("gunSlot", "sterlingL2A3");
                        skeleton.SetAttachment("backpack", null);
                        return grenadeSterlingL2A3Anim;
                    }
                case StickmanGunState.Tesla:
                    {
                        skeleton.SetAttachment("gunSlot", "tesla");
                        skeleton.SetAttachment("backpack", null);
                        return grenadeTeslaAnim;
                    }
                case StickmanGunState.Thompson:
                default:
                    {
                        skeleton.SetAttachment("gunSlot", "thompson");
                        skeleton.SetAttachment("backpack", null);
                        return grenadeThompsonAnim;
                    }
            }
        }

        Spine.Animation GetIdleAnimation(StickmanGunState gunState)
        {
            switch (gunState)
            {
                case StickmanGunState.Bazooka:
                    {
                        skeleton.SetAttachment("gunSlot", "bazooka");
                        skeleton.SetAttachment("backpack", null);
                        return idleBazookaAnim;
                    }
                case StickmanGunState.Carbine:
                    {
                        skeleton.SetAttachment("gunSlot", "carbine");
                        skeleton.SetAttachment("backpack", null);
                        return idleCarbineAnim;
                    }
                case StickmanGunState.Colt45:
                    {
                        skeleton.SetAttachment("gunSlot", "colt45");
                        skeleton.SetAttachment("backpack", null);
                        return idleColt45Anim;
                    }
                case StickmanGunState.Flamethrower:
                    {
                        skeleton.SetAttachment("gunSlot", "flamethrower");
                        skeleton.SetAttachment("backpack", "flamepack");
                        return idleFlamethrowerAnim;
                    }
                case StickmanGunState.Ithaca:
                    {
                        skeleton.SetAttachment("gunSlot", "ithaca 37");
                        skeleton.SetAttachment("backpack", null);
                        return idleIthacaAnim;
                    }
                case StickmanGunState.MagicStaff:
                    {
                        skeleton.SetAttachment("gunSlot", "magic_staff");
                        skeleton.SetAttachment("backpack", null);
                        return idleMagicStaffAnim;
                    }
                case StickmanGunState.SterlingL2A3:
                    {
                        skeleton.SetAttachment("gunSlot", "sterlingL2A3");
                        skeleton.SetAttachment("backpack", null);
                        return idleSterlingL2A3Anim;
                    }
                case StickmanGunState.Tesla:
                    {
                        skeleton.SetAttachment("gunSlot", "tesla");
                        skeleton.SetAttachment("backpack", null);
                        return idleTeslaAnim;
                    }
                case StickmanGunState.Thompson:
                default:
                    {
                        skeleton.SetAttachment("gunSlot", "thompson");
                        skeleton.SetAttachment("backpack", null);
                        return idleThompsonAnim;
                    }
            }
        }

        Spine.Animation GetShootAnimation(StickmanGunState gunState)
        {
            switch (gunState)
            {
                case StickmanGunState.Bazooka:
                    {
                        skeleton.SetAttachment("gunSlot", "bazooka");
                        skeleton.SetAttachment("backpack", null);
                        return shootingBazookaAnim;
                    }
                case StickmanGunState.Carbine:
                    {
                        skeleton.SetAttachment("gunSlot", "carbine");
                        skeleton.SetAttachment("backpack", null);
                        return shootingCarbineAnim;
                    }
                case StickmanGunState.Colt45:
                    {
                        skeleton.SetAttachment("gunSlot", "colt45");
                        skeleton.SetAttachment("backpack", null);
                        return shootingColt45Anim;
                    }
                case StickmanGunState.Flamethrower:
                    {
                        skeleton.SetAttachment("gunSlot", "flamethrower");
                        skeleton.SetAttachment("backpack", "flamepack");
                        return shootingFlamethrowerAnim;
                    }
                case StickmanGunState.Ithaca:
                    {
                        skeleton.SetAttachment("gunSlot", "ithaca 37");
                        skeleton.SetAttachment("backpack", null);
                        return shootingIthacaAnim;
                    }
                case StickmanGunState.MagicStaff:
                    {
                        skeleton.SetAttachment("gunSlot", "magic_staff");
                        skeleton.SetAttachment("backpack", null);
                        return shootingMagicStaffAnim;
                    }
                case StickmanGunState.SterlingL2A3:
                    {
                        skeleton.SetAttachment("gunSlot", "sterlingL2A3");
                        skeleton.SetAttachment("backpack", null);
                        return shootingSterlingL2A3Anim;
                    }
                case StickmanGunState.Tesla:
                    {
                        skeleton.SetAttachment("gunSlot", "tesla");
                        skeleton.SetAttachment("backpack", null);
                        return shootingTeslaAnim;
                    }
                case StickmanGunState.Thompson:
                default:
                    {
                        skeleton.SetAttachment("gunSlot", "thompson");
                        skeleton.SetAttachment("backpack", null);
                        return shootingThompsonAnim;
                    }
            }
        }

        Spine.Animation GetCrouchIdlehAnimation(StickmanGunState gunState)
        {
            switch (gunState)
            {
                case StickmanGunState.Bazooka:
                    {
                        skeleton.SetAttachment("gunSlot", "bazooka");
                        skeleton.SetAttachment("backpack", null);
                        return crouchIdleBazookaAnim;
                    }
                case StickmanGunState.Carbine:
                    {
                        skeleton.SetAttachment("gunSlot", "carbine");
                        skeleton.SetAttachment("backpack", null);
                        return crouchIdleCarbineAnim;
                    }
                case StickmanGunState.Colt45:
                    {
                        skeleton.SetAttachment("gunSlot", "colt45");
                        skeleton.SetAttachment("backpack", null);
                        return crouchIdleColt45Anim;
                    }
                case StickmanGunState.Flamethrower:
                    {
                        skeleton.SetAttachment("gunSlot", "flamethrower");
                        skeleton.SetAttachment("backpack", "flamepack");
                        return crouchIdleFlamethrowerAnim;
                    }
                case StickmanGunState.Ithaca:
                    {
                        skeleton.SetAttachment("gunSlot", "ithaca 37");
                        skeleton.SetAttachment("backpack", null);
                        return crouchIdleIthacaAnim;
                    }
                case StickmanGunState.MagicStaff:
                    {
                        skeleton.SetAttachment("gunSlot", "magic_staff");
                        skeleton.SetAttachment("backpack", null);
                        return crouchIdleMagicStaffAnim;
                    }
                case StickmanGunState.SterlingL2A3:
                    {
                        skeleton.SetAttachment("gunSlot", "sterlingL2A3");
                        skeleton.SetAttachment("backpack", null);
                        return crouchIdleSterlingL2A3Anim;
                    }
                case StickmanGunState.Tesla:
                    {
                        skeleton.SetAttachment("gunSlot", "tesla");
                        skeleton.SetAttachment("backpack", null);
                        return crouchIdleTeslaAnim;
                    }
                case StickmanGunState.Thompson:
                default:
                    {
                        skeleton.SetAttachment("gunSlot", "thompson");
                        skeleton.SetAttachment("backpack", null);
                        return crouchIdleThompsonAnim;
                    }
            }
        }

        Spine.Animation GetCrouchGrenadeAnimation(StickmanGunState gunState)
        {
            switch (gunState)
            {
                case StickmanGunState.Bazooka:
                    {
                        skeleton.SetAttachment("gunSlot", "bazooka");
                        skeleton.SetAttachment("backpack", null);
                        return crouchGrenadeBazookaAnim;
                    }
                case StickmanGunState.Carbine:
                    {
                        skeleton.SetAttachment("gunSlot", "carbine");
                        skeleton.SetAttachment("backpack", null);
                        return crouchGrenadeCarbineAnim;
                    }
                case StickmanGunState.Colt45:
                    {
                        skeleton.SetAttachment("gunSlot", "colt45");
                        skeleton.SetAttachment("backpack", null);
                        return crouchGrenadeColt45Anim;
                    }
                case StickmanGunState.Flamethrower:
                    {
                        skeleton.SetAttachment("gunSlot", "flamethrower");
                        skeleton.SetAttachment("backpack", "flamepack");
                        return crouchGrenadeFlamethrowerAnim;
                    }
                case StickmanGunState.Ithaca:
                    {
                        skeleton.SetAttachment("gunSlot", "ithaca 37");
                        skeleton.SetAttachment("backpack", null);
                        return crouchGrenadeIthacaAnim;
                    }
                case StickmanGunState.MagicStaff:
                    {
                        skeleton.SetAttachment("gunSlot", "magic_staff");
                        skeleton.SetAttachment("backpack", null);
                        return crouchGrenadeMagicStaffAnim;
                    }
                case StickmanGunState.SterlingL2A3:
                    {
                        skeleton.SetAttachment("gunSlot", "sterlingL2A3");
                        skeleton.SetAttachment("backpack", null);
                        return crouchGrenadeSterlingL2A3Anim;
                    }
                case StickmanGunState.Tesla:
                    {
                        skeleton.SetAttachment("gunSlot", "tesla");
                        skeleton.SetAttachment("backpack", null);
                        return crouchGrenadeTeslaAnim;
                    }
                case StickmanGunState.Thompson:
                default:
                    {
                        skeleton.SetAttachment("gunSlot", "thompson");
                        skeleton.SetAttachment("backpack", null);
                        return crouchGrenadeThompsonAnim;
                    }
            }
        }

        Spine.Animation GetCrouchShootAnimation(StickmanGunState gunState)
        {
            switch (gunState)
            {
                case StickmanGunState.Bazooka:
                    {
                        skeleton.SetAttachment("gunSlot", "bazooka");
                        skeleton.SetAttachment("backpack", null);
                        return crouchShootBazookaAnim;
                    }
                case StickmanGunState.Carbine:
                    {
                        skeleton.SetAttachment("gunSlot", "carbine");
                        skeleton.SetAttachment("backpack", null);
                        return crouchShootCarbineAnim;
                    }
                case StickmanGunState.Colt45:
                    {
                        skeleton.SetAttachment("gunSlot", "colt45");
                        skeleton.SetAttachment("backpack", null);
                        return crouchShootColt45Anim;
                    }
                case StickmanGunState.Flamethrower:
                    {
                        skeleton.SetAttachment("gunSlot", "flamethrower");
                        skeleton.SetAttachment("backpack", "flamepack");
                        return crouchShootFlamethrowerAnim;
                    }
                case StickmanGunState.Ithaca:
                    {
                        skeleton.SetAttachment("gunSlot", "ithaca 37");
                        skeleton.SetAttachment("backpack", null);
                        return crouchShootIthacaAnim;
                    }
                case StickmanGunState.MagicStaff:
                    {
                        skeleton.SetAttachment("gunSlot", "magic_staff");
                        skeleton.SetAttachment("backpack", null);
                        return crouchShootMagicStaffAnim;
                    }
                case StickmanGunState.SterlingL2A3:
                    {
                        skeleton.SetAttachment("gunSlot", "sterlingL2A3");
                        skeleton.SetAttachment("backpack", null);
                        return crouchShootSterlingL2A3Anim;
                    }
                case StickmanGunState.Tesla:
                    {
                        skeleton.SetAttachment("gunSlot", "tesla");
                        skeleton.SetAttachment("backpack", null);
                        return crouchShootTeslaAnim;
                    }
                case StickmanGunState.Thompson:
                default:
                    {
                        skeleton.SetAttachment("gunSlot", "thompson");
                        skeleton.SetAttachment("backpack", null);
                        return crouchShootThompsonAnim;
                    }
            }
        }

        Spine.Animation GetCrouchWalkAnimation(StickmanGunState gunState)
        {
            switch (gunState)
            {
                case StickmanGunState.Bazooka:
                    {
                        skeleton.SetAttachment("gunSlot", "bazooka");
                        skeleton.SetAttachment("backpack", null);
                        return crouchWalkBazookaAnim;
                    }
                case StickmanGunState.Carbine:
                    {
                        skeleton.SetAttachment("gunSlot", "carbine");
                        skeleton.SetAttachment("backpack", null);
                        return crouchWalkCarbineAnim;
                    }
                case StickmanGunState.Colt45:
                    {
                        skeleton.SetAttachment("gunSlot", "colt45");
                        skeleton.SetAttachment("backpack", null);
                        return crouchWalkColt45Anim;
                    }
                case StickmanGunState.Flamethrower:
                    {
                        skeleton.SetAttachment("gunSlot", "flamethrower");
                        skeleton.SetAttachment("backpack", "flamepack");
                        return crouchWalkFlamethrowerAnim;
                    }
                case StickmanGunState.Ithaca:
                    {
                        skeleton.SetAttachment("gunSlot", "ithaca 37");
                        skeleton.SetAttachment("backpack", null);
                    }
                    return crouchWalkIthacaAnim;
                case StickmanGunState.MagicStaff:
                    {
                        skeleton.SetAttachment("gunSlot", "magic_staff");
                        skeleton.SetAttachment("backpack", null);
                        return crouchWalkMagicStaffAnim;
                    }
                case StickmanGunState.SterlingL2A3:
                    {
                        skeleton.SetAttachment("gunSlot", "sterlingL2A3");
                        skeleton.SetAttachment("backpack", null);
                        return crouchWalkSterlingL2A3Anim;
                    }
                case StickmanGunState.Tesla:
                    {
                        skeleton.SetAttachment("gunSlot", "tesla");
                        skeleton.SetAttachment("backpack", null);
                        return crouchWalkTeslaAnim;
                    }
                case StickmanGunState.Thompson:
                default:
                    {
                        skeleton.SetAttachment("gunSlot", "thompson");
                        skeleton.SetAttachment("backpack", null);
                        return crouchWalkThompsonAnim;
                    }
            }
        }

        Spine.Animation GetAimAnimation(StickmanGunState gunState)
        {
            switch (gunState)
            {
                case StickmanGunState.Bazooka:
                    {
                        skeleton.SetAttachment("gunSlot", "bazooka");
                        skeleton.SetAttachment("backpack", null);
                        return aimBazookaAnim;
                    }
                case StickmanGunState.Carbine:
                    {
                        skeleton.SetAttachment("gunSlot", "carbine");
                        skeleton.SetAttachment("backpack", null);
                        return aimCarbineAnim;
                    }
                case StickmanGunState.Colt45:
                    {
                        skeleton.SetAttachment("gunSlot", "colt45");
                        skeleton.SetAttachment("backpack", null);
                        return aimColt45Anim;
                    }
                case StickmanGunState.Flamethrower:
                    {
                        skeleton.SetAttachment("gunSlot", "flamethrower");
                        skeleton.SetAttachment("backpack", "flamepack");
                        return aimFlamethrowerAnim;
                    }
                case StickmanGunState.Ithaca:
                    {
                        skeleton.SetAttachment("gunSlot", "ithaca 37");
                        skeleton.SetAttachment("backpack", null);
                        return aimIthacaAnim;
                    }
                case StickmanGunState.MagicStaff:
                    {
                        skeleton.SetAttachment("gunSlot", "magic_staff");
                        skeleton.SetAttachment("backpack", null);
                        return aimMagicStaffAnim;
                    }
                case StickmanGunState.SterlingL2A3:
                    {
                        skeleton.SetAttachment("gunSlot", "sterlingL2A3");
                        skeleton.SetAttachment("backpack", null);
                        return aimSterlingL2A3Anim;
                    }
                case StickmanGunState.Tesla:
                    {
                        skeleton.SetAttachment("gunSlot", "tesla");
                        skeleton.SetAttachment("backpack", null);
                        return aimTeslaAnim;
                    }
                case StickmanGunState.Thompson:
                default:
                    {
                        skeleton.SetAttachment("gunSlot", "thompson");
                        skeleton.SetAttachment("backpack", null);
                        return aimThompsonAnim;
                    }
            }
        }

        #region Transient Actions
        public void StartPlayingShoot()
        {
            Debug.Log("StartPlayingShoot");
            var newGunModelState = model.currentGunState;

            // Play the shoot animation on track 1.
            var track = skeletonAnimation.AnimationState.SetAnimation(1, GetShootAnimation(newGunModelState), true);
            //*track.AttachmentThreshold = 1f;
            track.MixDuration = 0f;

            //gunSource.pitch = GetRandomPitch(gunsoundPitchOffset);
            //gunSource.Play();
            ////gunParticles.randomSeed = (uint)Random.Range(0, 100);
            //gunParticles.Play();
            //FIXME sound
        }

        public void StopPlayingShoot()
        {
            skeletonAnimation.state.ClearTracks();
            var newGunModelState = model.currentGunState;
            //skeleton.SetToSetupPose();
            //var newGunModelState = model.currentGunState;
            var track = skeletonAnimation.AnimationState.SetAnimation(0, GetAimAnimation(newGunModelState), false);
            //*track.AttachmentThreshold = 1f;
            track.MixDuration = 0f;
            //var empty1 = skeletonAnimation.state.SetEmptyAnimation(1, 0.1f);
            //empty1.AttachmentThreshold = 1f;
        }

        public void StartPlayingCrouchIdle()
        {
            var empty1 = skeletonAnimation.state.SetEmptyAnimation(1, 0.5f);
            //*empty1.AttachmentThreshold = 1f;

            var newGunModelState = model.currentGunState;

            // Play the shoot animation on track 1.
            var track = skeletonAnimation.AnimationState.SetAnimation(0, GetCrouchIdlehAnimation(newGunModelState), false);
            //*track.AttachmentThreshold = 1f;
            track.MixDuration = 0f;

            gunSource.pitch = GetRandomPitch(gunsoundPitchOffset);
            gunSource.Play();
            //gunParticles.randomSeed = (uint)Random.Range(0, 100);
            gunParticles.Play();
        }

        public void StartPlayingCrouchGrenade()
        {
            var newGunModelState = model.currentGunState;

            // Play the shoot animation on track 1.
            var track = skeletonAnimation.AnimationState.SetAnimation(1, GetCrouchGrenadeAnimation(newGunModelState), false);
            //*track.AttachmentThreshold = 1f;
            track.MixDuration = 0f;
            track.Complete += PlayThrowCrouchGrenade_Complete;

            gunSource.pitch = GetRandomPitch(gunsoundPitchOffset);
            gunSource.Play();
            //gunParticles.randomSeed = (uint)Random.Range(0, 100);
            gunParticles.Play();
        }

        private void PlayThrowCrouchGrenade_Complete(Spine.TrackEntry trackEntry)
        {
            Debug.Log("PlayThrowCrouchGrenade_Complete()");

            model.currentBodyState = StickmanBodyState.CrouchIdle;

            StartPlayingCrouchIdle();
        }

        private void PlayThrowGrenade_Complete(Spine.TrackEntry trackEntry)
        {
            Debug.Log("PlayThrowGrenade_Complete()");

            model.currentBodyState = StickmanBodyState.Idle;

            PlayIdle();
        }

        public void StartPlayingCrouchShoot()
        {
            var newGunModelState = model.currentGunState;

            // Play the shoot animation on track 1.
            var track = skeletonAnimation.AnimationState.SetAnimation(1, GetCrouchShootAnimation(newGunModelState), true);
            //*track.AttachmentThreshold = 1f;
            track.MixDuration = 0f;

            gunSource.pitch = GetRandomPitch(gunsoundPitchOffset);
            gunSource.Play();
            //gunParticles.randomSeed = (uint)Random.Range(0, 100);
            gunParticles.Play();
        }

        public void StopPlayingCrouchShoot()
        {
            var empty1 = skeletonAnimation.state.SetEmptyAnimation(1, 0.5f);
            //*empty1.AttachmentThreshold = 1f;
        }

        public void StartPlayingCrouchWalk()
        {
            var newGunModelState = model.currentGunState;

            // Play the shoot animation on track 1.
            var track = skeletonAnimation.AnimationState.SetAnimation(1, GetCrouchWalkAnimation(newGunModelState), true);
            //*track.AttachmentThreshold = 1f;
            track.MixDuration = 0f;

            gunSource.pitch = GetRandomPitch(gunsoundPitchOffset);
            gunSource.Play();
            //gunParticles.randomSeed = (uint)Random.Range(0, 100);
            gunParticles.Play();
        }



        public void PlayJump()
        {
            var newGunModelState = model.currentGunState;

            // Play the aim animation on track 2 to aim at the mouse target.
            var track = skeletonAnimation.AnimationState.SetAnimation(0, GetJumpAnimation(newGunModelState), false);

            //aimTrack.AttachmentThreshold = 1f;
            //aimTrack.MixDuration = 0f;
        }

        public void PlayCrouchReload()
        {
            var newGunModelState = model.currentGunState;

            // Play the aim animation on track 2 to aim at the mouse target.
            var track = skeletonAnimation.AnimationState.SetAnimation(1, GetCrouchReloadAnimation(newGunModelState), false);
            //*track.AttachmentThreshold = 1f;
            track.MixDuration = 0f;

            skeletonAnimation.state.AddEmptyAnimation(1, 0.5f, 0f);

            //aimTrack.AttachmentThreshold = 1f;
            //aimTrack.MixDuration = 0f;
        }

        public void PlayReload()
        {
            var newGunModelState = model.currentGunState;

            // Play the aim animation on track 2 to aim at the mouse target.
            var track = skeletonAnimation.AnimationState.SetAnimation(1, GetReloadAnimation(newGunModelState), false);
            //*track.AttachmentThreshold = 1f;
            track.MixDuration = 0f;

            //aimTrack.AttachmentThreshold = 1f;
            //aimTrack.MixDuration = 0f;
        }



        public void PlayIdle()
        {
            var empty1 = skeletonAnimation.state.SetEmptyAnimation(1, 0.5f);
            //*empty1.AttachmentThreshold = 1f;

            Debug.Log("PlayIdle");
            var newGunModelState = model.currentGunState;

            // Play the aim animation on track 2 to aim at the mouse target.
            var track = skeletonAnimation.AnimationState.SetAnimation(0, GetIdleAnimation(newGunModelState), true);
            //aimTrack.AttachmentThreshold = 1f;
            //aimTrack.MixDuration = 0f;
        }

        public void PlayGrenade()
        {
            var empty1 = skeletonAnimation.state.SetEmptyAnimation(1, 0.5f);
            //*empty1.AttachmentThreshold = 1f;

            var newGunModelState = model.currentGunState;

            // Play the shoot animation on track 1.
            var track = skeletonAnimation.AnimationState.SetAnimation(1, GetGrenadeAnimation(newGunModelState), false);
            //*track.AttachmentThreshold = 1f;
            track.MixDuration = 0f;
            track.Complete += PlayThrowGrenade_Complete;

            gunSource.pitch = GetRandomPitch(gunsoundPitchOffset);
            gunSource.Play();
            //gunParticles.randomSeed = (uint)Random.Range(0, 100);
            gunParticles.Play();
        }
        #endregion
    }
}

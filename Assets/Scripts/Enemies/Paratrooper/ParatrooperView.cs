using Spine.Unity;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace iStick2War
{
    public class ParatrooperView : BaseView
    {
        public static int ParatroopSortingCount = 1;

        public ParatrooperView()
        {
            ParatroopSortingCount++;
        }

        #region Inspector
        [Header("Components")]
        public ParatrooperModel model;
        public GameObject fireParticleSystemTrigger;

        [Header("Animations")]
        public AnimationReferenceAsset deployAnim;
        public AnimationReferenceAsset electrocutedAnim;
        public AnimationReferenceAsset fallDownAnim;
        public AnimationReferenceAsset fireFallForwardDeathAnim;
        public AnimationReferenceAsset fireRunAnim;
        public AnimationReferenceAsset fireRunDeathAnim;

        [Space]
        public AnimationReferenceAsset glideAnim;
        public AnimationReferenceAsset glideDeathAnim;
        public AnimationReferenceAsset glideElectrocuted;
        public AnimationReferenceAsset glideFire;
        public AnimationReferenceAsset glideFireDeath;
        public AnimationReferenceAsset glideMagicHitHead;
        public AnimationReferenceAsset glideMagicHitLegs;
        public AnimationReferenceAsset glideMagicHitTorso;
        public AnimationReferenceAsset glideParachuteGone;
        public AnimationReferenceAsset grenadeAnim;
        public AnimationReferenceAsset landAnim;
        public AnimationReferenceAsset landFallDownBackAnim;
        public AnimationReferenceAsset landFallDownBack2Anim;
        public AnimationReferenceAsset landFallDownBack3Anim;

        [Space]
        public AnimationReferenceAsset aimMP40Anim;
        public AnimationReferenceAsset idleMP40Anim;
        public AnimationReferenceAsset jetpackFlyMP40Anim;
        public AnimationReferenceAsset jetpackHoverMP40Anim;
        public AnimationReferenceAsset jumpMP40Anim;
        public AnimationReferenceAsset magicHitHeadMP40Anim;
        public AnimationReferenceAsset magicHitLegsMP40Anim;
        public AnimationReferenceAsset magicHitTorsoMP40Anim;
        public AnimationReferenceAsset reloadMP40Anim;
        public AnimationReferenceAsset runMP40Anim;
        public AnimationReferenceAsset shootingMP40Anim;

        public ParticleSystem flameThrower;
        //public LightningBolt2D lightningBolt2D; FIXME

        #endregion

        protected override void Start()
        {
            base.Start();
            model.GrenadeEvent += PlayGrenade;
            model.ExplodeEvent += Explode;

            model.ShootHeadEvent += PlayShootHead;
            model.ShootArmsEvent += PlayShootArms;
            model.ShootTorsoEvent += PlayShootTorso;
            model.ShootLegsEvent += PlayShootLegs;
            model.DropHelmetEvent += DropHelmet;
            model.DropWeaponEvent += DropWeapon;

            model.StopElectrocuteEvent += StopElectrocute;

            model.StartFlamethrowerEvent += PlayOnFire;

            //TODO Add randomness and check which direction paratrooper was killed at
            model.DieEvent += PlayDie;

            model.LandDieEvent += PlayLandDie;

            model.FireDieEvent += PlayOnFire;

            model.MagicSpellEvent += PlayMagicSpelled;

            SetHat(true);

            CheckAnimationNames();

            SetWeapon();
        }

        private void SetWeapon()
        {
            skeleton.SetAttachment("gunSlot", "mp40");
        }

        private void CheckAnimationNames()
        {
            if (!deployAnim.name.Equals("E_deploy")) Debug.LogError(nameof(deployAnim) + " has wrong animation");
            if (!electrocutedAnim.name.Equals("E_electrocuted")) Debug.LogError(nameof(electrocutedAnim) + " has wrong animation");
            if (!fallDownAnim.name.Equals("E_fall_down")) Debug.LogError(nameof(fallDownAnim) + " has wrong animation");
            if (!fireFallForwardDeathAnim.name.Equals("E_fire_fall_forward_death")) Debug.LogError(nameof(fireFallForwardDeathAnim) + " has wrong animation");
            if (!fireRunAnim.name.Equals("E_fire_run")) Debug.LogError(nameof(fireRunAnim) + " has wrong animation");
            if (!fireRunDeathAnim.name.Equals("E_fire_run_death")) Debug.LogError(nameof(fireRunDeathAnim) + " has wrong animation");

            if (!glideAnim.name.Equals("E_glide")) Debug.LogError(nameof(glideAnim) + " has wrong animation");
            if (!glideDeathAnim.name.Equals("E_glide_death")) Debug.LogError(nameof(glideDeathAnim) + " has wrong animation");
            if (!glideElectrocuted.name.Equals("E_glide_electrocuted")) Debug.LogError(nameof(glideElectrocuted) + " has wrong animation");
            if (!glideFire.name.Equals("E_glide_fire")) Debug.LogError(nameof(glideFire) + " has wrong animation");
            if (!glideFireDeath.name.Equals("E_glide_fire_death")) Debug.LogError(nameof(glideFireDeath) + " has wrong animation");
            if (!glideMagicHitHead.name.Equals("E_glide_magic_hit_head")) Debug.LogError(nameof(glideMagicHitHead) + " has wrong animation");
            if (!glideMagicHitLegs.name.Equals("E_glide_magic_hit_legs")) Debug.LogError(nameof(glideMagicHitLegs) + " has wrong animation");
            if (!glideMagicHitTorso.name.Equals("E_glide_magic_hit_torso")) Debug.LogError(nameof(glideMagicHitTorso) + " has wrong animation");
            if (!glideParachuteGone.name.Equals("E_glide_parachute_gone")) Debug.LogError(nameof(glideParachuteGone) + " has wrong animation");
            if (!grenadeAnim.name.Equals("E_grenade")) Debug.LogError(nameof(grenadeAnim) + " has wrong animation");
            if (!landAnim.name.Equals("E_land")) Debug.LogError(nameof(landAnim) + " has wrong animation");

            if (!aimMP40Anim.name.Equals("E_mp40_aim")) Debug.LogError(nameof(aimMP40Anim) + " has wrong animation");
            if (!idleMP40Anim.name.Equals("E_mp40_idle")) Debug.LogError(nameof(idleMP40Anim) + " has wrong animation");
            if (!jetpackFlyMP40Anim.name.Equals("E_mp40_jetpack_fly")) Debug.LogError(nameof(jetpackFlyMP40Anim) + " has wrong animation");
            if (!jetpackHoverMP40Anim.name.Equals("E_mp40_jetpack_hover")) Debug.LogError(nameof(jetpackHoverMP40Anim) + " has wrong animation");
            if (!jumpMP40Anim.name.Equals("E_mp40_jump")) Debug.LogError(nameof(jumpMP40Anim) + " has wrong animation");
            if (!magicHitHeadMP40Anim.name.Equals("E_mp40_magic_hit_head")) Debug.LogError(nameof(magicHitHeadMP40Anim) + " has wrong animation");
            if (!magicHitLegsMP40Anim.name.Equals("E_mp40_magic_hit_legs")) Debug.LogError(nameof(magicHitLegsMP40Anim) + " has wrong animation");
            if (!magicHitTorsoMP40Anim.name.Equals("E_mp40_magic_hit_torso")) Debug.LogError(nameof(magicHitTorsoMP40Anim) + " has wrong animation");
            if (!runMP40Anim.name.Equals("E_mp40_run")) Debug.LogError(nameof(runMP40Anim) + " has wrong animation");
            if (!shootingMP40Anim.name.Equals("E_mp40_shoot")) Debug.LogError(nameof(shootingMP40Anim) + " has wrong animation");
        }

        private void SetHat(bool enabled)
        {
            if (enabled)
            {
                skeletonAnimation.skeleton.SetAttachment("helmet", "naziHelmet");
            }
            else
            {
                skeletonAnimation.skeleton.SetAttachment("helmet", null);
            }
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
                PlayNewStableAnimation();
            }

            previousGunViewState = currentGunModelState;

            if (model.shouldRun)
            {
                HideParachute();
                var isFacingRight = transform.parent.localScale.x > 0;
                transform.Translate(isFacingRight ? -0.2f : 0.2f, 0f, 0f);
            }
        }

        protected virtual void PlayNewStableAnimation()
        {
            Debug.Log("ParatrooperView: " + model.currentBodyState);
            var newBodyModelState = model.currentBodyState;
            var newGunModelState = model.currentGunState;
            Spine.Animation nextAnimation;
            int trackIndex = 0;
            Spine.AnimationState.TrackEntryDelegate @complete = null;

            // Add conditionals to not interrupt transient animations.

            if (previousBodyViewState == StickmanBodyState.Jump && newBodyModelState != StickmanBodyState.Jump)
            {
                PlayFootstepSound();
            }

            bool loop;
            switch (newBodyModelState)
            {
                case StickmanBodyState.Deploy:
                    nextAnimation = deployAnim;
                    loop = false;
                    trackIndex = 1;
                    complete = PlayDeploy_Complete;
                    break;
                case StickmanBodyState.OnFire:
                case StickmanBodyState.Die:
                case StickmanBodyState.LandDie:
                case StickmanBodyState.MagicSpell:
                case StickmanBodyState.FireDie:
                    return;
                case StickmanBodyState.Electrocuted:
                    if (model.isInAir)
                    {
                        nextAnimation = glideElectrocuted;
                        loop = true;
                        trackIndex = 0;
                    }
                    else
                    {
                        nextAnimation = electrocutedAnim;
                        loop = true;
                        trackIndex = 0;
                    }

                    StartCoroutine("StartElectrocute");

                    SetHat(false);
                    break;
                case StickmanBodyState.Glide:
                    nextAnimation = glideAnim;
                    loop = false;
                    trackIndex = 0;
                    break;
                case StickmanBodyState.GlideDie:
                    nextAnimation = glideDeathAnim;
                    loop = false;
                    trackIndex = 0;
                    break;
                case StickmanBodyState.Grenade:
                    nextAnimation = grenadeAnim;
                    loop = true;
                    trackIndex = 0;
                    break;
                case StickmanBodyState.Land:
                    nextAnimation = landAnim;
                    loop = false;
                    trackIndex = 1;
                    complete = PlayLand_Complete;
                    break;
                default:
                    nextAnimation = idleMP40Anim;
                    loop = true;
                    break;
            }

            var track = skeletonAnimation.AnimationState.SetAnimation(trackIndex, nextAnimation, loop);
            track.Complete += @complete;
        }

        public override void Explode()
        {
            base.Explode();

            var isFacingRight = transform.parent.localScale.x > 0;

            var mp40 = Instantiate(weapon, skeletonAnimation.transform.position, UnityEngine.Quaternion.Euler(0f, 0f, 0f));
            var mp40Sprite = mp40.GetComponent<SpriteRenderer>();
            mp40Sprite.flipX = !isFacingRight;

            var mp40Rb = mp40.GetComponent<Rigidbody2D>();
            mp40Rb.AddForce(GetRandomDirection() * GetRandomForce());
            mp40Rb.AddTorque(GetRandomTorque(), ForceMode2D.Force);

            var heads = new List<GameObject> { head1, head2 };
            var randHead = Random.Range(0, heads.Count);
            Debug.Log("Randhead: " + randHead);
            var head = Instantiate(heads[randHead], skeletonAnimation.transform.position, UnityEngine.Quaternion.Euler(0f, 0f, 0f));
            var headSprite = head.GetComponent<SpriteRenderer>();
            headSprite.flipX = !isFacingRight;
            var headRb = head.GetComponent<Rigidbody2D>();
            headRb.AddForce(GetRandomDirection() * GetRandomForce());
            headRb.AddTorque(GetRandomTorque(), ForceMode2D.Force);

            if (randHead == 0)
            {
                var naziHelmet = Instantiate(helmet, skeletonAnimation.transform.position, UnityEngine.Quaternion.Euler(0f, 0f, 0f));
                var naziHelmetSprite = helmet.GetComponent<SpriteRenderer>();
                naziHelmetSprite.flipX = !isFacingRight;

                var naziHelmetRb = naziHelmet.GetComponent<Rigidbody2D>();
                naziHelmetRb.AddForce(GetRandomDirection() * GetRandomForce());
                naziHelmetRb.AddTorque(GetRandomTorque(), ForceMode2D.Force);
            }
        }


        #region Transient Actions

        private void PlayDeploy_Complete(Spine.TrackEntry trackEntry)
        {
            Debug.Log("PlayDeploy_Complete()");
            PlayGlide();
        }

        IEnumerator StartElectrocute()
        {
            //if (!model.isDead)
            //{
            //    Debug.Log("ParatrooperView.StartElectrocute()");
            //    lightningBolt2D.gameObject.SetActive(true);
            //    var timer = 0.0;
            //    var randomTimeElectrocuted = Random.Range(0.5f, 1f);
            //    while (model.isElectrocuted)
            //    {
            //        timer += Time.deltaTime;
            //        if (timer > randomTimeElectrocuted)
            //        {
            //            model.isDead = true;
            //            model.isElectrocuted = false;
            //            DisableColliders();
            //            StopElectrocute();
            //            if (!model.isInAir)
            //            {
            //                DropWeapon();
            //            }
            //        }
            //        lightningBolt2D.startPoint.x = skeletonAnimation.transform.position.x;
            //        lightningBolt2D.startPoint.y = skeletonAnimation.transform.position.y;
            //        lightningBolt2D.endPoint.x = skeletonAnimation.transform.position.x;
            //        lightningBolt2D.endPoint.y = skeletonAnimation.transform.position.y + 5;
            //        yield return null;
            //    }
            //}
            //FIXME
            yield return null;
        }

        private void DisableColliders()
        {
            //var fireParticleSystemTrigger = model.fireParticleSystemTrigger.GetComponent<Collider2D>(); ;
            //if (fireFallForwardDeathAnim != null) fireParticleSystemTrigger.enabled = false;
            //FIXME
            foreach (Transform child in transform)
            {
                if (child.GetComponent<StickmanBodypart>())
                {
                    child.GetComponent<Collider2D>().enabled = false;
                }
            }
        }

        private void StopElectrocute()
        {
            //Debug.Log("ParatrooperView.StopElectrocute");

            //StopCoroutine("StartElectrocute");

            //var empty1 = skeletonAnimation.state.SetEmptyAnimation(0, 0.5f);
            //empty1.AttachmentThreshold = 1f;

            //lightningBolt2D.gameObject.SetActive(false);

            //skeletonAnimation.skeleton.SetAttachment("skeleton-electro", null);

            //skeletonAnimation.skeleton.SetAttachment("foot-back", "foot");
            //skeletonAnimation.skeleton.SetAttachment("foot-front", "foot");
            //skeletonAnimation.skeleton.SetAttachment("leg-upper-back", "leg-upper");
            //skeletonAnimation.skeleton.SetAttachment("leg-lower-back", "leg-lower");
            //skeletonAnimation.skeleton.SetAttachment("leg-upper-front", "leg-upper");
            //skeletonAnimation.skeleton.SetAttachment("leg-lower-front", "leg-lower");
            //skeletonAnimation.skeleton.SetAttachment("arm-upper-back", "arm-upper");
            //skeletonAnimation.skeleton.SetAttachment("arm-lower-back", "arm-lower");
            //skeletonAnimation.skeleton.SetAttachment("arm-upper-front", "arm-upper");
            //skeletonAnimation.skeleton.SetAttachment("arm-lower-front", "arm-lower");
            //skeletonAnimation.skeleton.SetAttachment("torso", "torso");
            //skeletonAnimation.skeleton.SetAttachment("head", "head");
            //skeletonAnimation.skeleton.SetAttachment("helmet", "naziHelmet");

            //if (!model.hasPlayedDead && model.isDead)
            //{

            //    if (model.isInAir)
            //    {
            //        model.hasPlayedDead = true;
            //        PlayGlideDeath();
            //    }
            //    else
            //    {
            //        model.hasPlayedDead = true;
            //        PlayDieDownBackwards();
            //    }
            //}
            //FIXME
        }

        public void PlayDie()
        {
            //DisableColliders();
            //FIXME ChatGPT this, why disable colliders on death?

            if (model.isInAir)
            {
                PlayGlideDeath();
            }
            else
            {
                PlayDieDownBackwards();
            }
        }

        public void PlayLandDie()
        {
            if (!model.hasExploded && !model.isMagicSpelled)
            {
                skeletonAnimation.state.SetEmptyAnimations(0);
                spineAnimationState.ClearTracks();
                var randTrack = Random.Range(1, 3);
                switch (randTrack)
                {
                    case 1:
                        skeletonAnimation.state.SetAnimation(0, landFallDownBackAnim, false);
                        break;
                    case 2:
                        skeletonAnimation.state.SetAnimation(0, landFallDownBack2Anim, false);
                        break;
                    case 3:
                        skeletonAnimation.state.SetAnimation(0, landFallDownBack3Anim, false);
                        break;
                }

                DropWeapon();
                HideWeapon();
            }
        }

        public void PlayOnFire()
        {
            if (model.isInAir)
            {
                PlayGlideOnFire();
            }
            else
            {
                PlayLandOnFire();
            }
        }

        public void PlayGlideOnFire()
        {
            Debug.Log("PlayGlideOnFire()");
            fireParticleSystemTrigger.GetComponent<Collider2D>().enabled = false;
            DisableColliders();
            // Play the shoot animation on track 1.
            var track = spineAnimationState.SetAnimation(1, glideFire, true);
            //track.AttachmentThreshold = 1f;
            track.MixDuration = 0f;

            StartCoroutine(StopGlideOnFire());

            model.isDead = true;

            flameThrower.gameObject.SetActive(true);
            flameThrower.Play();

            //TODO Add sound
        }



        IEnumerator StopGlideOnFire()
        {
            yield return new WaitForSeconds(1.5f);

            if (model.isInAir)
            {
                var track = spineAnimationState.SetAnimation(1, glideFireDeath, false);
                //track.AttachmentThreshold = 1f;
                track.MixDuration = 0f;
            }
            else
            {
                skeletonAnimation.skeleton.SetAttachment("foot-back", "foot-fire");
                skeletonAnimation.skeleton.SetAttachment("foot-front", "foot-fire");
                skeletonAnimation.skeleton.SetAttachment("leg-upper-back", "leg-upper-fire");
                skeletonAnimation.skeleton.SetAttachment("leg-lower-back", "leg-lower-fire");
                skeletonAnimation.skeleton.SetAttachment("leg-upper-front", "leg-upper-fire");
                skeletonAnimation.skeleton.SetAttachment("leg-lower-front", "leg-lower-fire");
                skeletonAnimation.skeleton.SetAttachment("arm-upper-back", "arm-upper-fire");
                skeletonAnimation.skeleton.SetAttachment("arm-lower-back", "arm-lower-fire");
                skeletonAnimation.skeleton.SetAttachment("arm-upper-front", "arm-upper-fire");
                skeletonAnimation.skeleton.SetAttachment("arm-lower-front", "arm-lower-fire");
                skeletonAnimation.skeleton.SetAttachment("torso", "torso-fire");
                skeletonAnimation.skeleton.SetAttachment("head", "head-fire");
            }

            flameThrower.gameObject.SetActive(false);
            flameThrower.Stop();

            model.isOnFire = false;
        }

        public void PlayLandOnFire()
        {
            Debug.Log("PlayLandOnFire()");
            fireParticleSystemTrigger.GetComponent<Collider2D>().enabled = false;
            DisableColliders();
            HideParachute();
            // Play the shoot animation on track 1.
            var track = spineAnimationState.SetAnimation(1, fireRunAnim, true);
            //track.AttachmentThreshold = 1f;
            track.MixDuration = 0f;

            model.shouldRun = true;

            StartCoroutine(StopRun());

            DropWeapon();

            model.isDead = true;

            flameThrower.gameObject.SetActive(true);
            flameThrower.Play();

            //TODO Add sound
        }

        IEnumerator StopRun()
        {
            yield return new WaitForSeconds(1.5f);

            HideParachute();

            var empty2 = skeletonAnimation.state.AddEmptyAnimation(1, 0.5f, 0.1f);
            //empty2.AttachmentThreshold = 1f;

            var track = spineAnimationState.SetAnimation(0, fireFallForwardDeathAnim, false);
            //track.AttachmentThreshold = 1f;
            track.MixDuration = 0f;

            model.shouldRun = false;

            flameThrower.gameObject.SetActive(false);
            flameThrower.Stop();

            model.isOnFire = false;
        }

        public void PlayGlide()
        {
            Debug.Log("PlayGlide()");
            // Play the shoot animation on track 1.
            var track = spineAnimationState.SetAnimation(1, glideAnim, true);
            //track.AttachmentThreshold = 1f;
            track.MixDuration = 0f;

            //TODO Add sound
        }

        private void HideWeapon()
        {
            skeleton.SetAttachment("gunSlot", null);
        }


        private void PlayLand_Complete(Spine.TrackEntry trackEntry)
        {
            if (model.isDead || model.hasExploded || model.isOnFire)
            {
                return;
            }

            HideBackpack();

            Debug.Log("PlayLand_Complete()");
            //TODO Move to config
            int throwGrenadePossibility = Random.Range(1, 10);
            if (throwGrenadePossibility > 7)
            {
                PlayGrenade();
            }
            else
            {
                PlayShootAutomatic();
            }
        }

        public void PlayShootAutomatic()
        {
            if (model.isDead || model.hasExploded || model.isOnFire)
            {
                return;
            }
            Debug.Log("PlayShootAutomatic()");
            // Play the shoot animation on track 1.
            var empty2 = skeletonAnimation.state.AddEmptyAnimation(1, 0.5f, 0.1f);
            //empty2.AttachmentThreshold = 1f;

            var track = spineAnimationState.SetAnimation(0, shootingMP40Anim, true);
            //track.AttachmentThreshold = 1f;
            track.MixDuration = 0f;
        }


        public void PlayGrenade()
        {
            if (model.isDead || model.hasExploded || model.isOnFire)
            {
                return;
            }
            Debug.Log("PlayGrenade()");
            // Play the shoot animation on track 1.
            Debug.Log("PlayGrenade(): " + spineAnimationState == null);
            var empty2 = skeletonAnimation.state.AddEmptyAnimation(1, 0.5f, 0.1f);
            //empty2.AttachmentThreshold = 1f;

            var track = spineAnimationState.SetAnimation(0, grenadeAnim, false);
            //track.AttachmentThreshold = 1f;
            track.MixDuration = 0f;
            track.Complete += PlayGrenade_Complete;

            //TODO Add sound
        }

        public void PlayGlideDeath()
        {
            Debug.Log("PlayGlideDeath()");
            var track = spineAnimationState.SetAnimation(1, glideDeathAnim, false);
            //track.AttachmentThreshold = 1f;
            track.MixDuration = 0f;

            DisableColliders();
            //TODO Add sound
        }

        public void PlayMagicSpelled()
        {
            Debug.Log("PlayMagicSpelled()");
            DisableColliders();
            if (model.isInAir)
            {
                var track = spineAnimationState.SetAnimation(1, glideMagicHitTorso, false);
                //track.AttachmentThreshold = 1f;
                track.MixDuration = 0f;
                track.Complete += PlayMagicSpelled_Complete;
                model.isDead = true;
            }
            else
            {
                var track = spineAnimationState.SetAnimation(1, magicHitTorsoMP40Anim, false);
                //track.AttachmentThreshold = 1f;
                track.MixDuration = 0f;
                track.Complete += PlayMagicSpelled_Complete;
                model.isDead = true;
            }

            //TODO Add sound
        }

        private void PlayMagicSpelled_Complete(Spine.TrackEntry trackEntry)
        {
            //TODO Pool boss
            Destroy(gameObject);
        }

        private void PlayGrenade_Complete(Spine.TrackEntry trackEntry)
        {
            Debug.Log("PlayGrenade_Complete()");

            PlayShootAutomatic();
        }

        #endregion
    }
}

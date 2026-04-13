using Spine.Unity;
using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Assets.Scripts.Hero_V2
{
    /*
 * HeroView_V2 (Presentation Layer)
 *
 * PURPOSE:
 * HeroView_V2 is responsible for all visual representation of the Hero.
 * It reacts to gameplay state changes and translates them into visuals.
 *
 * ---------------------------------------------------------
 * CORE PRINCIPLE:
 *
 * The View layer MUST NOT contain any gameplay logic.
 * It is strictly responsible for presentation only.
 *
 * ---------------------------------------------------------
 * ❌ MUST NOT DO:
 *
 * - Make gameplay decisions
 * - Read input
 * - Know or enforce gameplay rules
 * - Contain state machine logic
 * - Decide when actions like shooting, reloading, or moving happen
 *
 * ---------------------------------------------------------
 * ✅ RESPONSIBILITIES:
 *
 * - Play animations (Spine / sprites)
 * - Render visual effects (VFX)
 * - Handle visual flipping
 * - React to events from gameplay systems
 *
 * ---------------------------------------------------------
 * ARCHITECTURE NOTE:
 *
 * HeroView_V2:
 * - Only plays animations
 * - Only handles visual flipping
 * - Contains no gameplay logic
  */
    internal class HeroView_V2 : MonoBehaviour
    {
        private SkeletonAnimation _skeletonAnimation;

        private HeroStateMachine_V2 _stateMachine;
        private HeroDamageReceiver_V2 _damageReceiver;
        private HeroDeathHandler_V2 _deathHandler;

        // -------------------------
        // INIT
        // -------------------------
        public void Initialize(
            HeroStateMachine_V2 stateMachine,
            HeroDamageReceiver_V2 damageReceiver,
            HeroDeathHandler_V2 deathHandler,
            SkeletonAnimation skeletonAnimation)
        {
            _stateMachine = stateMachine;
            _damageReceiver = damageReceiver;
            _deathHandler = deathHandler;
            _skeletonAnimation = skeletonAnimation;

            // Subscribe to events
            stateMachine.OnStateChanged += HandleStateChanged;
            damageReceiver.OnDamageTaken += HandleDamageTaken;
            deathHandler.OnDeathHandled += HandleDeath;
        }

        private void OnDestroy()
        {
            if (_stateMachine != null)
                _stateMachine.OnStateChanged -= HandleStateChanged;

            if (_damageReceiver != null)
                _damageReceiver.OnDamageTaken -= HandleDamageTaken;

            if (_deathHandler != null)
                _deathHandler.OnDeathHandled -= HandleDeath;
        }

        // -------------------------
        // STATE → ANIMATION
        // -------------------------
        private void HandleStateChanged(HeroState from, HeroState to)
        {
            switch (to)
            {
                case HeroState.Idle:
                    PlayLoop("idle");
                    break;

                case HeroState.Moving:
                    PlayLoop("run");
                    break;

                case HeroState.Shooting:
                    PlayOneShot("shoot");
                    break;

                case HeroState.Reloading:
                    PlayOneShot("reload");
                    break;

                case HeroState.Dead:
                    PlayOneShot("dead");
                    break;
            }
        }

        // -------------------------
        // DAMAGE VISUALS
        // -------------------------
        private void HandleDamageTaken(int damage)
        {
            // hit flash / recoil animation / vignette trigger etc
            Debug.Log($"Hero took {damage} damage");
        }

        // -------------------------
        // FLIP (vänster/höger)
        // -------------------------
        public void Flip(float direction)
        {
            var scale = transform.localScale;
            scale.x = Mathf.Sign(direction) * Mathf.Abs(scale.x);
            transform.localScale = scale;
        }

        // -------------------------
        // DEATH VISUALS
        // -------------------------
        private void HandleDeath()
        {
            // extra VFX layer (camera shake, particles, sound trigger)
            Debug.Log("Hero death visuals triggered");
        }

        // -------------------------
        // HELPERS
        // -------------------------
        private void PlayLoop(string anim)
        {
            _skeletonAnimation.AnimationState.SetAnimation(0, anim, true);
        }

        private void PlayOneShot(string anim)
        {
            _skeletonAnimation.AnimationState.SetAnimation(0, anim, false);
            _skeletonAnimation.AnimationState.AddAnimation(0, "idle", true, 0f);
        }
    }
}

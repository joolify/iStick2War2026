using Spine.Unity;
using UnityEngine;

namespace iStick2War_V2
{
    /*
 * KamikazeDroneView_V2 (Presentation Layer)
 *
 * PURPOSE:
 * Spine visuals for the kamikaze stack: one looping clip (default name "fly") regardless of
 * Idle/Fly/Die from the state machine’s perspective—state changes keep the hook ready if clips split later.
 *
 * ---------------------------------------------------------
 * CORE PRINCIPLE:
 *
 * Presentation only. No bunker targeting, no explosion, no pooling.
 *
 * ---------------------------------------------------------
 * ❌ MUST NOT DO:
 *
 * - Duplicate logic from EnemyKamikazeDrone_V2 (rotation there is optional via its own fields)
 *
 * ---------------------------------------------------------
 * ✅ RESPONSIBILITIES:
 *
 * - Resolve SkeletonAnimation (serialized or search)
 * - Subscribe to KamikazeDroneStateMachine_V2.OnStateChanged and call SetAnimation
 *
 * ---------------------------------------------------------
 * ARCHITECTURE NOTE:
 *
 * Prefer placing this on the object that owns SkeletonAnimation; composition root may AddComponent
 * on the root if missing—assign references on the prefab when possible.
 */
    public sealed class KamikazeDroneView_V2 : MonoBehaviour
    {
        [SerializeField] private SkeletonAnimation _skeletonAnimation;
        [Tooltip("Kamikaze drone currently has one Spine clip.")]
        [SerializeField] private string _singleAnim = "fly";

        private KamikazeDroneStateMachine_V2 _stateMachine;

        public SkeletonAnimation SkeletonAnimation => _skeletonAnimation;

        public void Initialize(KamikazeDroneStateMachine_V2 stateMachine)
        {
            _stateMachine = stateMachine;
            if (_skeletonAnimation == null)
            {
                _skeletonAnimation = GetComponent<SkeletonAnimation>();
                if (_skeletonAnimation == null)
                {
                    _skeletonAnimation = GetComponentInChildren<SkeletonAnimation>(true);
                }
            }

            if (_stateMachine != null)
            {
                _stateMachine.OnStateChanged -= HandleStateChanged;
                _stateMachine.OnStateChanged += HandleStateChanged;
            }

            PlayForState(_stateMachine != null ? _stateMachine.CurrentState : KamikazeDroneState_V2.Idle);
        }

        private void OnDestroy()
        {
            if (_stateMachine != null)
            {
                _stateMachine.OnStateChanged -= HandleStateChanged;
            }
        }

        public void ResetVisualStateForSpawn()
        {
            PlayForState(KamikazeDroneState_V2.Idle);
        }

        private void HandleStateChanged(KamikazeDroneState_V2 from, KamikazeDroneState_V2 to)
        {
            PlayForState(to);
        }

        private void PlayForState(KamikazeDroneState_V2 state)
        {
            if (_skeletonAnimation == null || _skeletonAnimation.AnimationState == null || string.IsNullOrWhiteSpace(_singleAnim))
            {
                return;
            }

            _skeletonAnimation.AnimationState.SetAnimation(0, _singleAnim, true);
        }
    }
}

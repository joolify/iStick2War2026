using Spine.Unity;
using UnityEngine;

namespace iStick2War_V2
{
    /*
 * BombDroneView_V2 (Presentation Layer)
 *
 * PURPOSE:
 * Spine presentation for the bomb drone: looping fly clip, optional one-shot dropBomb when
 * state becomes DropBomb (default clip name matches typical Spine export).
 *
 * ---------------------------------------------------------
 * CORE PRINCIPLE:
 *
 * The View MUST NOT decide bunker alignment or when the bomb spawns.
 * It only reacts to BombDroneStateMachine_V2.OnStateChanged.
 *
 * ---------------------------------------------------------
 * ❌ MUST NOT DO:
 *
 * - Reference BunkerHitbox_V2 or Camera
 * - Call SimplePrefabPool_V2 or BombProjectile_V2
 *
 * ---------------------------------------------------------
 * ✅ RESPONSIBILITIES:
 *
 * - Resolve SkeletonAnimation (serialized or GetComponent / InChildren)
 * - Subscribe to state changes and call SetAnimation for fly / dropBomb
 *
 * ---------------------------------------------------------
 * ARCHITECTURE NOTE:
 *
 * Often lives on the same object or child as SkeletonAnimation; composition root may add a
 * duplicate component if none exists on the root—prefer assigning the View on the drone prefab.
 */
    public sealed class BombDroneView_V2 : MonoBehaviour
    {
        [SerializeField] private SkeletonAnimation _skeletonAnimation;
        [SerializeField] private string _flyAnim = "fly";
        [SerializeField] private string _dropBombAnim = "dropBomb";

        private BombDroneStateMachine_V2 _stateMachine;

        public SkeletonAnimation SkeletonAnimation => _skeletonAnimation;

        public void Initialize(BombDroneStateMachine_V2 stateMachine)
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

            PlayForState(_stateMachine != null ? _stateMachine.CurrentState : BombDroneState_V2.Idle);
        }

        public void ResetVisualStateForSpawn()
        {
            PlayForState(BombDroneState_V2.Idle);
        }

        private void OnDestroy()
        {
            if (_stateMachine != null)
            {
                _stateMachine.OnStateChanged -= HandleStateChanged;
            }
        }

        private void HandleStateChanged(BombDroneState_V2 from, BombDroneState_V2 to)
        {
            PlayForState(to);
        }

        private void PlayForState(BombDroneState_V2 state)
        {
            if (_skeletonAnimation == null || _skeletonAnimation.AnimationState == null)
            {
                return;
            }

            if (state == BombDroneState_V2.DropBomb && !string.IsNullOrWhiteSpace(_dropBombAnim))
            {
                _skeletonAnimation.AnimationState.SetAnimation(0, _dropBombAnim, false);
                return;
            }

            if (!string.IsNullOrWhiteSpace(_flyAnim))
            {
                _skeletonAnimation.AnimationState.SetAnimation(0, _flyAnim, true);
            }
        }
    }
}

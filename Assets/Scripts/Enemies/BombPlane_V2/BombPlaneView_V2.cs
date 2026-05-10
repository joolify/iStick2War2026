using Spine;
using Spine.Unity;
using UnityEngine;

namespace iStick2War_V2
{
    /*
 * BombPlaneView_V2 (Presentation Layer)
 *
 * PURPOSE:
 * Visuals for the bomb plane: Spine track selection driven by BombPlaneStateMachine_V2,
 * plus helpers to sample bone world positions for gameplay (bomb spawn origin).
 *
 * ---------------------------------------------------------
 * CORE PRINCIPLE:
 *
 * The View MUST NOT decide when bombs drop or when the plane despawns.
 * It reacts to state changes and exposes read-only spatial queries for the Controller.
 *
 * ---------------------------------------------------------
 * ❌ MUST NOT DO:
 *
 * - Spawn projectiles or touch pooling
 * - Encode flight speed, camera margins, or bomb cadence
 *
 * ---------------------------------------------------------
 * ✅ RESPONSIBILITIES:
 *
 * - Resolve SkeletonAnimation (serialized or search)
 * - Play fly loop; optional one-shot drop animation when configured
 * - TryGetBoneWorldPosition for named bones (same sampling idea as EnemySpawner_V2 paratrooper bone)
 *
 * ---------------------------------------------------------
 * ARCHITECTURE NOTE:
 *
 * Typically lives on the child object that owns SkeletonAnimation (mesh + Spine),
 * while Bombplane_V2 on the root resolves it via GetComponentInChildren.
 */
    public sealed class BombPlaneView_V2 : AircraftDualClipSpineViewBase_V2<BombPlaneState_V2>
    {
        protected override BombPlaneState_V2 IdleStateValue => BombPlaneState_V2.Idle;

        protected override BombPlaneState_V2 DropBombStateValue => BombPlaneState_V2.DropBomb;

        public void Initialize(BombPlaneStateMachine_V2 stateMachine)
        {
            base.Initialize(stateMachine);
        }

        /// <summary>
        /// World position of a named Spine bone (e.g. bomb bay). Matches paratrooper spawn bone sampling in <c>EnemySpawner_V2</c>.
        /// </summary>
        public bool TryGetBoneWorldPosition(string boneName, out Vector3 worldPosition)
        {
            worldPosition = default;
            if (string.IsNullOrWhiteSpace(boneName) || _skeletonAnimation == null)
            {
                return false;
            }

            Skeleton skeleton = _skeletonAnimation.Skeleton;
            if (skeleton == null)
            {
                return false;
            }

            _skeletonAnimation.Update(0f);
            Bone bone = TryFindBoneByName(skeleton, boneName.Trim());
            if (bone == null)
            {
                return false;
            }

            worldPosition = _skeletonAnimation.transform.TransformPoint(new Vector3(bone.WorldX, bone.WorldY, 0f));
            return true;
        }

        private static Bone TryFindBoneByName(Skeleton skeleton, string requestedBoneName)
        {
            if (skeleton == null || string.IsNullOrWhiteSpace(requestedBoneName))
            {
                return null;
            }

            Bone exactMatch = skeleton.FindBone(requestedBoneName);
            if (exactMatch != null)
            {
                return exactMatch;
            }

            string normalizedRequested = NormalizeBoneName(requestedBoneName);
            ExposedList<Bone> bones = skeleton.Bones;
            if (bones == null)
            {
                return null;
            }

            for (int i = 0; i < bones.Count; i++)
            {
                Bone candidate = bones.Items[i];
                if (candidate == null || string.IsNullOrWhiteSpace(candidate.Data?.Name))
                {
                    continue;
                }

                if (string.Equals(candidate.Data.Name, requestedBoneName, System.StringComparison.OrdinalIgnoreCase))
                {
                    return candidate;
                }

                if (NormalizeBoneName(candidate.Data.Name) == normalizedRequested)
                {
                    return candidate;
                }
            }

            return null;
        }

        private static string NormalizeBoneName(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            return value
                .Trim()
                .Replace("_", string.Empty)
                .Replace("-", string.Empty)
                .Replace(" ", string.Empty)
                .ToLowerInvariant();
        }
    }
}

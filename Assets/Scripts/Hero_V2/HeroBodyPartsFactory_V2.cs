using iStick2War;
using Spine.Unity;
using UnityEngine;

namespace iStick2War_V2
{
    /*
 * HeroBodyPartsFactory_V2
 *
 * Builds Spine BoundingBoxFollower hitboxes under HERO BodyParts_V2 (same slots/bones as paratrooper Infantry).
 * Body-parts root is parented under the SkeletonAnimation transform so colliders inherit the same scale as the mesh
 * (hero view is often 0.5 while the prefab root stays 1 — otherwise bounding boxes appear 2× too large).
 */
    public static class HeroBodyPartsFactory_V2
    {
        public const string BodyPartsContainerName = "HERO BodyParts_V2";

        private readonly struct BodyPartSpec
        {
            public readonly string GameObjectName;
            public readonly string SlotName;
            public readonly string BoneName;
            public readonly BodyPartType PartType;

            public BodyPartSpec(string gameObjectName, string slotName, string boneName, BodyPartType partType)
            {
                GameObjectName = gameObjectName;
                SlotName = slotName;
                BoneName = boneName;
                PartType = partType;
            }
        }

        private static readonly BodyPartSpec[] Specs =
        {
            new BodyPartSpec("Head-Hitbox", "head-bb", "head", BodyPartType.Head),
            new BodyPartSpec("Torso-Hitbox", "torso-bb", "torso", BodyPartType.Torso),
            new BodyPartSpec("Arm-Upper-Front-Hitbox", "arm-upper-front-bb", "arm-upper-front", BodyPartType.ArmUpperFront),
            new BodyPartSpec("Arm-Lower-Front-Hitbox", "arm-lower-front-bb", "arm-lower-front", BodyPartType.ArmLowerFront),
            new BodyPartSpec("Arm-Upper-Back-Hitbox", "arm-upper-back-bb", "arm-upper-back", BodyPartType.ArmUpperBack),
            new BodyPartSpec("Arm-Lower-Back-Hitbox", "arm-lower-back-bb", "arm-lower-back", BodyPartType.ArmLowerBack),
            new BodyPartSpec("Leg-Upper-Front-Hitbox", "leg-upper-front-bb", "leg-upper-front", BodyPartType.LegUpperFront),
            new BodyPartSpec("Leg-Lower-Front-Hitbox", "leg-lower-front-bb", "leg-lower-front", BodyPartType.LegLowerFront),
            new BodyPartSpec("Leg-Upper-Back-Hitbox", "leg-upper-back-bb", "leg-upper-back", BodyPartType.LegUpperBack),
            new BodyPartSpec("Leg-Lower-Back-Hitbox", "leg-lower-back-bb", "leg-lower-back", BodyPartType.LegLowerBack),
            new BodyPartSpec("Foot-Front-Hitbox", "foot-front-bb", "foot-front", BodyPartType.FootFront),
            new BodyPartSpec("Foot-Back-Hitbox", "foot-back-bb", "foot-back", BodyPartType.FootBack),
        };

        public static bool EnsureBodyPartsOnHero(Hero_V2 hero, bool logWhenSkipped = false)
        {
            if (hero == null)
            {
                return false;
            }

            SkeletonAnimation skeletonAnimation = hero.GetComponentInChildren<SkeletonAnimation>(true);
            if (skeletonAnimation == null)
            {
                Debug.LogWarning($"[HeroBodyPartsFactory_V2] No {nameof(SkeletonAnimation)} on '{hero.name}'.");
                return false;
            }

            Transform bodyPartsRoot = FindOrCreateBodyPartsRoot(hero.transform, skeletonAnimation.transform);
            if (bodyPartsRoot.childCount > 0)
            {
                bool allValid = true;
                for (int i = 0; i < bodyPartsRoot.childCount; i++)
                {
                    if (bodyPartsRoot.GetChild(i).GetComponent<HeroBodyPart_V2>() == null)
                    {
                        allValid = false;
                        break;
                    }
                }

                if (allValid && bodyPartsRoot.childCount >= Specs.Length)
                {
                    if (logWhenSkipped)
                    {
                        Debug.Log($"[HeroBodyPartsFactory_V2] '{hero.name}' already has body parts; skipped.");
                    }

                    AlignBodyPartsRootToSkeleton(hero.transform, skeletonAnimation.transform);
                    ReinitializeBodyPartColliders(hero);
                    RepairBodyPartPhysics(hero);
                    return true;
                }
            }

            ClearChildren(bodyPartsRoot);

            int playerLayer = LayerMask.NameToLayer("Player");
            for (int i = 0; i < Specs.Length; i++)
            {
                CreateHitboxChild(bodyPartsRoot, skeletonAnimation, Specs[i], playerLayer);
            }

            AlignBodyPartsRootToSkeleton(hero.transform, skeletonAnimation.transform);
            RepairBodyPartPhysics(hero);
            Debug.Log($"[HeroBodyPartsFactory_V2] Created {Specs.Length} bounding-box hitboxes on '{hero.name}'.");
            return true;
        }

        // Hitbox colliders must attach to the hero root Rigidbody2D (no per-part RB), like paratrooper grounding.
        public static void RepairBodyPartPhysics(Hero_V2 hero)
        {
            if (hero == null)
            {
                return;
            }

            SkeletonAnimation skeletonAnimation = hero.GetComponentInChildren<SkeletonAnimation>(true);
            if (skeletonAnimation != null)
            {
                AlignBodyPartsRootToSkeleton(hero.transform, skeletonAnimation.transform);
            }

            HeroBodyPart_V2[] parts = hero.GetComponentsInChildren<HeroBodyPart_V2>(true);
            for (int i = 0; i < parts.Length; i++)
            {
                if (parts[i] == null)
                {
                    continue;
                }

                Rigidbody2D childRb = parts[i].GetComponent<Rigidbody2D>();
                if (childRb == null)
                {
                    continue;
                }

#if UNITY_EDITOR
                if (!Application.isPlaying)
                {
                    Object.DestroyImmediate(childRb);
                }
                else
#endif
                {
                    Object.Destroy(childRb);
                }
            }

            EnsureRootPhysicsCollider(hero);
        }

        private static void EnsureRootPhysicsCollider(Hero_V2 hero)
        {
            Rigidbody2D rb = hero.GetComponent<Rigidbody2D>();
            if (rb == null)
            {
                return;
            }

            if (HasEnabledSolidAttachedCollider(rb))
            {
                return;
            }

            BoxCollider2D rootBox = hero.GetComponent<BoxCollider2D>();
            if (rootBox != null && !rootBox.enabled)
            {
                rootBox.enabled = true;
                Debug.LogWarning(
                    $"[HeroBodyPartsFactory_V2] Re-enabled root BoxCollider2D on '{hero.name}' — hero Rigidbody2D needs at least one solid collider for ground physics.");
            }
        }

        private static bool HasEnabledSolidAttachedCollider(Rigidbody2D rb)
        {
            if (rb == null || rb.attachedColliderCount <= 0)
            {
                return false;
            }

            var scratch = new Collider2D[32];
            int count = rb.GetAttachedColliders(scratch);
            for (int i = 0; i < count; i++)
            {
                Collider2D c = scratch[i];
                if (c != null && c.enabled && !c.isTrigger)
                {
                    return true;
                }
            }

            return false;
        }

        private static Transform FindOrCreateBodyPartsRoot(Transform heroRoot, Transform skeletonTransform)
        {
            Transform existing = skeletonTransform.Find(BodyPartsContainerName);
            if (existing == null)
            {
                existing = heroRoot.Find(BodyPartsContainerName);
            }

            if (existing != null)
            {
                AlignBodyPartsRootToSkeleton(heroRoot, skeletonTransform);
                return existing;
            }

            var go = new GameObject(BodyPartsContainerName);
            Transform t = go.transform;
            t.SetParent(skeletonTransform, false);
            t.localPosition = Vector3.zero;
            t.localRotation = Quaternion.identity;
            t.localScale = Vector3.one;
            int playerLayer = LayerMask.NameToLayer("Player");
            if (playerLayer >= 0)
            {
                go.layer = playerLayer;
            }

            return t;
        }

        private static void AlignBodyPartsRootToSkeleton(Transform heroRoot, Transform skeletonTransform)
        {
            if (heroRoot == null || skeletonTransform == null)
            {
                return;
            }

            Transform bodyParts = skeletonTransform.Find(BodyPartsContainerName);
            if (bodyParts == null)
            {
                bodyParts = heroRoot.Find(BodyPartsContainerName);
            }

            if (bodyParts == null)
            {
                return;
            }

            if (bodyParts.parent != skeletonTransform)
            {
                bodyParts.SetParent(skeletonTransform, false);
            }

            bodyParts.localPosition = Vector3.zero;
            bodyParts.localRotation = Quaternion.identity;
            bodyParts.localScale = Vector3.one;
        }

        private static void ReinitializeBodyPartColliders(Hero_V2 hero)
        {
            HeroBodyPart_V2[] parts = hero.GetComponentsInChildren<HeroBodyPart_V2>(true);
            for (int i = 0; i < parts.Length; i++)
            {
                if (parts[i] != null)
                {
                    parts[i].StabilizeBoundingBoxFollowerCollider();
                }
            }
        }

        private static void CreateHitboxChild(
            Transform bodyPartsRoot,
            SkeletonAnimation skeletonAnimation,
            BodyPartSpec spec,
            int playerLayer)
        {
            var go = new GameObject(spec.GameObjectName);
            Transform t = go.transform;
            t.SetParent(bodyPartsRoot, false);
            if (playerLayer >= 0)
            {
                go.layer = playerLayer;
            }

            BoundingBoxFollower follower = go.AddComponent<BoundingBoxFollower>();
            follower.skeletonRenderer = skeletonAnimation;
            follower.slotName = spec.SlotName;
            follower.isTrigger = false;

            go.AddComponent<PolygonCollider2D>();

            BoneFollower boneFollower = go.AddComponent<BoneFollower>();
            boneFollower.skeletonRenderer = skeletonAnimation;
            boneFollower.boneName = spec.BoneName;
            boneFollower.followXYPosition = true;
            boneFollower.followZPosition = true;
            boneFollower.followBoneRotation = true;
            boneFollower.followSkeletonFlip = true;
            boneFollower.initializeOnAwake = true;

            HeroBodyPart_V2 bodyPart = go.AddComponent<HeroBodyPart_V2>();
            bodyPart.bodyPart = spec.PartType;
            bodyPart.StabilizeBoundingBoxFollowerCollider();
        }

        private static void ClearChildren(Transform root)
        {
            for (int i = root.childCount - 1; i >= 0; i--)
            {
                GameObject child = root.GetChild(i).gameObject;
#if UNITY_EDITOR
                if (!Application.isPlaying)
                {
                    Object.DestroyImmediate(child);
                    continue;
                }
#endif
                Object.Destroy(child);
            }
        }
    }
}

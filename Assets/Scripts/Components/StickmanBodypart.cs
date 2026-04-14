using Spine;
using Spine.Unity;
using System;
using UnityEngine;
using Random = UnityEngine.Random;

namespace iStick2War
{
    public enum BodyPartType
    {
        Head,
        ArmLowerFront,
        ArmLowerBack,
        ArmUpperFront,
        ArmUpperBack,
        Torso,
        LegLowerFront,
        LegLowerBack,
        LegUpperFront,
        LegUpperBack,
        FootFront,
        FootBack
    }

    public class StickmanBodypart : MonoBehaviour
    {
        private Collider2D coll2D;

        public float DamageMultiplier = 1f;

        [Header("Components")]
        public SkeletonAnimation skeletonAnimation;

        public Spine.AnimationState spineAnimationState;
        public Spine.Skeleton skeleton;

        public BodyPartType bodyPart;

        public float damageMultiplier = 1f;

        [SpineBone(dataField: "skeletonAnimation")]
        public string bodyPartBoneName;
        public Bone bodyPartBone;

        public Damageable damageable;

        public BaseModel model;
        // Start is called before the first frame update
        private void Awake()
        {
            if (skeletonAnimation == null) skeletonAnimation = GetComponent<SkeletonAnimation>();
            if (skeletonAnimation == null) return;
            skeletonAnimation.Initialize(false);
            if (!skeletonAnimation.valid) return;

            if (damageable == null) damageable = transform.parent.GetComponent<Damageable>();
        }

        void Update()
        {
            var rb = GetComponent<Rigidbody2D>();
            var col = GetComponent<PolygonCollider2D>();

            Debug.Log(
                gameObject.name +
                " | active: " + gameObject.activeInHierarchy +
                " | simulated: " + (rb != null && rb.simulated) +
                " | enabled: " + col.enabled +
                " | points: " + col.points.Length +
                " | scale: " + transform.lossyScale
            );
        }

        void Start()
        {
            if (skeletonAnimation == null) skeletonAnimation = GetComponent<SkeletonAnimation>();
            if (skeletonAnimation == null) return;
            skeletonAnimation.Initialize(false);
            if (!skeletonAnimation.valid) return;

            if (damageable == null) damageable = transform.parent.GetComponent<Damageable>();
            if (DamageMultiplier <= 0) Debug.LogError("Body part - Damage multiplier can't be 0 or less.");

            spineAnimationState = skeletonAnimation.AnimationState;
            skeleton = skeletonAnimation.Skeleton;

            coll2D = GetComponent<Collider2D>();

            if (bodyPartBone == null) bodyPartBone = skeleton.FindBone(bodyPartBoneName);
        }

        void LateUpdate()
        {
            if (bodyPartBone == null) return;

            // Convert Spine bone position → Unity world position
            Vector3 boneWorldPos = skeletonAnimation.transform.TransformPoint(
                new Vector3(bodyPartBone.WorldX, bodyPartBone.WorldY, 0)
            );

            transform.position = boneWorldPos;
        }

        public void TakeDamage(float damage)
        {
            Debug.Log("StickmanBodypart.damageable" + (damageable == null));
            Debug.Log("StickmanBodypart.damageable.stats" + (damageable.stats == null));
            damageable.stats.curHealth -= damage * DamageMultiplier;
            Debug.Log("StickmanBodyPart.TakeDamage " + gameObject.name + " got hit " + damage * DamageMultiplier  + ", curHealth: " + damageable.stats.curHealth);

            Debug.Log("Simulated: " + GetComponent<Rigidbody2D>().simulated);
            Debug.Log("Scale: " + transform.lossyScale);
            var poly = GetComponent<PolygonCollider2D>();
            Debug.Log("Points: " + poly.points.Length);

            if (damageable.stats.curHealth <= 0)
            {
                Debug.Log("StickmanBodyPart.TakeDamage.isDead = true, " + bodyPart);
                //TODO Move to Pool boss
                model.isDead = true;
                switch (bodyPart)
                {
                    case BodyPartType.Head:
                        model.ShootHead();
                        model.DropHelmet();
                        break;
                    case BodyPartType.ArmLowerBack:
                    case BodyPartType.ArmLowerFront:
                    case BodyPartType.ArmUpperBack:
                    case BodyPartType.ArmUpperFront:
                        model.ShootArms();
                        break;
                    case BodyPartType.Torso:
                        model.ShootTorso();
                        model.DropHelmet();
                        break;
                    case BodyPartType.LegLowerBack:
                    case BodyPartType.LegLowerFront:
                    case BodyPartType.LegUpperBack:
                    case BodyPartType.LegUpperFront:
                        model.ShootLegs();
                        break;
                    case BodyPartType.FootBack:
                    case BodyPartType.FootFront:
                        model.ShootFeet();
                        break;
                    default:
                        Debug.LogError("Bodypart not set!");
                        break;
                }
                if (!model.isInAir)
                    model.DropWeapon();
                model.Die();

                //foreach (Transform child in transform.parent)
                //{
                //    if (child.GetComponent<StickmanBodypart>())
                //    {
                //        child.GetComponent<Collider2D>().enabled = false;
                //    }
                //}

                Debug.Log("SETTING DEAD LAYER");
                //int deadLayer = LayerMask.NameToLayer("EnemyDead");
                //SetLayerRecursively(model.gameObject, deadLayer);

                //FIXME Collider2D
            }
        }

        void SetLayerRecursively(GameObject obj, int newLayer)
        {
            obj.layer = newLayer;

            foreach (Transform child in obj.transform)
            {
                SetLayerRecursively(child.gameObject, newLayer);
            }
        }
    }
}

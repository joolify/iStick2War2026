using Spine;
using Spine.Unity;
using System;
using UnityEngine;
using Random = UnityEngine.Random;

namespace iStick2War
{
    public enum BodyPart
    {
        Head,
        Arms,
        Torso,
        Legs,
    }

    public class StickmanBodypart : MonoBehaviour
    {
        private Collider2D coll2D;

        public float DamageMultiplier = 1f;

        [Header("Components")]
        public SkeletonAnimation skeletonAnimation;

        public Spine.AnimationState spineAnimationState;
        public Spine.Skeleton skeleton;

        public BodyPart bodyPart;

        [SpineBone(dataField: "skeletonAnimation")]
        public string bloodBoneName;
        public Bone bloodBone;

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

            if (bloodBone == null) bloodBone = skeleton.FindBone(bloodBoneName);
        }

        public void TakeDamage(float damage)
        {
            Debug.Log("StickmanBodypart.damageable" + (damageable == null));
            Debug.Log("StickmanBodypart.damageable.stats" + (damageable.stats == null));
            damageable.stats.curHealth -= damage * DamageMultiplier;
            Debug.Log("StickmanBodyPart.TakeDamage " + gameObject.name + " got hit " + damage * DamageMultiplier);

            if (damageable.stats.curHealth <= 0)
            {
                //TODO Move to Pool boss
                model.isDead = true;
                switch (bodyPart)
                {
                    case BodyPart.Head:
                        model.ShootHead();
                        model.DropHelmet();
                        break;
                    case BodyPart.Arms:
                        model.ShootArms();
                        break;
                    case BodyPart.Torso:
                        model.ShootTorso();
                        model.DropHelmet();
                        break;
                    case BodyPart.Legs:
                        model.ShootLegs();
                        break;
                    default:
                        Debug.LogError("Bodypart not set!");
                        break;
                }
                if (!model.isInAir)
                    model.DropWeapon();
                model.Die();

                foreach (Transform child in transform.parent)
                {
                    if (child.GetComponent<StickmanBodypart>())
                    {
                        child.GetComponent<Collider2D>().enabled = false;
                    }
                }
            }
        }
    }
}

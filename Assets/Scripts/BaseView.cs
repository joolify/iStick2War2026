using Spine;
using Spine.Unity;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace iStick2War
{
    public class BaseView : MonoBehaviour
    {
        #region Inspector
        [Header("Components")]
        public SkeletonAnimation skeletonAnimation;

        [Header("Explosion")]
        public GameObject armUpper1;
        public GameObject armUpper2;
        public GameObject head1;
        public GameObject head2;
        public GameObject legUpper1;
        public GameObject legUpper2;
        public GameObject legUpper3;
        public GameObject legUpper4;
        public GameObject torso1;
        public GameObject torso2;
        public GameObject torso3;

        [Header("Animations")]
        public AnimationReferenceAsset fallDownBackwardAnim;
        public AnimationReferenceAsset fallDownBackward2Anim;
        public AnimationReferenceAsset fallDownBackward3Anim;
        public AnimationReferenceAsset fallBackwardAnim;
        public AnimationReferenceAsset fallBackward2Anim;
        public AnimationReferenceAsset fallForwardAnim;

        [Space]
        public Transform weapon; //TODO Need array of weapons
        public Transform helmet; //TODO Need array of helmets

        //This is for dropping weapons and helmets when dead
        [SpineBone(dataField: "skeletonAnimation")]
        public string armsBoneName;
        public Bone armsBone;
        [SpineBone(dataField: "skeletonAnimation")]
        public string headBoneName;
        public Bone headBone;

        [Space]
        public EventDataReferenceAsset footstepEvent;

        [Header("Audio")]
        public float footstepPitchOffset = 0.2f;
        public float gunsoundPitchOffset = 0.13f;
        public AudioSource footstepSource, gunSource, jumpSource;

        [Header("Effects")]
        public ParticleSystem gunParticles;
        #endregion

        protected StickmanBodyState previousBodyViewState;
        protected StickmanGunState previousGunViewState;

        public Spine.AnimationState spineAnimationState;
        public Spine.Skeleton skeleton;

        private Flippable flippable;
        private Transformable transformable;

        void Awake()
        {
            skeletonAnimation = GetComponent<SkeletonAnimation>();
            flippable = GetComponent<Flippable>();

            if (spineAnimationState == null) spineAnimationState = skeletonAnimation.AnimationState;
            if (skeleton == null) skeleton = skeletonAnimation.Skeleton;

            if (armsBone == null) armsBone = skeleton?.FindBone(armsBoneName);
            if (headBone == null) headBone = skeleton?.FindBone(headBoneName);

            skeleton?.SetAttachment("crosshair", null);
        }

        protected virtual void Start()
        {
            if (skeletonAnimation == null) return;

            if (spineAnimationState == null) spineAnimationState = skeletonAnimation.AnimationState;
            if (skeleton == null) skeleton = skeletonAnimation.Skeleton;

            if (armsBone == null) armsBone = skeleton.FindBone(armsBoneName);
            if (headBone == null) headBone = skeleton.FindBone(headBoneName);

        }

        protected void PlayFootstepSound()
        {
            footstepSource.Play();
            footstepSource.pitch = GetRandomPitch(footstepPitchOffset);
        }

        [ContextMenu("Check Tracks")]
        void CheckTracks()
        {
            var state = skeletonAnimation.AnimationState;
            Debug.Log(state.GetCurrent(0));
            Debug.Log(state.GetCurrent(1));
        }
        #region Transient Actions

        public virtual void Explode()
        {
            skeletonAnimation.state.SetEmptyAnimations(0);
            spineAnimationState.ClearTracks();
            Debug.Log("Explode");
            var arms = new List<GameObject> { armUpper1, armUpper2 };
            var randArm = Random.Range(0, arms.Count);

            var arm1 = Instantiate(arms[randArm], skeletonAnimation.transform.position, UnityEngine.Quaternion.Euler(0f, 0f, 0f));
            var arm1Rb = arm1.GetComponent<Rigidbody2D>();
            arm1Rb.AddForce(GetRandomDirection() * GetRandomForce());
            arm1Rb.AddTorque(GetRandomTorque(), ForceMode2D.Force);

            var arm2 = Instantiate(arms[randArm], skeletonAnimation.transform.position, UnityEngine.Quaternion.Euler(0f, 0f, 0f));
            var arm2Rb = arm2.GetComponent<Rigidbody2D>();
            arm2Rb.AddForce(GetRandomDirection() * GetRandomForce());
            arm2Rb.AddTorque(GetRandomTorque(), ForceMode2D.Force);

            var legs = new List<GameObject> { legUpper1, legUpper2, legUpper3, legUpper4 };
            var randLeg = Random.Range(0, legs.Count);

            var leg1 = Instantiate(legs[randLeg], skeletonAnimation.transform.position, UnityEngine.Quaternion.Euler(0f, 0f, 0f));
            var leg1Rb = leg1.GetComponent<Rigidbody2D>();
            leg1Rb.AddForce(GetRandomDirection() * GetRandomForce());
            leg1Rb.AddTorque(GetRandomTorque(), ForceMode2D.Force);

            var leg2 = Instantiate(legs[randLeg], skeletonAnimation.transform.position, UnityEngine.Quaternion.Euler(0f, 0f, 0f));
            var leg2Rb = leg2.GetComponent<Rigidbody2D>();
            leg2Rb.AddForce(GetRandomDirection() * GetRandomForce());
            leg2Rb.AddTorque(GetRandomTorque(), ForceMode2D.Force);

            var torsos = new List<GameObject> { torso1, torso2, torso3 };
            var randTorso = Random.Range(0, torsos.Count);
            var torso = Instantiate(torsos[randTorso], skeletonAnimation.transform.position, UnityEngine.Quaternion.Euler(0f, 0f, 0f));
            var torsoRb = torso.GetComponent<Rigidbody2D>();
            torsoRb.AddForce(GetRandomDirection() * GetRandomForce());
            torsoRb.AddTorque(GetRandomTorque(), ForceMode2D.Force);

            HideBody();
            HideParachute();
            HideBackpack();
            foreach (Transform child in transform)
            {
                if (child.GetComponent<StickmanBodypart>())
                {
                    child.GetComponent<Collider2D>().enabled = false;
                }
            }
        }

        private void HideBody()
        {
            skeleton.SetAttachment("helmet", null);
            skeleton.SetAttachment("head", null);
            skeleton.SetAttachment("arm-upper-back", null);
            skeleton.SetAttachment("arm-lower-back", null);
            skeleton.SetAttachment("arm-upper-front", null);
            skeleton.SetAttachment("arm-lower-front", null);
            skeleton.SetAttachment("leg-upper-front", null);
            skeleton.SetAttachment("leg-lower-front", null);
            skeleton.SetAttachment("leg-upper-back", null);
            skeleton.SetAttachment("leg-lower-back", null);
            skeleton.SetAttachment("torso", null);
            skeleton.SetAttachment("foot-back", null);
            skeleton.SetAttachment("foot-front", null);
            skeleton.SetAttachment("gunSlot", null);
        }

        protected float GetRandomForce()
        {
            return Random.Range(600, 900);
        }

        protected float GetRandomTorque()
        {
            return Random.Range(25, 100);
        }

        protected Vector2 GetRandomDirection()
        {
            return Random.insideUnitCircle.normalized;
        }

        public void PlayShootHead()
        {
            Debug.Log("PlayShootHead()");

            var list = new List<string>()
        {
            "headBlown",
            "headOff4",
            "headOff5",
            "headOff6",
            "headOff7",
        };

            int index = Random.Range(0, list.Count - 1);
            skeleton.SetAttachment("head", list[index]);
            skeleton.SetAttachment("gunSlot", null);
        }

        public void PlayShootArms()
        {
            var list = new List<string>()
        {
            "arm-upper-shoot",
            "arm-upper-shoot2",
        };

            int index = Random.Range(0, list.Count - 1);
            skeleton.SetAttachment("arm-upper-back", list[index]);
            skeleton.SetAttachment("arm-upper-front", list[index]);
            skeleton.SetAttachment("arm-lower-front", null);
            skeleton.SetAttachment("arm-lower-back", null);
            skeleton.SetAttachment("gunSlot", null);
        }
        public void PlayShootTorso()
        {
            Debug.Log("PlayShootTorso()");

            var list = new List<string>()
        {
            "torso-shoot",
            "torso-shoot2",
            "torso-shoot3",
        };

            int index = Random.Range(0, list.Count - 1);
            skeleton.SetAttachment("torso", list[index]);
            skeleton.SetAttachment("head", null);
            skeleton.SetAttachment("arm-upper-back", null);
            skeleton.SetAttachment("arm-upper-front", null);
            skeleton.SetAttachment("arm-lower-front", null);
            skeleton.SetAttachment("arm-lower-front", null);
            skeleton.SetAttachment("gunSlot", null);
        }
        public void PlayShootLegs()
        {
            Debug.Log("PlayShootLegs()");

            var list = new List<string>()
        {
            "leg-upper-shoot",
            "leg-upper-shoot2",
            "leg-upper-shoot3",
            "leg-upper-shoot4",
        };

            int index = Random.Range(0, list.Count - 1);
            skeleton.SetAttachment("leg-upper-front", list[index]);
            skeleton.SetAttachment("leg-upper-back", list[index]);
            skeleton.SetAttachment("leg-lower-front", null);
            skeleton.SetAttachment("leg-lower-back", null);
            skeleton.SetAttachment("foot-back", null);
            skeleton.SetAttachment("foot-front", null);
            skeleton.SetAttachment("gunSlot", null);
        }


        public void PlayDieDownBackwards()
        {
            HideParachute();
            skeletonAnimation.state.SetEmptyAnimations(0);
            spineAnimationState.ClearTracks();
            var randTrack = Random.Range(1, 3);
            switch (randTrack)
            {
                case 1:
                    skeletonAnimation.state.SetAnimation(1, fallDownBackwardAnim, false);
                    break;
                case 2:
                    skeletonAnimation.state.SetAnimation(1, fallDownBackward2Anim, false);
                    break;
                case 3:
                    skeletonAnimation.state.SetAnimation(1, fallDownBackward3Anim, false);
                    break;
            }
        }

        public void PlayDieBackwards()
        {
            Debug.Log("PlayDieBackwards");

            HideParachute();
            skeletonAnimation.state.SetEmptyAnimations(0);
            spineAnimationState.ClearTracks();
            var randTrack = Random.Range(1, 3);
            var track = skeletonAnimation.state.SetAnimation(1, fallBackwardAnim, false);
        }

        public void PlayDieForwards()
        {
            skeletonAnimation.state.SetEmptyAnimations(0);
            spineAnimationState.ClearTracks();
            var track = skeletonAnimation.state.SetAnimation(1, fallForwardAnim, false);
        }

        public void DropHelmet()
        {
            skeleton.SetAttachment("helmet", null);
            Debug.Log("Weapon: " + (helmet == null) + ", transform: " + (transform == null) + ", armsbone: " + (headBone == null));
            Transform dropHelmet = Instantiate(helmet, new UnityEngine.Vector3((skeletonAnimation.transform.position.x + headBone.WorldX), skeletonAnimation.transform.position.y + headBone.WorldY, 0f), UnityEngine.Quaternion.Euler(0f, 0f, 0f)) as Transform;

            var rb = dropHelmet.GetComponent<Rigidbody2D>();
            if (rb != null) rb.linearVelocity = skeletonAnimation.transform.parent.GetComponent<Rigidbody2D>().linearVelocity;

            var sprite = dropHelmet.GetComponent<SpriteRenderer>();
            sprite.flipX = transform.parent.localScale.x < 0;
        }

        public void DropWeapon()
        {
            Debug.Log("Weapon: " + (weapon == null) + ", transform: " + (transform == null) + ", armsbone: " + (armsBone == null));
            Transform dropWeapon = Instantiate(weapon, new UnityEngine.Vector3((skeletonAnimation.transform.position.x + armsBone.WorldX), skeletonAnimation.transform.position.y + armsBone.WorldY, 0f), UnityEngine.Quaternion.Euler(0f, 0f, 0f)) as Transform;

            var rb = dropWeapon.GetComponent<Rigidbody2D>();
            if (rb != null) rb.linearVelocity = skeletonAnimation.transform.parent.GetComponent<Rigidbody2D>().linearVelocity;

            var sprite = dropWeapon.GetComponent<SpriteRenderer>();
            sprite.flipX = transform.parent.localScale.x < 0;
        }

        public void HideParachute()
        {
            skeleton.SetAttachment("parachute", null);
            skeleton.SetAttachment("gunSlot", null);
            skeleton.SetAttachment("grenadeSlot", null);
        }

        public void HideBackpack()
        {
            skeleton.SetAttachment("backpack", null);
        }

        #endregion

        #region Utility
        public float GetRandomPitch(float maxPitchOffset)
        {
            return 1f + Random.Range(-maxPitchOffset, maxPitchOffset);
        }
        #endregion
    }
}

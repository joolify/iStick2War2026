using Spine;
using Spine.Unity;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;


namespace iStick2War
{
    public class ParatrooperInput : MonoBehaviour
    {
        public ParatrooperModel model;
        public ParatrooperView view;
        private SkeletonAnimation skeletonAnimation;

        [SpineEvent] public string shootEventName;
        [SpineEvent] public string grenadeEventName;

        private EventData shootEventData;
        private EventData grenadeEventData;

        public MP40 mp40;
        //public Potatomasher potatomasher;
        private Flippable flippable;

        void Start()
        {
            if (skeletonAnimation == null) skeletonAnimation = view.GetComponentInChildren<SkeletonAnimation>();

            if (model == null) model = GetComponent<ParatrooperModel>();

            if (view == null) view = GetComponentInChildren<ParatrooperView>();

            if (flippable == null) flippable = view.transform.GetComponent<Flippable>();

            shootEventData = skeletonAnimation.Skeleton.Data.FindEvent(shootEventName);
            grenadeEventData = skeletonAnimation.Skeleton.Data.FindEvent(grenadeEventName);

            skeletonAnimation.AnimationState.Event += HandleEvent;

            GetComponentInChildren<MeshRenderer>().sortingOrder = Random.Range(1, 32767);

            model.Deploy();
        }

        protected virtual void HandleEvent(Spine.TrackEntry trackEntry, Spine.Event e)
        {
            Debug.Log("ParatrooperInput.HandleEvent");
            //TODO Add footstep event in Spine
            if (e.Data == shootEventData)
            {
                Debug.Log("ParatrooperInput.shootEventData");
                if (!model.isDead)
                    mp40.StartShoot(Vector2.zero);
            }

            if (e.Data == grenadeEventData)
            {
                //if (!model.isDead)
                //    potatomasher.StartShoot(Vector2.zero);
            }
        }

        void OnCollisionEnter2D(Collision2D col)
        {
            Debug.Log("COLLISION with: " + col.gameObject.name);

            if (col.gameObject.TryGetComponent<StickmanBodypart>(out var hitbox))
            {
                Debug.Log("Hit body part: " + hitbox.bodyPart);

                //HandleHit(hitbox.bodyPart);
            }

            //switch (col.gameObject.name)
            //{
            //    case "Arm-Lower-Front-Hitbox":
            //        model.LandDie();
            //        break;
            //    case "Arm-Lower-Back-Hitbox":
            //        model.LandDie();
            //        break;
            //    case "Arm-Upper-Back-Hitbox":
            //        model.LandDie();
            //        break;
            //    case "Arm-Upper-Front-Hitbox":
            //        model.LandDie();
            //        break;
            //    case "Foot-Back-Hitbox":
            //        model.LandDie();
            //        break;
            //    case "Foot-Front-Hitbox":
            //        model.LandDie();
            //        break;
            //    case "Head-Hitbox":
            //        model.LandDie();
            //        break;
            //    case "Leg-Lower-Back-Hitbox":
            //        model.LandDie();
            //        break;
            //    case "Leg-Lower-Front-Hitbox":
            //        model.LandDie();
            //        break;
            //    case "Leg-Upper-Back-Hitbox":
            //        model.LandDie();
            //        break;
            //    case "Leg-Upper-Front-Hitbox":
            //        model.LandDie();
            //        break;
            //    case "Torso-Hitbox":
            //        model.LandDie();
            //        break;
            //    default:
            //        break;
            //}
            //FIXME
        }

        void HandleHit(BodyPartType part)
        {
            switch (part)
            {
                case BodyPartType.Head:
                    model.LandDie(); // maybe instant kill
                    break;

                case BodyPartType.Torso:
                    model.LandDie();
                    break;

                case BodyPartType.ArmUpperFront:
                case BodyPartType.ArmLowerFront:
                case BodyPartType.ArmUpperBack:
                case BodyPartType.ArmLowerBack:
                    model.LandDie();
                    break;

                case BodyPartType.LegUpperFront:
                case BodyPartType.LegLowerFront:
                case BodyPartType.LegUpperBack:
                case BodyPartType.LegLowerBack:
                    model.LandDie();
                    break;
                case BodyPartType.FootFront:
                case BodyPartType.FootBack:
                    model.LandDie();
                    break;
            }
        }

        void OnTriggerEnter2D(Collider2D other)
        {
            Debug.Log("TRIGGER with: " + other.gameObject.name);

            Debug.Log("ParatrooperInput: " + other.gameObject.tag);

            if (other.gameObject.CompareTag("LandingPoint") && model.isInAir)
            {
                //TODO Add random time until deploy
                if (model.isDead || model.isOnFire)
                {
                    model.LandDie();
                }
                else
                {
                    model.Land();
                }
            }

            //if (other.gameObject.CompareTag("Explosion"))
            //{
            //    if (!model.isDead)
            //    {
            //        model.Explode();
            //    }
            //}
            //FIXME
        }
    }
}

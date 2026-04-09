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

        //public MP40 mp40;
        //public Potatomasher potatomasher;
        private Flippable flippable;

        void Start()
        {
            if (skeletonAnimation == null) skeletonAnimation = view.GetComponentInChildren<SkeletonAnimation>();

            if (model == null) model = GetComponent<ParatrooperModel>();

            if (view == null) view = GetComponentInChildren<ParatrooperView>();

            if (flippable == null) flippable = view.transform.GetComponent<Flippable>();

            skeletonAnimation.AnimationState.Event += HandleEvent;

            GetComponentInChildren<MeshRenderer>().sortingOrder = Random.Range(1, 32767);

            model.Deploy();
        }

        protected virtual void HandleEvent(Spine.TrackEntry trackEntry, Spine.Event e)
        {
            //TODO Add footstep event in Spine
            if (e.Data == shootEventData)
            {
                //if (!model.isDead)
                //    mp40.StartShoot(Vector2.zero);
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

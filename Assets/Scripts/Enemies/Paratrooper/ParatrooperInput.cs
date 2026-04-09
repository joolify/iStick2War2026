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
        public EventDataReferenceAsset shootEvent;
        public EventDataReferenceAsset grenadeEvent;
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
            if (e.Data == shootEvent.EventData)
            {
                //if (!model.isDead)
                //    mp40.StartShoot(Vector2.zero);
            }

            if (e.Data == grenadeEvent.EventData)
            {
                //if (!model.isDead)
                //    potatomasher.StartShoot(Vector2.zero);
            }
        }

        void OnTriggerEnter2D(Collider2D other)
        {
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

            if (other.gameObject.CompareTag("Explosion"))
            {
                if (!model.isDead)
                {
                    model.Explode();
                }
            }
        }
    }
}

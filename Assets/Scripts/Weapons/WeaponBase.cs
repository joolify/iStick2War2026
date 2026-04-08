using Spine;
using Spine.Unity;
using System.Collections.Generic;
using UnityEngine;

namespace iStick2War
{

    [System.Serializable]
    public class WeaponBase : MonoBehaviour
    {
        public Transform RocketPrefab;

        public StickmanGunState gunState;

        public WeaponType weaponType;

        public float Damage = 10f;

        public bool shouldReload = false;

        [SpineBone(dataField: "skeletonAnimation")] public string aimPointBoneName;

        [SpineBone(dataField: "skeletonAnimation")] public string crossHairBoneName;

        protected Bone crossHairBone;

        protected Bone aimPointBone;

        public SkeletonAnimation skeletonAnimation;

        public Camera cam;

        private Dictionary<WeaponType, WeaponBase> _weapons;

        public virtual void Start()
        {
            skeletonAnimation = GetComponent<SkeletonAnimation>();

        }
        public virtual void StartShoot(Vector2 touchPos)
        {
        }

        public virtual void StartReload()
        {
        }

        public virtual void StopShoot()
        {
        }

        public virtual void HitExplodable(Explodable explodable)
        {
        }

        public virtual void HitBodyPart(StickmanBodypart enemyBodyPart)
        {
        }

        public virtual void Effect(Vector3 hitPos, Vector3 hitNormal)
        {
        }

    }
}

using Spine;
using Spine.Unity;
using UnityEngine;

namespace iStick2War
{

    public class WeaponBase : MonoBehaviour
    {
        public Transform RocketPrefab;

        public StickmanGunState gunState;

        public float Damage = 10f;

        public bool shouldReload = false;

        public SkeletonMecanim skeletonMecanim;

        public string crosshairBoneName;
        public Bone crossHairBone;

        public string aimPointBoneName;
        public Bone aimPointBone;

        //*public SkeletonAnimation skeletonAnimation;

        //*[SpineBone(dataField: "skeletonAnimation")]
        //*public string aimPointName;

        //*[SpineBone(dataField: "skeletonAnimation")]
        //*public string crossHairName;
        public Camera cam;

        //*public Bone crossHairBone;
        //*public Bone aimPointBone;


        public virtual void Start()
        {
            skeletonMecanim = GetComponent<SkeletonMecanim>();

            var skeleton = skeletonMecanim.Skeleton;

            //*if (skeletonAnimation == null) skeletonAnimation = //FIXME
            crossHairBone = skeleton.FindBone("crosshair");

            aimPointBone = skeleton.FindBone("gunBone"); //FIXME
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

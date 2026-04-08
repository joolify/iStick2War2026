using Spine;
using Spine.Unity;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace iStick2War
{
    public class Flamethrower : WeaponBase
    {
        //public ParticleSystem flameThrowerPS;

        //public override void Start()
        //{
        //    base.Start();
        //    gunState = StickmanGunState.Flamethrower;

        //    if (skeletonAnimation == null) skeletonAnimation = GetComponent<SkeletonAnimation>();
        //    if (crossHairBone == null) crossHairBone = skeletonAnimation.Skeleton.FindBone(crossHairName);
        //    if (aimPointBone == null) aimPointBone = skeletonAnimation.Skeleton.FindBone(aimPointName);
        //    if (crossHairBone == null) Debug.LogError("Crosshairbone of " + gunState + " is null");
        //    if (aimPointBone == null) Debug.LogError("AimPointBone of " + gunState + " is null");
        //    if (skeletonAnimation == null) Debug.LogError("SkeletonAnimation of " + gunState + " is null");

        //    flameThrowerPS.gameObject.SetActive(false);
        //}

        //public override void StartShoot(Vector2 touchPos)
        //{
        //    Debug.Log("Flamegun.StartShoot()");
        //    UnityEngine.Vector2 firePointPosition = new UnityEngine.Vector2((skeletonAnimation.transform.position.x + aimPointBone.WorldX) * transform.localScale.x, (skeletonAnimation.transform.position.y + aimPointBone.WorldY) * transform.localScale.y);

        //    flameThrowerPS.transform.position = transform.position + new Vector3(firePointPosition.x, firePointPosition.y, 0f);

        //    Vector2 direction = new Vector2(crossHairBone.WorldX, crossHairBone.WorldY) - new Vector2(aimPointBone.WorldX, aimPointBone.WorldY);

        //    direction.Normalize();

        //    var fo = flameThrowerPS.forceOverLifetime;
        //    fo.x = direction.x * 20;
        //    fo.y = direction.y * 20;

        //    flameThrowerPS.gameObject.SetActive(true);
        //    flameThrowerPS.Play();
        //}

        //public override void StopShoot()
        //{
        //    flameThrowerPS.Stop();
        //}
    }
}


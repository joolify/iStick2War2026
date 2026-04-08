using Spine;
using Spine.Unity;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace iStick2War
{
    public class Tesla : GunBase
    {
    //    private ParatrooperModel previousParatrooper;

    //    public LightningBolt2D lightningBolt2D;

    //    public override void Start()
    //    {
    //        base.Start();
    //        gunState = StickmanGunState.Tesla;

    //        if (lightningBolt2D != null) lightningBolt2D.gameObject.SetActive(false);
    //        if (crossHairBone == null) Debug.LogError("Crosshairbone of " + gunState + " is null");
    //        if (aimPointBone == null) Debug.LogError("AimPointBone of " + gunState + " is null");
    //        if (skeletonAnimation == null) Debug.LogError("SkeletonAnimation of " + gunState + " is null");
    //    }

    //    public override void StartShoot(Vector2 touchPos)
    //    {
    //        UnityEngine.Vector2 firePointPosition = new UnityEngine.Vector2((skeletonAnimation.transform.position.x + aimPointBone.WorldX) * transform.localScale.x, (skeletonAnimation.transform.position.y + aimPointBone.WorldY) * transform.localScale.y);

    //        Debug.Log("Tesla: " + firePointPosition);

    //        var direction = touchPos - firePointPosition;

    //        Debug.Log("Tesla: " + direction);

    //        RaycastHit2D hit = Physics2D.Raycast(firePointPosition, direction, 100, whatToHit);

    //        Debug.Log("Tesla: " + hit);

    //        Debug.DrawLine(firePointPosition, direction * 100, Color.cyan);

    //        if (hit)
    //        {
    //            var currentParatrooper = hit.collider.GetComponentInParent<ParatrooperModel>();
    //            Debug.Log(DateTime.Now + " Tesla.Paratrooper 1: " + previousParatrooper?.isElectrocuted);
    //            Debug.Log(DateTime.Now + " Tesla.Paratrooper 1: " + currentParatrooper?.isElectrocuted);

    //            if (!ReferenceEquals(currentParatrooper, previousParatrooper))
    //            {
    //                Debug.Log(DateTime.Now + " Tesla.Paratrooper 2: ");
    //                if (previousParatrooper != null)
    //                {
    //                    Debug.Log(DateTime.Now + " Tesla.Paratrooper 2.1: ");
    //                    previousParatrooper.StopElectrocute();
    //                }
    //            }

    //            if (currentParatrooper != null)
    //            {
    //                Debug.Log(DateTime.Now + " Tesla.Paratrooper 3: " + previousParatrooper?.isElectrocuted);
    //                Debug.Log(DateTime.Now + " Tesla.Paratrooper 3: " + currentParatrooper?.isElectrocuted);
    //                currentParatrooper.StartElectrocute();
    //                previousParatrooper = currentParatrooper;
    //            }
    //        }
    //        else if (previousParatrooper != null)
    //        {
    //            Debug.Log(DateTime.Now + " Tesla.Paratrooper 5: " + previousParatrooper.isElectrocuted);
    //            // not anymore.
    //            previousParatrooper.StopElectrocute();
    //            previousParatrooper = null;
    //        }

    //        Vector3 hitPos;
    //        Vector3 hitNormal;

    //        if (hit.collider == null)
    //        {
    //            hitPos = touchPos;
    //            hitNormal = new Vector3(9999, 9999, 9999);
    //        }
    //        else
    //        {
    //            hitPos = hit.point;
    //            hitNormal = hit.normal;
    //        }

    //        Effect(hitPos, hitNormal);
    //    }

    //    public override void StopShoot()
    //    {
    //        Debug.Log(DateTime.Now + " Tesla.Paratrooper 6: " + previousParatrooper?.isElectrocuted);
    //        lightningBolt2D.gameObject.SetActive(false);
    //        if (previousParatrooper != null) previousParatrooper.StopElectrocute();
    //    }

    //    public override void Effect(Vector3 hitPos, Vector3 hitNormal)
    //    {
    //        lightningBolt2D.gameObject.SetActive(true);
    //        lightningBolt2D.enabled = true;
    //        lightningBolt2D.isPlaying = true;
    //        UnityEngine.Vector2 firePointPosition = new UnityEngine.Vector2((skeletonAnimation.transform.position.x + aimPointBone.WorldX) * transform.localScale.x, (skeletonAnimation.transform.position.y + aimPointBone.WorldY) * transform.localScale.y);

    //        Debug.Log("lightningBolt2D == null: " + lightningBolt2D == null);

    //        lightningBolt2D.startPoint.x = firePointPosition.x;
    //        lightningBolt2D.startPoint.y = firePointPosition.y;

    //        lightningBolt2D.endPoint.x = hitPos.x;
    //        lightningBolt2D.endPoint.y = hitPos.y;
    //    }
    }
}


using Spine.Unity;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace iStick2War
{
    public class GunBase : WeaponBase
    {
        public LayerMask whatToHit;

        public float offSetX = 0f;
        public float offSetY = 0f;
        public float offSetZ = 90f;

        public Transform TrailPrefab;
        public Transform[] BloodPrefabs;
        public List<string> Muzzles;

        public float timeToSpawnEffect = 0;
        public float effectSpawnRate = 10;

        public int maxAmmo = 10;
        private int currentAmmo;

        public Muzzle muzzle;

        public override void Start()
        {
            if (skeletonAnimation == null) skeletonAnimation = GetComponent<SkeletonAnimation>();
            if (skeletonAnimation == null) Debug.LogError("SkeletonAnimation of " + gunState + " is null");

            if (!string.IsNullOrEmpty(aimPointBoneName))
            {
                aimPointBone = skeletonAnimation.Skeleton.FindBone(aimPointBoneName);
            }

            if (aimPointBone == null)
            {
                Debug.LogError("Aim bone not found!");
            }

            if (!string.IsNullOrEmpty(crossHairBoneName))
            {
                crossHairBone = skeletonAnimation.Skeleton.FindBone(crossHairBoneName);
            }

            if (crossHairBone == null)
            {
                Debug.LogError("Cross hair bone not found!");
            }


            Debug.Log("Gunstate: " + gunState);
            currentAmmo = maxAmmo;
        }

        public override void StartShoot(Vector2 touchPos)
        {
            if (currentAmmo <= 0)
            {
                shouldReload = true;
                return;
            }
            else
            {
                currentAmmo--;
            }

            if (aimPointBone == null)
            {
                Debug.LogError($"{name}: {nameof(aimPointBone)} is NULL");
                return;
            }

            if (crossHairBone == null)
            {
                Debug.LogError($"{name}: {nameof(crossHairBone)} is NULL");
                return;
            }

            //UnityEngine.Vector2 firePointPosition = new UnityEngine.Vector2((skeletonAnimation.transform.position.x + aimPointBone.WorldX) * transform.localScale.x, (skeletonAnimation.transform.position.y + aimPointBone.WorldY) * transform.localScale.y);

            Vector3 aimPos = skeletonAnimation.transform.TransformPoint(new Vector3(aimPointBone.WorldX, aimPointBone.WorldY, 0));
            Vector3 crossPos = skeletonAnimation.transform.TransformPoint(new Vector3(crossHairBone.WorldX, crossHairBone.WorldY, 0));

            //var direction = touchPos - firePointPosition;

            Vector3 direction = (crossPos - aimPos).normalized;

            Muzzle(direction);

            //RaycastHit2D hit = Physics2D.Raycast(firePointPosition, direction, 100, whatToHit);

            RaycastHit2D hit = Physics2D.Raycast(aimPos, direction, 100f, whatToHit);

            //Debug.DrawLine(firePointPosition, (direction) * 100, Color.cyan);
            Debug.Log("GunBase.StartShoot1");
            if (hit.collider != null)
            {
                Debug.Log("GunBase.StartShoot2");
                Debug.DrawLine(aimPos, hit.point, Color.red);

                var enemyBodyPart = hit.collider.GetComponent<StickmanBodypart>();

                Debug.Log("GunBase.StartShoot3: " + (enemyBodyPart == null));
                if (enemyBodyPart != null)
                {
                    Debug.Log("GunBase.StartShoot4: " + (enemyBodyPart == null));
                    HitBodyPart(enemyBodyPart);
                }

                var explodable = hit.collider.GetComponent<Explodable>();

                if (explodable != null)
                {
                    Debug.Log("GunBase.StartShoot5");
                    HitExplodable(explodable);
                }
            }
            //FIXME

            Vector3 hitPos;
            Vector3 hitNormal;

            if (hit.collider == null)
            {
                hitPos = (direction) * 9999;
                hitNormal = new Vector3(9999, 9999, 9999);
            }
            else
            {
                hitPos = hit.point;
                hitNormal = hit.normal;
            }

            Vector3 finalPos = hit.collider != null ? (Vector3)hit.point : aimPos + direction * 100f;

            //Effect(hitPos, hitNormal);

            Effect(finalPos, hit.collider != null ? hit.normal : Vector3.zero);
        }

        public override void HitExplodable(Explodable explodable)
        {
            explodable.TakeDamage(Damage);

            //TODO Add hit particles
        }

        public override void HitBodyPart(StickmanBodypart enemyBodyPart)
        {
            if (enemyBodyPart == null) return;
            Debug.Log("GunBase.HitBodyPart");
            enemyBodyPart.TakeDamage(Damage);

            //var bloodPrefab = BloodPrefabs[UnityEngine.Random.Range(0, BloodPrefabs.Length)];
            //var bodyPartBone = enemyBodyPart.bloodBone;
            //bloodPrefab.GetComponent<SpriteRenderer>().sortingOrder = enemyBodyPart.transform.parent.GetComponent<MeshRenderer>().sortingOrder + 1;

            //if (bodyPartBone == null) return;
            //Debug.Log("HitBodyPart enemyBodyPart: " + enemyBodyPart == null);
            //Debug.Log("HitBodyPart bodyPartBone: " + bodyPartBone == null);
            //Transform hitParticle = Instantiate(bloodPrefab, new UnityEngine.Vector3(enemyBodyPart.transform.position.x + bodyPartBone.WorldX, enemyBodyPart.transform.position.y + bodyPartBone.WorldY, 0f), UnityEngine.Quaternion.Euler(0f, 0f, 0f)) as Transform;
            //Destroy(hitParticle.gameObject, 1f);

            //FIXME

        }

        //public override void Effect(Vector3 hitPos, Vector3 hitNormal)
        //{
        //    Debug.Log("Effect0: ");
        //    // using mousePosition and player's transform (on orthographic camera view)
        //    //UnityEngine.Vector2 firePointPosition = new UnityEngine.Vector2((skeletonAnimation.transform.position.x + aimPointBone.WorldX) * transform.localScale.x, (skeletonAnimation.transform.position.y + aimPointBone.WorldY) * transform.localScale.y);

        //    Vector3 firePointPosition = skeletonAnimation.transform.TransformPoint(new Vector3(aimPointBone.WorldX, aimPointBone.WorldY, 0));

        //    Debug.DrawLine(firePointPosition, firePointPosition + Vector3.up * 0.2f, Color.red, 0.1f);
        //    Debug.DrawLine(firePointPosition, firePointPosition + Vector3.right * 0.2f, Color.red, 0.1f);

        //    var aimPos = skeletonAnimation.transform.TransformPoint(new Vector3(aimPointBone.WorldX, aimPointBone.WorldY, 0));

        //    var crossPos = skeletonAnimation.transform.TransformPoint(
        //        new Vector3(crossHairBone.WorldX, crossHairBone.WorldY, 0)
        //    );

        //    Debug.DrawLine(aimPos, aimPos + Vector3.up * 0.2f, Color.red, 0.1f);
        //    Debug.DrawLine(crossPos, crossPos + Vector3.up * 0.2f, Color.green, 0.1f);

        //    Debug.Log("Effect1: " + firePointPosition);
        //    Transform trail = Instantiate(TrailPrefab, firePointPosition, UnityEngine.Quaternion.Euler(aimPointBone.WorldRotationX, aimPointBone.WorldRotationY, 0f));
        //    Debug.Log("Effect2: " + trail);
        //    LineRenderer lr = trail.GetComponent<LineRenderer>();

        //    lr.useWorldSpace = true;

        //    Debug.Log("Effect3: " + lr);
        //    if (lr != null)
        //    {
        //        lr.SetPosition(0, firePointPosition);
        //        //lr.SetPosition(1, hitPos);
        //        lr.SetPosition(1, crossPos);
        //        Debug.Log("Effect4: " + lr.GetPosition(0));
        //        Debug.Log("Effect5: " + lr.GetPosition(1));
        //    }

        //    Destroy(trail.gameObject, 0.04f);

        //    if (hitNormal != new UnityEngine.Vector3(9999, 9999, 9999))
        //    {
        //        //TODO FIXME
        //        //var bloodPrefab = BloodPrefabs[UnityEngine.Random.Range(0, BloodPrefabs.Length - 1)];
        //        //Transform hitParticle = Instantiate(bloodPrefab, new UnityEngine.Vector3(transform.position.x + bloodHeadBone.WorldX, transform.position.y +bloodHeadBone.WorldY, 0f), UnityEngine.Quaternion.Euler(bloodHeadBone.WorldToLocalRotationX, bloodHeadBone.WorldToLocalRotationY, 0f)) as Transform;
        //        //Destroy(hitParticle.gameObject, 1f);
        //    }
        //}

        public override void Effect(Vector3 finalPos, Vector3 hitNormal)
        {
            Vector3 aimPos = skeletonAnimation.transform.TransformPoint(
                new Vector3(aimPointBone.WorldX, aimPointBone.WorldY, 0)
            );

            Transform trail = Instantiate(TrailPrefab, aimPos, Quaternion.identity);

            LineRenderer lr = trail.GetComponent<LineRenderer>();

            lr.positionCount = 0;   // clear prefab state
            lr.positionCount = 2;

            if (lr != null)
            {
                lr.useWorldSpace = true;
                lr.SetPosition(0, aimPos);
                lr.SetPosition(1, finalPos);
            }

            //FIXME At 100 waves = 💀 performance issues later
            Destroy(trail.gameObject, 0.04f);
        }

        public override void StartReload()
        {
            currentAmmo = maxAmmo;
            shouldReload = false;
        }

        private void Muzzle(Vector2 direction)
        {
            //float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            //var randMuzzle = Random.Range(0, muzzle.muzzles.Length);
            //var muzzleInst = muzzle.muzzles[randMuzzle];
            //var muzzleParticle = Instantiate(muzzleInst, new UnityEngine.Vector3(skeletonAnimation.transform.position.x + aimPointBone.WorldX, skeletonAnimation.transform.position.y + aimPointBone.WorldY, 0f), UnityEngine.Quaternion.Euler(0f, 0f, angle));
            //FIXME Muzzle

        }
    }
}

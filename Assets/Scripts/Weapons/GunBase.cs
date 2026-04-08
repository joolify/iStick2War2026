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
            if (crossHairBone == null) crossHairBone = skeletonAnimation.Skeleton.FindBone("crosshair");
            if (aimPointBone == null) aimPointBone = skeletonAnimation.Skeleton.FindBone("gunBone"); //FIXME
            if (crossHairBone == null) Debug.LogError("Crosshairbone of " + gunState + " is null");
            if (aimPointBone == null) Debug.LogError("AimPointBone of " + gunState + " is null");
            if (skeletonAnimation == null) Debug.LogError("SkeletonAnimation of " + gunState + " is null");

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

            UnityEngine.Vector2 firePointPosition = new UnityEngine.Vector2((skeletonMecanim.transform.position.x + aimPointBone.WorldX) * transform.localScale.x, (skeletonMecanim.transform.position.y + aimPointBone.WorldY) * transform.localScale.y);

            var direction = touchPos - firePointPosition;

            Muzzle(direction);

            RaycastHit2D hit = Physics2D.Raycast(firePointPosition, direction, 100, whatToHit);

            Debug.DrawLine(firePointPosition, (direction) * 100, Color.cyan);
            if (hit.collider != null)
            {
                Debug.DrawLine(firePointPosition, hit.point, Color.red);

                var enemyBodyPart = hit.collider.GetComponent<StickmanBodypart>();

                if (enemyBodyPart != null)
                {
                    HitBodyPart(enemyBodyPart);
                }

                var explodable = hit.collider.GetComponent<Explodable>();

                if (explodable != null)
                {
                    HitExplodable(explodable);
                }
            }

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

            Effect(hitPos, hitNormal);
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

            var bloodPrefab = BloodPrefabs[UnityEngine.Random.Range(0, BloodPrefabs.Length)];
            var bodyPartBone = enemyBodyPart.bloodBone;
            bloodPrefab.GetComponent<SpriteRenderer>().sortingOrder = enemyBodyPart.transform.parent.GetComponent<MeshRenderer>().sortingOrder + 1;

            if (bodyPartBone == null) return;
            Debug.Log("HitBodyPart enemyBodyPart: " + enemyBodyPart == null);
            Debug.Log("HitBodyPart bodyPartBone: " + bodyPartBone == null);
            Transform hitParticle = Instantiate(bloodPrefab, new UnityEngine.Vector3(enemyBodyPart.transform.position.x + bodyPartBone.WorldX, enemyBodyPart.transform.position.y + bodyPartBone.WorldY, 0f), UnityEngine.Quaternion.Euler(0f, 0f, 0f)) as Transform;
            Destroy(hitParticle.gameObject, 1f);

        }

        public override void Effect(Vector3 hitPos, Vector3 hitNormal)
        {
            // using mousePosition and player's transform (on orthographic camera view)
            UnityEngine.Vector2 firePointPosition = new UnityEngine.Vector2((skeletonMecanim.transform.position.x + aimPointBone.WorldX) * transform.localScale.x, (skeletonMecanim.transform.position.y + aimPointBone.WorldY) * transform.localScale.y);

            Transform trail = Instantiate(TrailPrefab, firePointPosition, UnityEngine.Quaternion.Euler(aimPointBone.WorldRotationX, aimPointBone.WorldRotationY, 0f));
            LineRenderer lr = trail.GetComponent<LineRenderer>();

            if (lr != null)
            {
                lr.SetPosition(0, firePointPosition);
                lr.SetPosition(1, hitPos);
            }

            Destroy(trail.gameObject, 0.04f);

            if (hitNormal != new UnityEngine.Vector3(9999, 9999, 9999))
            {
                //TODO FIXME
                //var bloodPrefab = BloodPrefabs[UnityEngine.Random.Range(0, BloodPrefabs.Length - 1)];
                //Transform hitParticle = Instantiate(bloodPrefab, new UnityEngine.Vector3(transform.position.x + bloodHeadBone.WorldX, transform.position.y +bloodHeadBone.WorldY, 0f), UnityEngine.Quaternion.Euler(bloodHeadBone.WorldToLocalRotationX, bloodHeadBone.WorldToLocalRotationY, 0f)) as Transform;
                //Destroy(hitParticle.gameObject, 1f);
            }
        }

        public override void StartReload()
        {
            currentAmmo = maxAmmo;
            shouldReload = false;
        }

        private void Muzzle(Vector2 direction)
        {
            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            var randMuzzle = Random.Range(0, muzzle.muzzles.Length);
            var muzzleInst = muzzle.muzzles[randMuzzle];
            var muzzleParticle = Instantiate(muzzleInst, new UnityEngine.Vector3(skeletonMecanim.transform.position.x + aimPointBone.WorldX, skeletonMecanim.transform.position.y + aimPointBone.WorldY, 0f), UnityEngine.Quaternion.Euler(0f, 0f, angle));
            Destroy(muzzleParticle.gameObject, 0.1f);
        }
    }
}

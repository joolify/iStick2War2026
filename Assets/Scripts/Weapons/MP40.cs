using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace iStick2War
{
    public class MP40 : SMG
    {
        private Transform target;

        public override void Start()
        {
            base.Start();

            Debug.Log("MP40 Start, enabled: " + enabled);

            gunState = StickmanGunState.MP40;
        }

        private void Awake()
        {
            target = GameObject.FindGameObjectWithTag("Player").transform;
            Debug.Log("MP40 Awake, enabled: " + enabled);
        }

        void OnEnable()
        {
            Debug.Log("MP40 ENABLED");
        }

        void OnDisable()
        {
            Debug.Log("MP40 DISABLED");
        }

        public override void StartShoot(Vector2 touchPos)
        {
            Debug.Log("MP40.Shoot");
            Debug.Log("skeletonAnimation: " + (skeletonAnimation == null));
            Debug.Log("aimPointBone: " + (aimPointBone == null));
            Vector3 aimPos = skeletonAnimation.transform.TransformPoint(new Vector3(aimPointBone.WorldX, aimPointBone.WorldY, 0));
            //Vector2 firePointPosition = new Vector2((skeletonAnimation.transform.position.x + aimPointBone.WorldX), (skeletonAnimation.transform.position.y + aimPointBone.WorldY) * skeletonAnimation.skeleton.ScaleY);
            Vector2 firePointPosition = aimPos;
            //Add randomness to AI bots
            var shootingRangeY = UnityEngine.Random.Range(0f, 1f);
            var shootingUp = UnityEngine.Random.value > 0.5f;
            Vector2 playerPosition = new Vector2(target.position.x, firePointPosition.y + (shootingUp ? shootingRangeY : -shootingRangeY));

            //var direction = playerPosition - firePointPosition;
            var direction = (playerPosition - firePointPosition).normalized;

            Muzzle(direction);

            RaycastHit2D hit = Physics2D.Raycast(firePointPosition, direction, 100, whatToHit);

            //Debug.DrawLine(firePointPosition, direction * 100, Color.cyan);
            //Debug.DrawLine(firePointPosition, firePointPosition + direction * 100, Color.cyan);
#if UNITY_EDITOR
            //Debug.DrawLine(firePointPosition, firePointPosition + direction.normalized * 100, Color.cyan);
            Debug.DrawLine(aimPos, aimPos + (Vector3)(direction.normalized * 100f), Color.cyan
);
#endif
            Debug.Log("StickmanAutoShoot: hit.collider:" + hit.collider);
            if (hit.collider != null)
            {
                Debug.Log("StickmanAutoShoot: hit.collider != null");
                Debug.DrawLine(firePointPosition, hit.point, Color.red);

                var bodyPart = hit.collider.GetComponent<StickmanBodypart>();

                Debug.Log("StickmanAutoShoot: BodyPart: " + (bodyPart == null));

                if (bodyPart != null)
                {
                    Debug.Log("StickmanAutoShoot: BodyPart != null");
                    bodyPart.TakeDamage(Damage);
                }

                //var bunker = hit.collider.GetComponent<BunkerController>();

                //Debug.Log("StickmanAutoShoot: Bunker: " + bunker == null);
                //if (bunker != null)
                //{
                //    Debug.Log("StickmanAutoShoot: Bunker");
                //    bunker.Damage(Damage);
                //}
                //FIXME
            }

            if (Time.time >= timeToSpawnEffect)
            {
                Vector3 hitPos;
                Vector3 hitNormal;

                if (hit.collider == null)
                {
                    //hitPos = direction * 9999;
                    hitPos = firePointPosition + direction.normalized * 100f;
                    hitNormal = new Vector3(9999, 9999, 9999);
                }
                else
                {

                    hitPos = hit.point;
                    hitNormal = hit.normal;
                }

                Effect(hitPos, hitNormal);
                timeToSpawnEffect = Time.time + 1 / effectSpawnRate;
            }
        }

        private void Muzzle(Vector2 direction)
        {
            //float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            //var randMuzzle = UnityEngine.Random.Range(0, muzzle.muzzles.Length);
            //var muzzleInst = muzzle.muzzles[randMuzzle];
            //var muzzleParticle = Instantiate(muzzleInst, new UnityEngine.Vector3(skeletonAnimation.transform.position.x + aimPointBone.WorldX, skeletonAnimation.transform.position.y + aimPointBone.WorldY, 0f), UnityEngine.Quaternion.Euler(0f, 0f, angle));
            //Destroy(muzzleParticle.gameObject, 0.1f);
            //FIXME
        }
    }
}

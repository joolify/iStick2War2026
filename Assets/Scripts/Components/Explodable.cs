using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace iStick2War
{
    public class Explodable : MonoBehaviour
    {
        public Transform ExplodeGroundPrefab;
        public float ExplodeGroundScale = 1f;
        public Transform ExplodeMidAirPrefab;
        public float ExplodeMidAirScale = 1f;
        public Transform ExplodeSpawnPoint;
        public Transform RadiusPrefab;

        public HealthStats stats;
        public bool IsGrenade;
        public bool IsHuman;

        private Collider2D coll2D;


        void Start()
        {
            stats.Init();
            coll2D = GetComponent<Collider2D>();
            if (ExplodeGroundPrefab == null) Debug.LogError(nameof(ExplodeGroundPrefab) + " is not assigned.");
            if (ExplodeMidAirPrefab == null) Debug.LogError(nameof(ExplodeMidAirPrefab) + " is not assigned.");

            if (IsHuman && IsGrenade) Debug.LogError("Explodable can not be both human and grenade.");

            if(IsGrenade)
            {
                StartCoroutine(ExplodeInTime());
            }
        }

        public void TakeDamage(float damage)
        {
            stats.curHealth -= damage;

            if (stats.curHealth <= 0)
            {
                //TODO Pool boss
                var explodeInstance =  Instantiate(ExplodeMidAirPrefab, new Vector3(ExplodeSpawnPoint.position.x, ExplodeSpawnPoint.position.y, ExplodeSpawnPoint.position.z), Quaternion.Euler(0f, 0f, 0f));
                explodeInstance.transform.localScale = new Vector3(ExplodeMidAirScale, ExplodeMidAirScale, ExplodeMidAirScale);
                Destroy(gameObject);
            }
        }

        IEnumerator ExplodeInTime()
        {
            var randSeconds = Random.Range(4, 5);
            yield return new WaitForSeconds(randSeconds);

            var explodeInstance = Instantiate(ExplodeGroundPrefab, new Vector3(ExplodeSpawnPoint.position.x, ExplodeSpawnPoint.position.y, ExplodeSpawnPoint.position.z), Quaternion.Euler(0f, 0f, 0f));
            var radiusInstance = Instantiate(RadiusPrefab, new Vector3(ExplodeSpawnPoint.position.x, ExplodeSpawnPoint.position.y, ExplodeSpawnPoint.position.z), Quaternion.Euler(0f, 0f, 0f));
            explodeInstance.transform.localScale = new Vector3(ExplodeGroundScale, ExplodeGroundScale, ExplodeGroundScale);
            Destroy(gameObject);
            Destroy(explodeInstance.gameObject, 1f);
            Destroy(radiusInstance.gameObject, 0.1f);
        }

        void OnTriggerEnter2D(Collider2D other)
        {
            Debug.Log("Hit: " + other.gameObject.tag);

            if (other.gameObject.CompareTag("BombPoint") && !IsGrenade && !IsHuman)
            {
                //TODO Pool boss
                var explodeInstance = Instantiate(ExplodeGroundPrefab, new Vector3(ExplodeSpawnPoint.position.x, ExplodeSpawnPoint.position.y, ExplodeSpawnPoint.position.z), Quaternion.Euler(0f, 0f, 0f));
                explodeInstance.transform.localScale = new Vector3(ExplodeGroundScale, ExplodeGroundScale, ExplodeGroundScale);
                Destroy(gameObject);
            }
            if (other.gameObject.CompareTag("BombPoint") && IsGrenade)
            {
                Physics2D.IgnoreCollision(coll2D, other);
            }
        }
    }
}

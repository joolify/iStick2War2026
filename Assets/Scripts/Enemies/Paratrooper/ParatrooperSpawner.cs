using Spine.Unity;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace iStick2War
{
    public class ParatrooperSpawner : MonoBehaviour
    {
        public Transform ParatrooperPrefab;
        public Transform ParatrooperSpawnPoint;

        private Transformable transformable;
        private Flippable flippable;

        void Awake()
        {
            transformable = GetComponent<Transformable>();
            flippable = GetComponent<Flippable>();
        }

        void OnTriggerEnter2D(Collider2D other)
        {

            if (other.gameObject.CompareTag("RightParatroopersPoint") && !flippable.facingRight && !transformable.translateRight)
            {
                StartCoroutine(SpawnEnemies(5, 0.5f));
                //TODO Add random time until deploy
            }

            if (other.gameObject.CompareTag("LeftParatroopersPoint") && flippable.facingRight && transformable.translateRight)
            {
                StartCoroutine(SpawnEnemies(5, 0.5f));
                //TODO Add random time until deploy
            }
        }

        IEnumerator SpawnEnemies(int count, float delay)
        {
            for (int i = 0; i < count; i++)
            {
                Debug.Log("SpawnEnemies");
                var paratrooper = Instantiate(ParatrooperPrefab, ParatrooperSpawnPoint.position, ParatrooperSpawnPoint.rotation);
                paratrooper.GetComponentInChildren<MeshRenderer>().sortingOrder = ParatrooperView.ParatroopSortingCount;

                var stickmanAnim = paratrooper.GetComponentInChildren<ParatrooperView>();

                var paraTrooperFlippable = paratrooper.GetComponentInChildren<Flippable>();
                if (paraTrooperFlippable != null)
                {
                    Debug.Log("Helicopter right: " + flippable.facingRight);
                    Debug.Log("Paratrooper right: " + paraTrooperFlippable.facingRight);
                    if ((!flippable.facingRight && paraTrooperFlippable.facingRight) || (flippable.facingRight && !paraTrooperFlippable.facingRight))
                    {
                        paraTrooperFlippable.Flip(stickmanAnim.skeleton);
                    }
                }

                yield return new WaitForSeconds(delay);
            }
        }
    }
}

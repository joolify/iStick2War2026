using UnityEngine;

namespace iStick2War
{
    [RequireComponent(typeof(Flippable))]
    public class Transformable : MonoBehaviour
    {
        #region Inspector
        [Header("Speed")]
        public float minSpeed;
        public float maxSpeed;
        public bool translateRight;
        #endregion

        // Update is called once per frame
        void Update()
        {
            var flip = GetComponent<Flippable>();
            var speed = Random.Range(minSpeed, maxSpeed);
            transform.Translate((flip.facingRight && translateRight) ? speed : -1 * speed, 0f, 0f);
        }
    }
}

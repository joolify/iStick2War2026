using Spine;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

    public class Flippable : MonoBehaviour
    {
        public bool facingRight = true;

        public void Flip(Skeleton skeleton)
        {
            skeleton.ScaleX *= -1;
            facingRight = !facingRight;
        }

        public void Flip(Transform transform)
        {
            transform.localScale *= -1;
            facingRight = !facingRight;
        }
    }

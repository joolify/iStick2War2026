using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Assets.Scripts.Helpers
{
    public class DebugVisualization : MonoBehaviour
    {
        public float x;

        public float y;

        void OnDrawGizmos()
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireCube(transform.position, new Vector3(x, y, 0));
        }
    }
}

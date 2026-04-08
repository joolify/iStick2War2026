using UnityEngine;

namespace iStick2War
{
    [System.Serializable]
    public class HealthStats
    {
        public float maxHealth = 100f;

        private float _curHealth;
        public float curHealth
        {
            get { return _curHealth; }
            set { _curHealth = UnityEngine.Mathf.Clamp(value, 0, maxHealth); }
        }

        public void Init()
        {
            curHealth = maxHealth;
        }
    }
}

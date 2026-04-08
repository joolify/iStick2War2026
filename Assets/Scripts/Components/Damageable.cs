using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace iStick2War
{
    public class Damageable : MonoBehaviour
    {
        public HealthStats stats = new HealthStats();
        void Start()
        {
            stats.Init();
        }

        public void TakeDamage(float damage)
        {
            Debug.Log(gameObject.name + " got hit " + stats.curHealth);
            stats.curHealth -= damage;

            if (stats.curHealth <= 0)
            {
                //TODO Move to Pool boss
                //GameMaster.KillEnemy(this);
            }
        }


    }
}

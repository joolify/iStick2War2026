using UnityEngine;
using iStick2War;

namespace Assets.Scripts.Components
{
    public struct DamageInfo
    {
        public float BaseDamage;
        public BodyPartType BodyPart;
        public Vector2 HitPoint;
        public bool IsExplosive;
        public float ExplosionForce;
        /// <summary>Weapon that dealt the hit (for Tesla/flamethrower reactions).</summary>
        public WeaponType SourceWeapon;
    }
}

using UnityEngine;
using iStick2War;

namespace Assets.Scripts.Components
{
    public struct DamageInfo
    {
        public float BaseDamage;
        public BodyPartType BodyPart;
        public Vector2 HitPoint;
        /// <summary>World-space direction the damage traveled (into the target). Zero if unknown.</summary>
        public Vector2 ShotDirection;
        public bool IsExplosive;
        public float ExplosionForce;
        /// <summary>Weapon that dealt the hit (for Tesla/flamethrower reactions).</summary>
        public WeaponType SourceWeapon;
    }
}

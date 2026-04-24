using iStick2War;
using System;
using System.Collections.Generic;
using System.Text;
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
        /// <summary>Weapon that dealt the hit (for Tesla electrocute visuals, etc.). <see cref="WeaponType.None"/> when unknown.</summary>
        public WeaponType SourceWeapon;
    }
}

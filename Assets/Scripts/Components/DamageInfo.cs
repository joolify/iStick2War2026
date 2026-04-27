using iStick2War;
using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Assets.Scripts.Components
{
    public struct DamageInfo
    {
        public float BaseDamage;
        public BodyPartType BodyPart;
        public Vector2 HitPoint;
        public bool IsExplosive;
        public float ExplosionForce;
        public WeaponType WeaponType;
    }
}

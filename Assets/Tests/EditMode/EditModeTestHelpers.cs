using System.Reflection;
using iStick2War;
using iStick2War_V2;
using UnityEngine;

namespace iStick2War.Tests.EditMode
{
    /// <summary>
    /// Helpers for Edit Mode tests (production assets use private SerializeField).
    /// </summary>
    internal static class EditModeTestHelpers
    {
        public static HeroWeaponDefinition_V2 CreateWeaponDefinition(WeaponType weaponType)
        {
            var def = ScriptableObject.CreateInstance<HeroWeaponDefinition_V2>();
            SetPrivateField(def, "_weaponType", weaponType);
            return def;
        }

        public static void SetPrivateField<T>(object target, string fieldName, T value)
        {
            FieldInfo field = GetFieldOrThrow(target, fieldName);
            field.SetValue(target, value);
        }

        public static T GetPrivateField<T>(object target, string fieldName)
        {
            FieldInfo field = GetFieldOrThrow(target, fieldName);
            object value = field.GetValue(target);
            if (value is T typed)
            {
                return typed;
            }

            if (value == null && default(T) == null)
            {
                return default;
            }

            return (T)System.Convert.ChangeType(value, typeof(T));
        }

        private static FieldInfo GetFieldOrThrow(object target, string fieldName)
        {
            FieldInfo field = target.GetType().GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            if (field == null)
            {
                throw new System.InvalidOperationException(
                    $"Field '{fieldName}' not found on {target.GetType().Name}");
            }

            return field;
        }
    }
}

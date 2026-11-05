using HarmonyLib;
using System;
using System.Reflection;

namespace DeftHands.Utils
{
    internal static class ReflectionUtils
    {
        /// <summary>
        /// Finds a type by its full name across every assembly loaded in the app domain.
        /// </summary>
        /// <returns>The type, or null if no loaded assembly defines it.</returns>
        public static Type FindType(string fullName)
        {
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type type;
                try
                {
                    type = assembly.GetType(fullName);
                }
                catch
                {
                    continue;
                }

                if (type != null)
                    return type;
            }

            return null;
        }

        /// <summary>
        /// Returns an accessor for the instance field <paramref name="name"/> of <typeparamref name="TOwner"/>, or null
        /// if it has no field of that name and type.
        /// </summary>
        public static AccessTools.FieldRef<TOwner, TValue> FindFieldRef<TOwner, TValue>(string name)
        {
            FieldInfo field = AccessTools.Field(typeof(TOwner), name);
            return field != null && field.FieldType == typeof(TValue) ? AccessTools.FieldRefAccess<TOwner, TValue>(field) : null;
        }
    }
}

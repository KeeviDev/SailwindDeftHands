using System;
using System.Reflection;

namespace DeftHands.Utils
{
    internal static class ReflectionUtils
    {
        /// <summary>
        /// Finds a type by its full name across every assembly loaded in the app domain.
        /// Assemblies that can't be inspected are skipped.
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
    }
}

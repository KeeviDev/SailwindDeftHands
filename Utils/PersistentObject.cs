using UnityEngine;

namespace DeftHands.Utils
{
    /// <summary>
    /// Creates the mod's components on objects of their own that stay loaded when scenes change.
    /// </summary>
    internal static class PersistentObject
    {
        /// <summary>
        /// Returns a new <typeparamref name="T"/> on a new object named after it that stays loaded when scenes change.
        /// </summary>
        public static T Create<T>() where T : MonoBehaviour
        {
            var obj = new GameObject("DeftHands_" + typeof(T).Name);
            T component = obj.AddComponent<T>();
            Object.DontDestroyOnLoad(obj);
            return component;
        }
    }
}

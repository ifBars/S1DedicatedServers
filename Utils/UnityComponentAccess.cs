using System;
using System.Collections.Generic;
#if IL2CPP
using Il2CppInterop.Runtime;
using RuntimeType = Il2CppSystem.Type;
#else
using RuntimeType = System.Type;
#endif
using UnityEngine;

namespace DedicatedServerMod.Utils
{
    /// <summary>
    /// Provides Unity component lookup helpers that avoid IL2CPP generic cast constraints on game types.
    /// </summary>
    internal static class UnityComponentAccess
    {
        internal static T GetComponent<T>(Component component)
            where T : Component
        {
            if (component == null)
            {
                return null;
            }

#if IL2CPP
            return ConvertObject<T>(component.GetComponent(GetRuntimeType<T>()));
#else
            return component.GetComponent<T>();
#endif
        }

        internal static T GetComponentInParent<T>(Component component, bool includeInactive = false)
            where T : Component
        {
            if (component == null)
            {
                return null;
            }

#if IL2CPP
            return ConvertObject<T>(component.GetComponentInParent(GetRuntimeType<T>(), includeInactive));
#else
            return component.GetComponentInParent<T>(includeInactive);
#endif
        }

        internal static T[] FindObjectsOfType<T>(bool includeInactive = false)
            where T : UnityEngine.Object
        {
#if IL2CPP
            UnityEngine.Object[] objects = UnityEngine.Object.FindObjectsOfType(GetRuntimeType<T>(), includeInactive);
            if (objects == null || objects.Length == 0)
            {
                return Array.Empty<T>();
            }

            List<T> results = new List<T>(objects.Length);
            for (int i = 0; i < objects.Length; i++)
            {
                T converted = ConvertObject<T>(objects[i]);
                if (converted != null)
                {
                    results.Add(converted);
                }
            }

            return results.ToArray();
#else
            return UnityEngine.Object.FindObjectsOfType<T>(includeInactive);
#endif
        }

#if IL2CPP
        private static RuntimeType GetRuntimeType<T>()
            where T : UnityEngine.Object
        {
            return Il2CppType.Of<T>();
        }

        private static T ConvertObject<T>(UnityEngine.Object value)
            where T : UnityEngine.Object
        {
            if (value is Il2CppSystem.Object il2CppObject)
            {
                return il2CppObject.TryCast<T>();
            }

            return value as T;
        }
#else
        private static T ConvertObject<T>(UnityEngine.Object value)
            where T : UnityEngine.Object
        {
            return value as T;
        }
#endif
    }
}

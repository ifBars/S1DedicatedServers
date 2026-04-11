using System;
using System.Reflection;

namespace DedicatedServerMod.Utils
{
    internal static class SafeReflection
    {
        private const BindingFlags InstanceFlags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

        private const BindingFlags StaticFlags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;

        internal static Type FindType(string fullName)
        {
            if (string.IsNullOrWhiteSpace(fullName))
            {
                return null;
            }

            Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (int i = 0; i < assemblies.Length; i++)
            {
                Assembly assembly = assemblies[i];
                if (assembly == null)
                {
                    continue;
                }

                try
                {
                    Type type = assembly.GetType(fullName, throwOnError: false, ignoreCase: false);
                    if (type != null)
                    {
                        return type;
                    }
                }
                catch (ReflectionTypeLoadException)
                {
                }
                catch (TypeLoadException)
                {
                }
                catch (FileLoadException)
                {
                }
                catch (BadImageFormatException)
                {
                }
            }

            return null;
        }

        internal static MethodInfo FindMethod(Type type, string name, params Type[] parameterTypes)
        {
            if (type == null || string.IsNullOrWhiteSpace(name))
            {
                return null;
            }

            const BindingFlags Flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;
            try
            {
                return type.GetMethod(name, Flags, binder: null, types: parameterTypes ?? Type.EmptyTypes, modifiers: null);
            }
            catch (AmbiguousMatchException)
            {
                MethodInfo[] methods = type.GetMethods(Flags);
                for (int i = 0; i < methods.Length; i++)
                {
                    MethodInfo method = methods[i];
                    if (!string.Equals(method.Name, name, StringComparison.Ordinal))
                    {
                        continue;
                    }

                    ParameterInfo[] parameters = method.GetParameters();
                    int expectedCount = parameterTypes?.Length ?? 0;
                    if (parameters.Length != expectedCount)
                    {
                        continue;
                    }

                    bool match = true;
                    for (int j = 0; j < expectedCount; j++)
                    {
                        if (parameters[j].ParameterType != parameterTypes[j])
                        {
                            match = false;
                            break;
                        }
                    }

                    if (match)
                    {
                        return method;
                    }
                }
            }

            return null;
        }

        internal static bool TryGetInstanceFieldOrProperty<T>(object target, string memberName, out T value)
        {
            value = default;
            if (!TryGetFieldOrPropertyValue(target, memberName, InstanceFlags, out object rawValue))
            {
                return false;
            }

            if (rawValue is T typedValue)
            {
                value = typedValue;
                return true;
            }

            if (rawValue == null)
            {
                return !typeof(T).IsValueType || Nullable.GetUnderlyingType(typeof(T)) != null;
            }

            return false;
        }

        internal static bool TrySetInstanceFieldOrProperty(object target, string memberName, object value)
        {
            return TrySetFieldOrPropertyValue(target, memberName, value, InstanceFlags);
        }

        private static bool TryGetFieldOrPropertyValue(object target, string memberName, BindingFlags flags, out object value)
        {
            value = null;
            if (target == null || string.IsNullOrWhiteSpace(memberName))
            {
                return false;
            }

            Type targetType = target.GetType();
            FieldInfo field = FindField(targetType, memberName, flags);
            if (field != null)
            {
                value = field.GetValue(target);
                return true;
            }

            PropertyInfo property = FindProperty(targetType, memberName, flags);
            if (property == null || !property.CanRead)
            {
                return false;
            }

            value = property.GetValue(target, index: null);
            return true;
        }

        private static bool TrySetFieldOrPropertyValue(object target, string memberName, object value, BindingFlags flags)
        {
            if (target == null || string.IsNullOrWhiteSpace(memberName))
            {
                return false;
            }

            Type targetType = target.GetType();
            FieldInfo field = FindField(targetType, memberName, flags);
            if (field != null)
            {
                if (!TryConvertValue(field.FieldType, value, out object convertedValue))
                {
                    return false;
                }

                field.SetValue(target, convertedValue);
                return true;
            }

            PropertyInfo property = FindProperty(targetType, memberName, flags);
            if (property == null || !property.CanWrite)
            {
                return false;
            }

            if (!TryConvertValue(property.PropertyType, value, out object convertedPropertyValue))
            {
                return false;
            }

            property.SetValue(target, convertedPropertyValue, index: null);
            return true;
        }

        private static bool TryConvertValue(Type targetType, object value, out object convertedValue)
        {
            convertedValue = value;

            if (targetType == null)
            {
                return false;
            }

            if (value == null)
            {
                return !targetType.IsValueType || Nullable.GetUnderlyingType(targetType) != null;
            }

            if (targetType.IsInstanceOfType(value))
            {
                return true;
            }

            Type underlyingType = Nullable.GetUnderlyingType(targetType) ?? targetType;

            try
            {
                if (underlyingType.IsEnum && value is string enumName)
                {
                    convertedValue = Enum.Parse(underlyingType, enumName, ignoreCase: false);
                    return true;
                }

                convertedValue = Convert.ChangeType(value, underlyingType);
                return true;
            }
            catch (InvalidCastException)
            {
                return false;
            }
            catch (FormatException)
            {
                return false;
            }
            catch (OverflowException)
            {
                return false;
            }
        }

        private static FieldInfo FindField(Type type, string name, BindingFlags flags)
        {
            for (Type currentType = type; currentType != null; currentType = currentType.BaseType)
            {
                FieldInfo field = currentType.GetField(name, flags | BindingFlags.DeclaredOnly);
                if (field != null)
                {
                    return field;
                }
            }

            return null;
        }

        private static PropertyInfo FindProperty(Type type, string name, BindingFlags flags)
        {
            for (Type currentType = type; currentType != null; currentType = currentType.BaseType)
            {
                PropertyInfo property = currentType.GetProperty(name, flags | BindingFlags.DeclaredOnly);
                if (property != null)
                {
                    return property;
                }
            }

            return null;
        }
    }
}

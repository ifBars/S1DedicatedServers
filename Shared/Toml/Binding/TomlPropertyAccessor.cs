using System;
using System.Reflection;

namespace DedicatedServerMod.Shared.Toml.Binding
{
    /// <summary>
    /// Wraps reflected property access for typed TOML binding.
    /// </summary>
    internal sealed class TomlPropertyAccessor
    {
        public TomlPropertyAccessor(PropertyInfo property)
        {
            Property = property ?? throw new ArgumentNullException(nameof(property));
            DeclaredType = property.PropertyType;
        }

        public PropertyInfo Property { get; }

        public Type DeclaredType { get; }

        public string Name => Property.Name;

        public object GetValue(object target)
        {
            return Property.GetValue(target);
        }

        public void SetValue(object target, object value)
        {
            Property.SetValue(target, value);
        }
    }
}

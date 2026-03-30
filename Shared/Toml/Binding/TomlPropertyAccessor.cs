using System.Reflection;

namespace DedicatedServerMod.Shared.Toml.Binding
{
    /// <summary>
    /// Wraps reflected property access for typed TOML binding.
    /// </summary>
    internal sealed class TomlPropertyAccessor(PropertyInfo property)
    {
        public PropertyInfo Property { get; } = property ?? throw new ArgumentNullException(nameof(property));

        public Type DeclaredType { get; } = property.PropertyType;

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

using System;
using System.Collections.Generic;
using System.Linq;

namespace DedicatedServerMod.API.Toml
{
    /// <summary>
    /// Represents a typed TOML value in the framework's constrained TOML subset.
    /// </summary>
    public sealed class TomlValue
    {
        private readonly object _value;

        private TomlValue(TomlValueKind kind, object value)
        {
            Kind = kind;
            _value = value;
        }

        /// <summary>
        /// Gets the TOML value kind.
        /// </summary>
        public TomlValueKind Kind { get; }

        /// <summary>
        /// Creates a string TOML value.
        /// </summary>
        public static TomlValue FromString(string value)
        {
            return new TomlValue(TomlValueKind.String, value ?? string.Empty);
        }

        /// <summary>
        /// Creates a boolean TOML value.
        /// </summary>
        public static TomlValue FromBoolean(bool value)
        {
            return new TomlValue(TomlValueKind.Boolean, value);
        }

        /// <summary>
        /// Creates an integer TOML value.
        /// </summary>
        public static TomlValue FromInteger(long value)
        {
            return new TomlValue(TomlValueKind.Integer, value);
        }

        /// <summary>
        /// Creates a floating-point TOML value.
        /// </summary>
        public static TomlValue FromFloat(double value)
        {
            return new TomlValue(TomlValueKind.Float, value);
        }

        /// <summary>
        /// Creates an array TOML value.
        /// </summary>
        public static TomlValue FromArray(IEnumerable<TomlValue> items)
        {
            return new TomlValue(
                TomlValueKind.Array,
                (items ?? Array.Empty<TomlValue>()).ToList().AsReadOnly());
        }

        /// <summary>
        /// Gets the raw stored value.
        /// </summary>
        internal object RawValue => _value;

        /// <summary>
        /// Gets the string value.
        /// </summary>
        public string GetString()
        {
            return Kind == TomlValueKind.String
                ? (string)_value
                : throw new InvalidOperationException($"Toml value kind '{Kind}' is not a string.");
        }

        /// <summary>
        /// Gets the boolean value.
        /// </summary>
        public bool GetBoolean()
        {
            return Kind == TomlValueKind.Boolean
                ? (bool)_value
                : throw new InvalidOperationException($"Toml value kind '{Kind}' is not a boolean.");
        }

        /// <summary>
        /// Gets the integer value.
        /// </summary>
        public long GetInt64()
        {
            return Kind == TomlValueKind.Integer
                ? (long)_value
                : throw new InvalidOperationException($"Toml value kind '{Kind}' is not an integer.");
        }

        /// <summary>
        /// Gets the floating-point value.
        /// </summary>
        public double GetDouble()
        {
            return Kind == TomlValueKind.Float
                ? (double)_value
                : throw new InvalidOperationException($"Toml value kind '{Kind}' is not a float.");
        }

        /// <summary>
        /// Gets the array value.
        /// </summary>
        public IReadOnlyList<TomlValue> GetArray()
        {
            return Kind == TomlValueKind.Array
                ? (IReadOnlyList<TomlValue>)_value
                : throw new InvalidOperationException($"Toml value kind '{Kind}' is not an array.");
        }

        /// <summary>
        /// Attempts to read the value as a string.
        /// </summary>
        public bool TryGetString(out string value)
        {
            value = Kind == TomlValueKind.String ? (string)_value : string.Empty;
            return Kind == TomlValueKind.String;
        }

        /// <summary>
        /// Attempts to read the value as a boolean.
        /// </summary>
        public bool TryGetBoolean(out bool value)
        {
            if (Kind == TomlValueKind.Boolean)
            {
                value = (bool)_value;
                return true;
            }

            value = false;
            return false;
        }

        /// <summary>
        /// Attempts to read the value as an integer.
        /// </summary>
        public bool TryGetInt64(out long value)
        {
            if (Kind == TomlValueKind.Integer)
            {
                value = (long)_value;
                return true;
            }

            value = 0;
            return false;
        }

        /// <summary>
        /// Attempts to read the value as a floating-point value.
        /// </summary>
        public bool TryGetDouble(out double value)
        {
            if (Kind == TomlValueKind.Float)
            {
                value = (double)_value;
                return true;
            }

            value = 0;
            return false;
        }

        /// <summary>
        /// Attempts to read the value as an array.
        /// </summary>
        public bool TryGetArray(out IReadOnlyList<TomlValue> values)
        {
            if (Kind == TomlValueKind.Array)
            {
                values = (IReadOnlyList<TomlValue>)_value;
                return true;
            }

            values = Array.Empty<TomlValue>();
            return false;
        }

        /// <inheritdoc />
        public override string ToString()
        {
            switch (Kind)
            {
                case TomlValueKind.String:
                    return GetString();
                case TomlValueKind.Boolean:
                    return GetBoolean().ToString();
                case TomlValueKind.Integer:
                    return GetInt64().ToString();
                case TomlValueKind.Float:
                    return GetDouble().ToString();
                case TomlValueKind.Array:
                    return $"[{string.Join(", ", GetArray().Select(item => item.ToString()))}]";
                default:
                    return string.Empty;
            }
        }
    }
}

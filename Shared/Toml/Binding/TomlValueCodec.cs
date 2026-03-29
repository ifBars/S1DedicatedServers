using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using DedicatedServerMod.API.Toml;

namespace DedicatedServerMod.Shared.Toml.Binding
{
    /// <summary>
    /// Shared TOML token parsing and CLR conversion helpers.
    /// </summary>
    internal static class TomlValueCodec
    {
        public static string StripInlineComment(string line)
        {
            if (string.IsNullOrEmpty(line))
            {
                return string.Empty;
            }

            bool inBasicString = false;
            bool inLiteralString = false;
            bool isEscaped = false;

            for (int index = 0; index < line.Length; index++)
            {
                char character = line[index];

                if (isEscaped)
                {
                    isEscaped = false;
                    continue;
                }

                if (inBasicString)
                {
                    if (character == '\\')
                    {
                        isEscaped = true;
                    }
                    else if (character == '"')
                    {
                        inBasicString = false;
                    }

                    continue;
                }

                if (inLiteralString)
                {
                    if (character == '\'')
                    {
                        inLiteralString = false;
                    }

                    continue;
                }

                if (character == '"')
                {
                    inBasicString = true;
                    continue;
                }

                if (character == '\'')
                {
                    inLiteralString = true;
                    continue;
                }

                if (character == '#')
                {
                    return line.Substring(0, index);
                }
            }

            return line;
        }

        public static bool IsValueComplete(string value)
        {
            int bracketDepth = 0;
            bool inBasicString = false;
            bool inLiteralString = false;
            bool isEscaped = false;

            foreach (char character in value ?? string.Empty)
            {
                if (isEscaped)
                {
                    isEscaped = false;
                    continue;
                }

                if (inBasicString)
                {
                    if (character == '\\')
                    {
                        isEscaped = true;
                    }
                    else if (character == '"')
                    {
                        inBasicString = false;
                    }

                    continue;
                }

                if (inLiteralString)
                {
                    if (character == '\'')
                    {
                        inLiteralString = false;
                    }

                    continue;
                }

                switch (character)
                {
                    case '"':
                        inBasicString = true;
                        break;
                    case '\'':
                        inLiteralString = true;
                        break;
                    case '[':
                        bracketDepth++;
                        break;
                    case ']':
                        bracketDepth--;
                        break;
                }
            }

            return bracketDepth <= 0 && !inBasicString && !inLiteralString;
        }

        public static bool TryParseToken(string rawValue, out TomlValue value, out string error)
        {
            string trimmedValue = (rawValue ?? string.Empty).Trim();

            if (trimmedValue.StartsWith("[", StringComparison.Ordinal))
            {
                return TryParseArray(trimmedValue, out value, out error);
            }

            if (TryParseString(trimmedValue, out string stringValue))
            {
                value = TomlValue.FromString(stringValue);
                error = string.Empty;
                return true;
            }

            if (bool.TryParse(trimmedValue, out bool boolValue))
            {
                value = TomlValue.FromBoolean(boolValue);
                error = string.Empty;
                return true;
            }

            if (long.TryParse(trimmedValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out long integerValue))
            {
                value = TomlValue.FromInteger(integerValue);
                error = string.Empty;
                return true;
            }

            if (double.TryParse(trimmedValue, NumberStyles.Float, CultureInfo.InvariantCulture, out double floatValue))
            {
                value = TomlValue.FromFloat(floatValue);
                error = string.Empty;
                return true;
            }

            value = TomlValue.FromString(trimmedValue);
            error = string.Empty;
            return true;
        }

        public static void AppendFormattedValue(StringBuilder builder, TomlValue value)
        {
            switch (value.Kind)
            {
                case TomlValueKind.String:
                    builder.Append(FormatString(value.GetString()));
                    break;
                case TomlValueKind.Boolean:
                    builder.Append(value.GetBoolean() ? "true" : "false");
                    break;
                case TomlValueKind.Integer:
                    builder.Append(value.GetInt64().ToString(CultureInfo.InvariantCulture));
                    break;
                case TomlValueKind.Float:
                    builder.Append(value.GetDouble().ToString("0.0###############", CultureInfo.InvariantCulture));
                    break;
                case TomlValueKind.Array:
                    builder.Append("[");
                    IReadOnlyList<TomlValue> items = value.GetArray();
                    for (int index = 0; index < items.Count; index++)
                    {
                        if (index > 0)
                        {
                            builder.Append(", ");
                        }

                        AppendFormattedValue(builder, items[index]);
                    }

                    builder.Append("]");
                    break;
                default:
                    builder.Append("''");
                    break;
            }
        }

        public static string FormatString(string value)
        {
            string safeValue = value ?? string.Empty;
            if (!safeValue.Contains('\'') && !safeValue.Contains('\r') && !safeValue.Contains('\n'))
            {
                return $"'{safeValue}'";
            }

            StringBuilder escaped = new StringBuilder(safeValue.Length + 2);

            foreach (char character in safeValue)
            {
                switch (character)
                {
                    case '\\':
                        escaped.Append("\\\\");
                        break;
                    case '\"':
                        escaped.Append("\\\"");
                        break;
                    case '\r':
                        escaped.Append("\\r");
                        break;
                    case '\n':
                        escaped.Append("\\n");
                        break;
                    case '\t':
                        escaped.Append("\\t");
                        break;
                    default:
                        escaped.Append(character);
                        break;
                }
            }

            return $"\"{escaped}\"";
        }

        public static bool TryConvertToClr(TomlValue value, Type targetType, out object convertedValue, out string error)
        {
            if (value == null)
            {
                convertedValue = null;
                error = "TOML value was null.";
                return false;
            }

            if (targetType == typeof(string))
            {
                if (value.TryGetString(out string stringValue))
                {
                    convertedValue = stringValue;
                    error = string.Empty;
                    return true;
                }
            }
            else if (targetType == typeof(bool))
            {
                if (value.TryGetBoolean(out bool boolValue))
                {
                    convertedValue = boolValue;
                    error = string.Empty;
                    return true;
                }
            }
            else if (targetType == typeof(int))
            {
                if (TryConvertToInt32(value, out int intValue))
                {
                    convertedValue = intValue;
                    error = string.Empty;
                    return true;
                }
            }
            else if (targetType == typeof(long))
            {
                if (value.TryGetInt64(out long longValue))
                {
                    convertedValue = longValue;
                    error = string.Empty;
                    return true;
                }
            }
            else if (targetType == typeof(float))
            {
                if (TryConvertToDouble(value, out double doubleValue))
                {
                    convertedValue = (float)doubleValue;
                    error = string.Empty;
                    return true;
                }
            }
            else if (targetType == typeof(double))
            {
                if (TryConvertToDouble(value, out double doubleValue))
                {
                    convertedValue = doubleValue;
                    error = string.Empty;
                    return true;
                }
            }
            else if (targetType.IsEnum)
            {
                if (value.TryGetString(out string enumString))
                {
                    try
                    {
                        convertedValue = Enum.Parse(targetType, enumString, ignoreCase: true);
                        error = string.Empty;
                        return true;
                    }
                    catch (Exception ex)
                    {
                        convertedValue = null;
                        error = ex.Message;
                        return false;
                    }
                }
            }
            else if (targetType == typeof(List<string>))
            {
                if (TryConvertToStringList(value, out List<string> values))
                {
                    convertedValue = values;
                    error = string.Empty;
                    return true;
                }
            }
            else if (targetType == typeof(HashSet<string>))
            {
                if (TryConvertToStringList(value, out List<string> values))
                {
                    convertedValue = new HashSet<string>(values, StringComparer.Ordinal);
                    error = string.Empty;
                    return true;
                }
            }

            convertedValue = null;
            error = $"Unsupported TOML conversion to CLR type '{targetType.FullName}'.";
            return false;
        }

        public static bool TryConvertFromClr(object value, Type declaredType, out TomlValue tomlValue, out string error)
        {
            Type valueType = declaredType ?? value?.GetType();
            if (valueType == null)
            {
                tomlValue = TomlValue.FromString(string.Empty);
                error = string.Empty;
                return true;
            }

            if (valueType == typeof(string))
            {
                tomlValue = TomlValue.FromString((string)value ?? string.Empty);
                error = string.Empty;
                return true;
            }

            if (valueType == typeof(bool))
            {
                tomlValue = TomlValue.FromBoolean(value is bool boolValue && boolValue);
                error = string.Empty;
                return true;
            }

            if (valueType == typeof(int))
            {
                tomlValue = TomlValue.FromInteger(value is int intValue ? intValue : 0);
                error = string.Empty;
                return true;
            }

            if (valueType == typeof(long))
            {
                tomlValue = TomlValue.FromInteger(value is long longValue ? longValue : 0L);
                error = string.Empty;
                return true;
            }

            if (valueType == typeof(float))
            {
                tomlValue = TomlValue.FromFloat(value is float floatValue ? floatValue : 0f);
                error = string.Empty;
                return true;
            }

            if (valueType == typeof(double))
            {
                tomlValue = TomlValue.FromFloat(value is double doubleValue ? doubleValue : 0d);
                error = string.Empty;
                return true;
            }

            if (valueType.IsEnum)
            {
                tomlValue = TomlValue.FromString(value?.ToString() ?? string.Empty);
                error = string.Empty;
                return true;
            }

            if (valueType == typeof(List<string>) || valueType == typeof(HashSet<string>))
            {
                tomlValue = TomlValue.FromArray(((IEnumerable<string>)value ?? Enumerable.Empty<string>()).Select(TomlValue.FromString));
                error = string.Empty;
                return true;
            }

            tomlValue = null;
            error = $"Unsupported CLR to TOML conversion for type '{valueType.FullName}'.";
            return false;
        }

        private static bool TryParseArray(string rawValue, out TomlValue value, out string error)
        {
            string trimmed = rawValue.Trim();
            if (!trimmed.StartsWith("[", StringComparison.Ordinal) || !trimmed.EndsWith("]", StringComparison.Ordinal))
            {
                value = null;
                error = "Array values must start with '[' and end with ']'.";
                return false;
            }

            string inner = trimmed.Substring(1, trimmed.Length - 2);
            List<string> itemTokens = SplitArrayItems(inner);
            List<TomlValue> items = new List<TomlValue>(itemTokens.Count);

            foreach (string itemToken in itemTokens)
            {
                if (!TryParseToken(itemToken, out TomlValue itemValue, out error))
                {
                    value = null;
                    return false;
                }

                items.Add(itemValue);
            }

            value = TomlValue.FromArray(items);
            error = string.Empty;
            return true;
        }

        private static List<string> SplitArrayItems(string inner)
        {
            List<string> items = new List<string>();
            StringBuilder currentItem = new StringBuilder();
            bool inBasicString = false;
            bool inLiteralString = false;
            bool isEscaped = false;
            int bracketDepth = 0;

            foreach (char character in inner ?? string.Empty)
            {
                if (isEscaped)
                {
                    currentItem.Append(character);
                    isEscaped = false;
                    continue;
                }

                if (inBasicString)
                {
                    currentItem.Append(character);
                    if (character == '\\')
                    {
                        isEscaped = true;
                    }
                    else if (character == '"')
                    {
                        inBasicString = false;
                    }

                    continue;
                }

                if (inLiteralString)
                {
                    currentItem.Append(character);
                    if (character == '\'')
                    {
                        inLiteralString = false;
                    }

                    continue;
                }

                switch (character)
                {
                    case '"':
                        inBasicString = true;
                        currentItem.Append(character);
                        break;
                    case '\'':
                        inLiteralString = true;
                        currentItem.Append(character);
                        break;
                    case '[':
                        bracketDepth++;
                        currentItem.Append(character);
                        break;
                    case ']':
                        bracketDepth--;
                        currentItem.Append(character);
                        break;
                    case ',' when bracketDepth == 0:
                        AddArrayItem(items, currentItem);
                        break;
                    default:
                        currentItem.Append(character);
                        break;
                }
            }

            AddArrayItem(items, currentItem);
            return items;
        }

        private static void AddArrayItem(ICollection<string> items, StringBuilder currentItem)
        {
            string item = currentItem.ToString().Trim();
            if (item.Length > 0)
            {
                items.Add(item);
            }

            currentItem.Clear();
        }

        private static bool TryParseString(string trimmedValue, out string value)
        {
            value = string.Empty;

            if (trimmedValue.Length >= 2 &&
                trimmedValue.StartsWith("'", StringComparison.Ordinal) &&
                trimmedValue.EndsWith("'", StringComparison.Ordinal))
            {
                value = trimmedValue.Substring(1, trimmedValue.Length - 2);
                return true;
            }

            if (trimmedValue.Length >= 2 &&
                trimmedValue.StartsWith("\"", StringComparison.Ordinal) &&
                trimmedValue.EndsWith("\"", StringComparison.Ordinal))
            {
                value = UnescapeBasicString(trimmedValue.Substring(1, trimmedValue.Length - 2));
                return true;
            }

            return false;
        }

        private static string UnescapeBasicString(string value)
        {
            StringBuilder builder = new StringBuilder(value?.Length ?? 0);

            for (int index = 0; index < (value?.Length ?? 0); index++)
            {
                char character = value[index];
                if (character != '\\' || index == value.Length - 1)
                {
                    builder.Append(character);
                    continue;
                }

                char escapeCode = value[++index];
                switch (escapeCode)
                {
                    case '\\':
                        builder.Append('\\');
                        break;
                    case '"':
                        builder.Append('"');
                        break;
                    case 'n':
                        builder.Append('\n');
                        break;
                    case 'r':
                        builder.Append('\r');
                        break;
                    case 't':
                        builder.Append('\t');
                        break;
                    case 'u':
                        builder.Append(ParseUnicodeEscape(value, ref index, 4));
                        break;
                    case 'U':
                        builder.Append(ParseUnicodeEscape(value, ref index, 8));
                        break;
                    default:
                        builder.Append(escapeCode);
                        break;
                }
            }

            return builder.ToString();
        }

        private static string ParseUnicodeEscape(string value, ref int index, int length)
        {
            if (index + length >= value.Length)
            {
                return string.Empty;
            }

            string hex = value.Substring(index + 1, length);
            index += length;

            return int.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int codePoint)
                ? char.ConvertFromUtf32(codePoint)
                : string.Empty;
        }

        private static bool TryConvertToInt32(TomlValue value, out int convertedValue)
        {
            convertedValue = 0;

            if (!value.TryGetInt64(out long integerValue))
            {
                return false;
            }

            if (integerValue < int.MinValue || integerValue > int.MaxValue)
            {
                return false;
            }

            convertedValue = (int)integerValue;
            return true;
        }

        private static bool TryConvertToDouble(TomlValue value, out double convertedValue)
        {
            if (value.TryGetDouble(out convertedValue))
            {
                return true;
            }

            if (value.TryGetInt64(out long integerValue))
            {
                convertedValue = integerValue;
                return true;
            }

            convertedValue = 0d;
            return false;
        }

        private static bool TryConvertToStringList(TomlValue value, out List<string> values)
        {
            values = new List<string>();

            if (!value.TryGetArray(out IReadOnlyList<TomlValue> items))
            {
                return false;
            }

            foreach (TomlValue item in items)
            {
                if (!item.TryGetString(out string stringValue))
                {
                    values = new List<string>();
                    return false;
                }

                values.Add(stringValue);
            }

            return true;
        }
    }
}

using System.Collections;
using System.Globalization;
using System.Reflection;
using System.Text;
using DedicatedServerMod.API.Toml;
using Newtonsoft.Json;

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
            bool inBasicString = false;
            bool inLiteralString = false;
            bool isEscaped = false;
            int bracketDepth = 0;
            int braceDepth = 0;

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
                    case '{':
                        braceDepth++;
                        break;
                    case '}':
                        braceDepth--;
                        break;
                }
            }

            return bracketDepth <= 0 && braceDepth <= 0 && !inBasicString && !inLiteralString;
        }

        public static bool TryParseToken(string rawValue, out TomlValue value, out string error)
        {
            string trimmedValue = (rawValue ?? string.Empty).Trim();

            if (trimmedValue.StartsWith("[", StringComparison.Ordinal))
            {
                return TryParseArray(trimmedValue, out value, out error);
            }

            if (trimmedValue.StartsWith("{", StringComparison.Ordinal))
            {
                return TryParseInlineTable(trimmedValue, out value, out error);
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
                    AppendArray(builder, value.GetArray());
                    break;
                case TomlValueKind.InlineTable:
                    AppendInlineTable(builder, value.GetInlineTable());
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
            else if (TryGetListElementType(targetType, out Type elementType) && IsSerializableObjectType(elementType))
            {
                return TryConvertToObjectList(value, targetType, elementType, out convertedValue, out error);
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

            if (TryGetListElementType(valueType, out Type elementType) && IsSerializableObjectType(elementType))
            {
                tomlValue = TomlValue.FromArray(ConvertObjectListToTomlValues((IEnumerable)value, elementType));
                error = string.Empty;
                return true;
            }

            tomlValue = null;
            error = $"Unsupported CLR to TOML conversion for type '{valueType.FullName}'.";
            return false;
        }

        private static void AppendArray(StringBuilder builder, IReadOnlyList<TomlValue> items)
        {
            builder.Append("[");
            for (int index = 0; index < items.Count; index++)
            {
                if (index > 0)
                {
                    builder.Append(", ");
                }

                AppendFormattedValue(builder, items[index]);
            }

            builder.Append("]");
        }

        private static void AppendInlineTable(StringBuilder builder, IReadOnlyDictionary<string, TomlValue> table)
        {
            builder.Append("{ ");
            int entryIndex = 0;
            foreach (KeyValuePair<string, TomlValue> entry in table)
            {
                if (entryIndex++ > 0)
                {
                    builder.Append(", ");
                }

                builder.Append(entry.Key).Append(" = ");
                AppendFormattedValue(builder, entry.Value);
            }

            builder.Append(" }");
        }

        private static bool TryParseInlineTable(string rawValue, out TomlValue value, out string error)
        {
            string trimmed = rawValue.Trim();
            if (!trimmed.StartsWith("{", StringComparison.Ordinal) || !trimmed.EndsWith("}", StringComparison.Ordinal))
            {
                value = null;
                error = "Inline table values must start with '{' and end with '}'.";
                return false;
            }

            Dictionary<string, TomlValue> entries = new Dictionary<string, TomlValue>(StringComparer.Ordinal);
            foreach (string entryToken in SplitDelimitedItems(trimmed.Substring(1, trimmed.Length - 2)))
            {
                int equalsIndex = FindTopLevelEquals(entryToken);
                if (equalsIndex <= 0)
                {
                    value = null;
                    error = $"Inline table entry '{entryToken}' is missing a key/value separator.";
                    return false;
                }

                string key = entryToken.Substring(0, equalsIndex).Trim();
                string itemValueText = entryToken.Substring(equalsIndex + 1).Trim();
                if (string.IsNullOrWhiteSpace(key))
                {
                    value = null;
                    error = "Inline table entry key cannot be empty.";
                    return false;
                }

                if (!TryParseToken(itemValueText, out TomlValue itemValue, out error))
                {
                    value = null;
                    return false;
                }

                entries[key] = itemValue;
            }

            value = TomlValue.FromInlineTable(entries);
            error = string.Empty;
            return true;
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

            List<TomlValue> items = new List<TomlValue>();
            foreach (string itemToken in SplitDelimitedItems(trimmed.Substring(1, trimmed.Length - 2)))
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

        private static List<string> SplitDelimitedItems(string inner)
        {
            List<string> items = new List<string>();
            StringBuilder currentItem = new StringBuilder();
            bool inBasicString = false;
            bool inLiteralString = false;
            bool isEscaped = false;
            int bracketDepth = 0;
            int braceDepth = 0;

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
                    case '{':
                        braceDepth++;
                        currentItem.Append(character);
                        break;
                    case '}':
                        braceDepth--;
                        currentItem.Append(character);
                        break;
                    case ',' when bracketDepth == 0 && braceDepth == 0:
                        AddDelimitedItem(items, currentItem);
                        break;
                    default:
                        currentItem.Append(character);
                        break;
                }
            }

            AddDelimitedItem(items, currentItem);
            return items;
        }

        private static void AddDelimitedItem(ICollection<string> items, StringBuilder currentItem)
        {
            string item = currentItem.ToString().Trim();
            if (item.Length > 0)
            {
                items.Add(item);
            }

            currentItem.Clear();
        }

        private static int FindTopLevelEquals(string value)
        {
            bool inBasicString = false;
            bool inLiteralString = false;
            bool isEscaped = false;
            int bracketDepth = 0;
            int braceDepth = 0;

            for (int index = 0; index < (value ?? string.Empty).Length; index++)
            {
                char character = value[index];

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
                    case '{':
                        braceDepth++;
                        break;
                    case '}':
                        braceDepth--;
                        break;
                    case '=' when bracketDepth == 0 && braceDepth == 0:
                        return index;
                }
            }

            return -1;
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

        private static bool TryGetListElementType(Type type, out Type elementType)
        {
            if (type != null && type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>))
            {
                elementType = type.GetGenericArguments()[0];
                return true;
            }

            elementType = null;
            return false;
        }

        private static bool IsSerializableObjectType(Type type)
        {
            return type != null
                && type.IsClass
                && type != typeof(string)
                && HasParameterlessConstructor(type)
                && GetSerializableProperties(type).Count > 0;
        }

        private static bool HasParameterlessConstructor(Type type)
        {
            return type.GetConstructor(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, Type.EmptyTypes, null) != null;
        }

        private static IReadOnlyList<PropertyInfo> GetSerializableProperties(Type type)
        {
            return type.GetProperties(BindingFlags.Instance | BindingFlags.Public)
                .Where(property => property.CanRead
                    && property.CanWrite
                    && property.GetIndexParameters().Length == 0
                    && property.GetCustomAttributes(typeof(JsonIgnoreAttribute), inherit: true).Length == 0
                    && IsSupportedSerializableMemberType(property.PropertyType))
                .ToList();
        }

        private static bool IsSupportedSerializableMemberType(Type type)
        {
            return type == typeof(string)
                || type == typeof(bool)
                || type == typeof(int)
                || type == typeof(long)
                || type == typeof(float)
                || type == typeof(double)
                || type.IsEnum
                || type == typeof(List<string>)
                || type == typeof(HashSet<string>);
        }

        private static bool TryConvertToObjectList(TomlValue value, Type listType, Type elementType, out object objectList, out string error)
        {
            if (!value.TryGetArray(out IReadOnlyList<TomlValue> items))
            {
                objectList = null;
                error = "Expected a TOML array of inline tables.";
                return false;
            }

            IList list = (IList)Activator.CreateInstance(listType);
            foreach (TomlValue item in items)
            {
                if (!TryConvertInlineTableToObject(item, elementType, out object convertedItem, out error))
                {
                    objectList = null;
                    return false;
                }

                list.Add(convertedItem);
            }

            objectList = list;
            error = string.Empty;
            return true;
        }

        private static bool TryConvertInlineTableToObject(TomlValue value, Type elementType, out object convertedObject, out string error)
        {
            if (!value.TryGetInlineTable(out IReadOnlyDictionary<string, TomlValue> table))
            {
                convertedObject = null;
                error = "Expected an inline table item.";
                return false;
            }

            object instance = Activator.CreateInstance(elementType, nonPublic: true);
            foreach (PropertyInfo property in GetSerializableProperties(elementType))
            {
                string key = ResolvePropertyKey(property);
                if (!table.TryGetValue(key, out TomlValue memberValue))
                {
                    continue;
                }

                if (!TryConvertToClr(memberValue, property.PropertyType, out object convertedValue, out error))
                {
                    convertedObject = null;
                    return false;
                }

                property.SetValue(instance, convertedValue);
            }

            convertedObject = instance;
            error = string.Empty;
            return true;
        }

        private static IEnumerable<TomlValue> ConvertObjectListToTomlValues(IEnumerable values, Type elementType)
        {
            foreach (object item in values ?? Array.Empty<object>())
            {
                if (item == null)
                {
                    continue;
                }

                yield return ConvertObjectToInlineTable(item, elementType);
            }
        }

        private static TomlValue ConvertObjectToInlineTable(object value, Type elementType)
        {
            Dictionary<string, TomlValue> entries = new Dictionary<string, TomlValue>(StringComparer.Ordinal);
            foreach (PropertyInfo property in GetSerializableProperties(elementType))
            {
                if (TryConvertFromClr(property.GetValue(value), property.PropertyType, out TomlValue memberValue, out _))
                {
                    entries[ResolvePropertyKey(property)] = memberValue;
                }
            }

            return TomlValue.FromInlineTable(entries);
        }

        private static string ResolvePropertyKey(PropertyInfo property)
        {
            JsonPropertyAttribute attribute = property.GetCustomAttributes(typeof(JsonPropertyAttribute), inherit: true)
                .OfType<JsonPropertyAttribute>()
                .FirstOrDefault();

            return string.IsNullOrWhiteSpace(attribute?.PropertyName)
                ? property.Name
                : attribute.PropertyName;
        }
    }
}

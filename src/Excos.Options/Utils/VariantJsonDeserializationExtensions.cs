using System.ComponentModel;
using System.Text.Json;
using System.Text.Json.Serialization;

internal static class VariantJsonDeserializationExtensions
{
    // The following options make JSON deserialization act more like configuration binding.
    private static readonly JsonSerializerOptions ConfigurationJsonSettings = new JsonSerializerOptions
    {
        PropertyNameCaseInsensitive = true,
        NumberHandling = JsonNumberHandling.AllowReadingFromString,
        AllowTrailingCommas = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        IncludeFields = true,
        PreferredObjectCreationHandling = JsonObjectCreationHandling.Populate,
        Converters =
        {
            new CaseInsensitiveEnumConverterFactory(),
            new TypeConverterJsonConverterFactory(),
        },
    };

    public static TOptions DeserializeMergedConfiguration<TOptions>(this JsonElement mergedElement, string sectionName)
        where TOptions : class, new()
    {
        if (mergedElement.ValueKind != JsonValueKind.Object)
        {
            return new TOptions();
        }

        if (!string.IsNullOrEmpty(sectionName))
        {
            var sectionParts = sectionName.AsSpan().Split(':');
            foreach (var part in sectionParts)
            {
                bool found = false;
                foreach (var property in mergedElement.EnumerateObject())
                {
                    // TODO: figure out how to avoid allocation when calling property.Name (currently allocates a new string on each call)
                    if (string.Equals(property.Name, sectionName[part], StringComparison.OrdinalIgnoreCase))
                    {
                        mergedElement = property.Value;
                        found = true;
                        break;
                    }
                }
                if (!found)
                {
                    return new TOptions();
                }
            }
        }

        return mergedElement.Deserialize<TOptions>(ConfigurationJsonSettings) ?? new TOptions();
    }

    private class CaseInsensitiveEnumConverterFactory : JsonConverterFactory
    {
        public override bool CanConvert(Type typeToConvert)
            => typeToConvert.IsEnum;

        public override JsonConverter CreateConverter(Type type, JsonSerializerOptions options)
        {
            var converterType = typeof(CaseInsensitiveEnumConverter<>).MakeGenericType(type);
            return (JsonConverter)Activator.CreateInstance(converterType)!;
        }
    }

    private class CaseInsensitiveEnumConverter<T> : JsonConverter<T> where T : struct, Enum
    {
        public override T Read(ref Utf8JsonReader reader, Type type, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.Number)
            {
                var intValue = reader.GetInt32();
                return (T)Enum.ToObject(typeof(T), intValue);
            }

            var stringValue = reader.GetString();
            if (Enum.TryParse<T>(stringValue, ignoreCase: true, out var val))
            {
                return val;
            }

            throw new JsonException($"Invalid enum value '{stringValue}' for type {typeof(T).Name}.");
        }

        public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options)
            => writer.WriteStringValue(value.ToString());
    }

    public class TypeConverterJsonConverterFactory : JsonConverterFactory
    {
        public override bool CanConvert(Type typeToConvert)
        {
            var conv = TypeDescriptor.GetConverter(typeToConvert);
            return conv.CanConvertFrom(typeof(string));
        }

        public override JsonConverter CreateConverter(Type type, JsonSerializerOptions options)
        {
            var t = typeof(TypeConverterJsonConverter<>).MakeGenericType(type);
            return (JsonConverter)Activator.CreateInstance(t)!;
        }
    }

    public class TypeConverterJsonConverter<T> : JsonConverter<T>
    {
        private static readonly TypeConverter Converter = TypeDescriptor.GetConverter(typeof(T));

        public override T Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.String)
            {
                var str = reader.GetString();
                if (str == null)
                {
                    throw new JsonException($"Cannot convert null to {typeof(T)}");
                }
                return (T)Converter.ConvertFromInvariantString(str)!;
            }

            throw new JsonException($"Cannot convert to {typeof(T)}");
        }

        public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options)
            => writer.WriteStringValue(Converter.ConvertToInvariantString(value));
    }
}

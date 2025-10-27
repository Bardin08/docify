using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Reflection;
using System.Text.Json;

namespace Docify.LLM.Utils;

/// <summary>
/// Complete JSON Schema generator for OpenAI Structured Outputs
/// Supports all OpenAI requirements including nested objects, definitions, and validation
/// </summary>
public static class OpenAiJsonSchemaUtils
{
    private static readonly Dictionary<Type, string> TypeDefinitions = new();
    private static readonly Dictionary<string, object> Definitions = new();
    private static int _nestingLevel;
    private static readonly HashSet<Type> DiscoveredTypes = [];
    private static readonly HashSet<Type> ProcessingTypes = [];
    private static bool _isInDiscoveryPhase = true;

    private static readonly JsonSerializerOptions JsonSerializerOptions = new()
    {
        WriteIndented = true
    };

    /// <summary>
    /// Generate a JSON schema from a generic type
    /// </summary>
    /// <param name="schemaName">Name for the schema (required for OpenAI)</param>
    /// <param name="description">Optional description for the schema</param>
    /// <returns>Complete JSON schema as formatted string</returns>
    public static string GenerateSchema<T>(string schemaName, string? description = null)
    {
        return GenerateSchema(typeof(T), schemaName, description);
    }

    /// <summary>
    /// Generate a complete OpenAI-compatible JSON schema from a C# class type
    /// </summary>
    /// <param name="type">The C# class type to convert</param>
    /// <param name="schemaName">Name for the schema (required for OpenAI)</param>
    /// <param name="description">Optional description for the schema</param>
    /// <returns>Complete JSON schema as formatted string</returns>
    public static string GenerateSchema(Type type, string schemaName, string? description = null)
    {
        if (string.IsNullOrEmpty(schemaName))
            throw new ArgumentException("Schema name is required for OpenAI Structured Outputs", nameof(schemaName));

        // Reset state for new schema generation
        ResetState();

        // Phase 1: Discover all types that need definitions
        _isInDiscoveryPhase = true;
        DiscoverTypes(type, isRoot: true);

        // Phase 2: Create all type definitions
        _isInDiscoveryPhase = false;
        CreateAllDefinitions();

        // Phase 3: Generate the root schema
        var rootSchema = CreateSchemaObject(type, isRoot: true);

        // Add definitions to root schema
        if (Definitions.Count != 0)
        {
            rootSchema["$defs"] = new Dictionary<string, object>(Definitions);
        }

        var schemaDefinition = new Dictionary<string, object>
        {
            ["name"] = schemaName,
            ["strict"] = true,
            ["schema"] = rootSchema
        };

        return JsonSerializer.Serialize(schemaDefinition, JsonSerializerOptions);
    }

    private static void ResetState()
    {
        TypeDefinitions.Clear();
        Definitions.Clear();
        _nestingLevel = 0;
        DiscoveredTypes.Clear();
        ProcessingTypes.Clear();
        _isInDiscoveryPhase = true;
    }

    private static void DiscoverTypes(Type type, bool isRoot = false)
    {
        if (IsNullableType(type))
        {
            var underlyingType = Nullable.GetUnderlyingType(type);
            DiscoverTypes(underlyingType!, false);
            return;
        }

        if (IsSimpleType(type))
            return;

        if (type.IsArray || IsGenericList(type))
        {
            var elementType = type.IsArray ? type.GetElementType()! : type.GetGenericArguments()[0];
            DiscoverTypes(elementType, false);
            return;
        }

        // For complex object types
        if (!isRoot && !DiscoveredTypes.Contains(type) && _isInDiscoveryPhase)
        {
            DiscoveredTypes.Add(type);

            // Assign a definition name
            if (!TypeDefinitions.ContainsKey(type))
            {
                TypeDefinitions[type] = GenerateDefinitionName(type);
            }
        }

        // Avoid infinite recursion
        if (ProcessingTypes.Contains(type))
            return;

        ProcessingTypes.Add(type);

        // Discover types from properties
        foreach (var prop in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (prop is { CanRead: true, CanWrite: true })
            {
                DiscoverTypes(prop.PropertyType, false);
            }
        }

        ProcessingTypes.Remove(type);
    }

    private static void CreateAllDefinitions()
    {
        // Create a copy of the discovered types to avoid modification during enumeration
        var typesToProcess = new List<Type>(DiscoveredTypes);

        foreach (var type in typesToProcess)
        {
            if (!Definitions.ContainsKey(TypeDefinitions[type]))
            {
                var definition = CreateObjectSchemaInternal(type);
                Definitions[TypeDefinitions[type]] = definition;
            }
        }
    }

    private static Dictionary<string, object> CreateSchemaObject(Type type, bool isRoot = false)
    {
        if (IsNullableType(type))
            return CreateNullableTypeSchema(type);

        if (IsSimpleType(type))
            return CreateSimpleTypeSchema(type);

        if (type.IsArray || IsGenericList(type))
            return CreateArraySchema(type);

        return CreateObjectSchema(type, isRoot);
    }

    private static Dictionary<string, object> CreateNullableTypeSchema(Type type)
    {
        var underlyingType = Nullable.GetUnderlyingType(type);
        var underlyingSchema = CreateSchemaObject(underlyingType!);

        return new Dictionary<string, object>
        {
            ["anyOf"] = new object[]
            {
                underlyingSchema,
                new Dictionary<string, object> { ["type"] = "null" }
            }
        };
    }

    private static Dictionary<string, object> CreateSimpleTypeSchema(Type type)
    {
        if (type.IsEnum)
        {
            return CreateEnumSchema(type);
        }

        return type.Name switch
        {
            "String" => new Dictionary<string, object> { ["type"] = "string" },
            "Int32" or "Int64" or "Int16" or "Byte" or "SByte" =>
                new Dictionary<string, object> { ["type"] = "integer" },
            "Double" or "Single" or "Decimal" =>
                new Dictionary<string, object> { ["type"] = "number" },
            "Boolean" => new Dictionary<string, object> { ["type"] = "boolean" },
            "DateTime" => new Dictionary<string, object>
            {
                ["type"] = "string",
                ["format"] = "date-time"
            },
            "DateOnly" => new Dictionary<string, object>
            {
                ["type"] = "string",
                ["format"] = "date"
            },
            "TimeOnly" => new Dictionary<string, object>
            {
                ["type"] = "string",
                ["format"] = "time"
            },
            "TimeSpan" => new Dictionary<string, object>
            {
                ["type"] = "string",
                ["format"] = "duration"
            },
            "Guid" => new Dictionary<string, object>
            {
                ["type"] = "string",
                ["format"] = "uuid"
            },
            _ => new Dictionary<string, object> { ["type"] = "string" }
        };
    }

    private static Dictionary<string, object> CreateEnumSchema(Type type)
    {
        const int maxEnumValuesPerSchema = 500;
        const int maxEnumValuesPerType = 250;
        const int maxEnumValuesCumulativeLength = 7500;

        var enumValues = Enum.GetNames(type);

        if (enumValues.Length > maxEnumValuesPerSchema)
        {
            throw new InvalidOperationException(
                $"Enum {type.Name} has more than 500 values, which exceeds OpenAI limit");
        }

        var totalLength = enumValues.Sum(v => v.Length);
        if (enumValues.Length > maxEnumValuesPerType && totalLength > maxEnumValuesCumulativeLength)
        {
            throw new InvalidOperationException($"Enum {type.Name} exceeds string length limits for large enums");
        }

        return new Dictionary<string, object>
        {
            ["type"] = "string",
            ["enum"] = enumValues
        };
    }

    private static Dictionary<string, object> CreateArraySchema(Type type)
    {
        var elementType = type.IsArray ? type.GetElementType()! : type.GetGenericArguments()[0];

        var schema = new Dictionary<string, object>
        {
            ["type"] = "array",
            ["items"] = CreateSchemaObject(elementType)
        };

        return schema;
    }

    private static Dictionary<string, object> CreateObjectSchema(Type type, bool isRoot = false)
    {
        const int maxNestingLevel = 5;

        if (_nestingLevel >= maxNestingLevel)
        {
            throw new InvalidOperationException($"Maximum nesting depth of 5 exceeded for type {type.Name}");
        }

        // For non-root complex types, return a $ref
        if (!isRoot && TypeDefinitions.TryGetValue(type, out var defName))
        {
            return new Dictionary<string, object>
            {
                ["$ref"] = $"#/$defs/{defName}"
            };
        }

        // For root types, create the schema inline
        return CreateObjectSchemaInternal(type);
    }

    private static Dictionary<string, object> CreateObjectSchemaInternal(Type type)
    {
        const int maxPropertiesPerType = 100;
        var properties = new Dictionary<string, object>();
        var required = new List<string>();
        var totalProperties = 0;

        foreach (var prop in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (prop is not { CanRead: true, CanWrite: true })
                continue;

            totalProperties++;

            if (totalProperties > maxPropertiesPerType)
            {
                throw new InvalidOperationException(
                    $"Maximum of {maxPropertiesPerType} properties exceeded in type {type.Name}");
            }

            var propertyName = GetPropertyName(prop);

            _nestingLevel++;
            properties[propertyName] = CreatePropertySchema(prop);
            _nestingLevel--;

            // ALL fields must be required for OpenAI Structured Outputs
            required.Add(propertyName);
        }

        var schema = new Dictionary<string, object>
        {
            ["type"] = "object",
            ["properties"] = properties,
            ["additionalProperties"] = false,
            ["required"] = required.ToArray()
        };

        var description = GetTypeDescription(type);
        if (!string.IsNullOrEmpty(description))
        {
            schema["description"] = description;
        }

        return schema;
    }

    private static Dictionary<string, object> CreatePropertySchema(PropertyInfo property)
    {
        var schema = CreateSchemaObject(property.PropertyType);

        // If this is a $ref, we cannot add description or constraints directly
        // $ref must be alone in the object
        if (schema.ContainsKey("$ref"))
        {
            return schema; // Return $ref as-is, no additional properties allowed
        }

        // Add description only for non-$ref schemas
        var description = GetPropertyDescription(property);
        if (!string.IsNullOrEmpty(description))
        {
            schema["description"] = description;
        }

        AddConstraints(schema, property);

        return schema;
    }

    private static void AddConstraints(Dictionary<string, object> schema, PropertyInfo property)
    {
        var isString = property.PropertyType == typeof(string) ||
                       (IsNullableType(property.PropertyType) &&
                        Nullable.GetUnderlyingType(property.PropertyType) == typeof(string));

        if (isString)
        {
            AddStringConstraints(schema, property);
        }

        if (IsNumericType(property.PropertyType))
        {
            AddNumericConstraints(schema, property);
        }
    }

    private static void AddNumericConstraints(Dictionary<string, object> schema, PropertyInfo property)
    {
        var range = property.GetCustomAttribute<RangeAttribute>();
        if (range == null) return;

        schema["minimum"] = Convert.ToDouble(range.Minimum);
        schema["maximum"] = Convert.ToDouble(range.Maximum);
    }

    private static void AddStringConstraints(Dictionary<string, object> schema, PropertyInfo property)
    {
        var stringLength = property.GetCustomAttribute<StringLengthAttribute>();
        var minLength = property.GetCustomAttribute<MinLengthAttribute>();
        var maxLength = property.GetCustomAttribute<MaxLengthAttribute>();
        var regularExpression = property.GetCustomAttribute<RegularExpressionAttribute>();

        if (stringLength != null)
        {
            if (stringLength.MinimumLength > 0)
                schema["minLength"] = stringLength.MinimumLength;
            schema["maxLength"] = stringLength.MaximumLength;
        }

        if (minLength != null)
            schema["minLength"] = minLength.Length;

        if (maxLength != null)
            schema["maxLength"] = maxLength.Length;

        if (regularExpression != null)
            schema["pattern"] = regularExpression.Pattern;
    }

    private static bool IsSimpleType(Type type)
    {
        return type.IsPrimitive ||
               type == typeof(string) ||
               type == typeof(DateTime) ||
               type == typeof(DateOnly) ||
               type == typeof(TimeOnly) ||
               type == typeof(TimeSpan) ||
               type == typeof(Guid) ||
               type == typeof(decimal) ||
               type.IsEnum;
    }

    private static bool IsNullableType(Type type)
    {
        return type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Nullable<>);
    }

    private static bool IsGenericList(Type type)
    {
        return type.IsGenericType &&
               (type.GetGenericTypeDefinition() == typeof(List<>) ||
                type.GetGenericTypeDefinition() == typeof(IList<>) ||
                type.GetGenericTypeDefinition() == typeof(ICollection<>) ||
                type.GetGenericTypeDefinition() == typeof(IEnumerable<>));
    }

    private static bool IsNumericType(Type type)
    {
        var underlyingType = IsNullableType(type) ? Nullable.GetUnderlyingType(type) : type;

        return underlyingType == typeof(int) ||
               underlyingType == typeof(long) ||
               underlyingType == typeof(short) ||
               underlyingType == typeof(byte) ||
               underlyingType == typeof(double) ||
               underlyingType == typeof(float) ||
               underlyingType == typeof(decimal);
    }

    private static string GenerateDefinitionName(Type type)
    {
        var baseName = type.Name;
        if (type.IsGenericType)
        {
            baseName = baseName.Split('`')[0];
            var genericArgs = string.Join("", type.GetGenericArguments().Select(t => t.Name));
            baseName += genericArgs;
        }

        var counter = 1;
        var finalName = baseName;
        while (TypeDefinitions.Values.Contains(finalName))
        {
            finalName = $"{baseName}_{counter++}";
        }

        return finalName;
    }

    private static string GetPropertyName(PropertyInfo property)
    {
        var jsonPropertyName = property.GetCustomAttribute<System.Text.Json.Serialization.JsonPropertyNameAttribute>();
        if (jsonPropertyName != null)
        {
            return jsonPropertyName.Name;
        }

        var name = property.Name;
        return char.ToLowerInvariant(name[0]) + name[1..];
    }

    private static string GetPropertyDescription(PropertyInfo property)
    {
        var description = property.GetCustomAttribute<DescriptionAttribute>()?.Description;
        if (string.IsNullOrEmpty(description))
        {
            description = property.GetCustomAttribute<DisplayAttribute>()?.Description;
        }

        return description!;
    }

    private static string GetTypeDescription(Type type)
    {
        return type.GetCustomAttribute<DescriptionAttribute>()?.Description!;
    }
}

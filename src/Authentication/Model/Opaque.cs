#nullable enable

using System;
using System.Buffers;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using CommunityToolkit.Diagnostics;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.Annotations;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Altinn.Platform.Authentication.Model;

/// <summary>
/// An opaque value is a value that can be transmitted to another party
/// without divulging any type information or expectations about the value.
/// </summary>
public static partial class Opaque
{
    /// <summary>
    /// Create a new opaque value.
    /// </summary>
    /// <typeparam name="T">The type of the inner value</typeparam>
    /// <param name="value">The inner value</param>
    /// <returns>A new opaque value.</returns>
    public static Opaque<T> Create<T>(T value) 
        => new(value);

    /// <summary>
    /// Returns the etag type for a <see cref="Opaque{T}"/> type.
    /// </summary>
    /// <param name="opaqueType">The <see cref="Opaque{T}"/> type.</param>
    /// <returns>
    /// The type argument of the <paramref name="opaqueType"/> parameter, 
    /// if the <paramref name="opaqueType"/> is a closed generic request 
    /// opaque type, otherwise <see langword="null"/>.
    /// </returns>
    public static Type? GetUnderlyingType(Type opaqueType)
    {
        Guard.IsNotNull(opaqueType);

        if (opaqueType.IsGenericType && !opaqueType.IsGenericTypeDefinition)
        {
            var genericType = opaqueType.GetGenericTypeDefinition();
            if (ReferenceEquals(genericType, typeof(Opaque<>)))
            {
                return opaqueType.GetGenericArguments()[0];
            }
        }

        return null;
    }
}

/// <summary>
///     <para>
///     An opaque value is a value that can be transmitted to another party
///     without divulging any type information or expectations about the value.
/// </para>
/// <para>
///     Opaque values are typically usefull in APIs where the server wants to
///     be able to return a value that the client later needs to send back to
///     the server, but where the server does not want to expose the type of
///     the value to the client. For instance, in a pagination scenario, the
///     server can use an opaque int to do pagination by page number, while
///     allowing itself to later change the implementation to use a cursor
///     instead of a page number without breaking the API.
/// </para>
/// </summary>
/// <typeparam name="T">The type of the inner value</typeparam>
/// <param name="value">The inner value</param>
[SwaggerSchemaFilter(typeof(Opaque.OpaqueSchemaFilter))]
[JsonConverter(typeof(Opaque.OpaqueJsonConverter))]
public class Opaque<T>(T value)
    : IParsable<Opaque<T>>
    , ISpanParsable<Opaque<T>>
    , IUtf8SpanParsable<Opaque<T>>
{
    /// <summary>
    /// Gets the inner value.
    /// </summary>
    public T Value => value;

    /// <inheritdoc/>
    public override string ToString()
        => Base64UrlEncoder.Encode(JsonSerializer.SerializeToUtf8Bytes(value));

    /// <inheritdoc/>
    public static Opaque<T> Parse(string s, IFormatProvider? provider)
        => Parse(s.AsSpan(), provider);

    /// <inheritdoc/>
    public static Opaque<T> Parse(ReadOnlySpan<char> s, IFormatProvider? provider)
    {
        if (!Opaque<T>.TryParse(s, provider, out var result))
        {
            throw new FormatException($"Failed to parse opaque {typeof(T).FullName}");
        }

        return result;
    }

    /// <inheritdoc/>
    public static bool TryParse([NotNullWhen(true)] string? s, IFormatProvider? provider, [MaybeNullWhen(false)] out Opaque<T> result)
    {
        if (s is null)
        {
            result = null;
            return false;
        }

        return TryParse(s.AsSpan(), provider, out result);
    }

    /// <inheritdoc/>
    public static Opaque<T> Parse(ReadOnlySpan<byte> s, IFormatProvider? provider)
    {
        if (!Opaque<T>.TryParse(s, provider, out var result))
        {
            throw new FormatException($"Failed to parse opaque {typeof(T).FullName}");
        }

        return result;
    }

    /// <inheritdoc/>
    public static bool TryParse(ReadOnlySpan<char> s, IFormatProvider? provider, [MaybeNullWhen(false)] out Opaque<T> result)
    {
        byte[] buff = null!;
        var binaryLength = Base64UrlEncoder.GetMaxDecodedLength(s.Length);
        try 
        {
            buff = ArrayPool<byte>.Shared.Rent(binaryLength);
            if (!Base64UrlEncoder.TryDecode(s, buff, out var written))
            {
                result = null;
                return false;
            }

            var bytes = buff.AsSpan(0, written);
            var inner = JsonSerializer.Deserialize<T>(bytes);
            if (inner is null)
            {
                result = null;
                return false;
            }

            result = new Opaque<T>(inner);
            return true;
        }
        catch (JsonException)
        {
            result = null;
            return false;
        }
        finally
        {
            if (buff is not null)
            {
                ArrayPool<byte>.Shared.Return(buff);
            }
        }
    }

    /// <inheritdoc/>
    public static bool TryParse(ReadOnlySpan<byte> s, IFormatProvider? provider, [MaybeNullWhen(false)] out Opaque<T> result)
    {
        byte[] buff = null!;
        var binaryLength = Base64UrlEncoder.GetMaxDecodedLength(s.Length);
        try
        {
            buff = ArrayPool<byte>.Shared.Rent(binaryLength);
            if (!Base64UrlEncoder.TryDecode(s, buff, out var written))
            {
                result = null;
                return false;
            }

            var bytes = buff.AsSpan(0, written);
            var inner = JsonSerializer.Deserialize<T>(bytes);
            if (inner is null)
            {
                result = null;
                return false;
            }

            result = new Opaque<T>(inner);
            return true;
        }
        catch (JsonException)
        {
            result = null;
            return false;
        }
        finally
        {
            if (buff is not null)
            {
                ArrayPool<byte>.Shared.Return(buff);
            }
        }
    }
}

[SuppressMessage("StyleCop.CSharp.DocumentationRules", "SA1601:Partial elements should be documented", Justification = "Documented earler in the file")]
public static partial class Opaque
{
    /// <summary>
    /// Schema filter for opaque types
    /// </summary>
    [ExcludeFromCodeCoverage]
    internal class OpaqueSchemaFilter
        : ISchemaFilter
    {
        /// <inheritdoc/>
        public void Apply(IOpenApiSchema schema, SchemaFilterContext context)
        {
            if (schema is OpenApiSchema openApiSchema)
            {
                openApiSchema.ExternalDocs = null;
                openApiSchema.Type = JsonSchemaType.String;
            }
        }
    }

    /// <summary>
    /// <see cref="JsonConverterFactory"/> for <see cref="Opaque{T}"/>
    /// </summary>
    internal class OpaqueJsonConverter : JsonConverterFactory
    {
        private ImmutableDictionary<Type, JsonConverter> _converters
            = ImmutableDictionary<Type, JsonConverter>.Empty;

        /// <inheritdoc/>
        public override bool CanConvert(Type typeToConvert)
            => Opaque.GetUnderlyingType(typeToConvert) is not null;

        /// <inheritdoc/>
        public override JsonConverter? CreateConverter(Type typeToConvert, JsonSerializerOptions options)
        {
            if (Opaque.GetUnderlyingType(typeToConvert) is not { } innerType)
            {
                return null;
            }

            return ImmutableInterlocked.GetOrAdd(ref _converters, innerType, CreateConverterInner);
        }

        private static JsonConverter CreateConverterInner(Type opaqueInnerType)
        {
            var converterType = typeof(OpaqueJsonConverter<>).MakeGenericType(opaqueInnerType);
            var converter = converterType
                .GetProperty(nameof(OpaqueJsonConverter<object>.Instance), BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)!
                .GetValue(null)!;
            return (JsonConverter)converter;
        }
    }

    private class OpaqueJsonConverter<T>
        : JsonConverter<Opaque<T>>
        where T : notnull
    {
        public static OpaqueJsonConverter<T> Instance { get; } = new();

        /// <inheritdoc/>
        public override Opaque<T>? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            // Opaque values should never need JSON escaping
            byte[]? buff = null;
            ReadOnlySpan<byte> utf8Data;
            try
            {
                if (reader.HasValueSequence)
                {
                    var seq = reader.ValueSequence;
                    buff = ArrayPool<byte>.Shared.Rent((int)seq.Length);
                    seq.CopyTo(buff);
                    utf8Data = buff.AsSpan(0, (int)seq.Length);
                } 
                else
                {
                    utf8Data = reader.ValueSpan;
                }

                if (!Opaque<T>.TryParse(utf8Data, null, out var result))
                {
                    throw new JsonException("Failed to parse opaque value");
                }

                return result;
            }
            finally
            {
                if (buff is not null)
                {
                    ArrayPool<byte>.Shared.Return(buff);
                }
            }
        }

        /// <inheritdoc/>
        public override void Write(Utf8JsonWriter writer, Opaque<T> value, JsonSerializerOptions options)
        {
            var buff = new ArrayBufferWriter<byte>();
            {
                using var tempWriter = new Utf8JsonWriter(buff);

                // do not use options from outside - Opaque always use the same options
                JsonSerializer.Serialize(tempWriter, value.Value, options: null);
                tempWriter.Flush();
            }

            var span = buff.WrittenSpan;
            var utf8Buff = ArrayPool<byte>.Shared.Rent(Base64UrlEncoder.GetMaxEncodedLength(span.Length));
            try
            {
                if (!Base64UrlEncoder.TryEncode(span, utf8Buff, out var written))
                {
                    throw new JsonException("Failed to encod opaque value");
                }

                writer.WriteStringValue(utf8Buff.AsSpan(0, written));
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(utf8Buff);
            }
        }
    }
}

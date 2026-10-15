#nullable enable

using CommunityToolkit.Diagnostics;

namespace Altinn.Platform.Authentication.Core.Models;

/// <summary>
/// An optional value (similar to <see cref="Nullable{T}"/>, but supports reference types and value types).
/// </summary>
/// <typeparam name="T">The inner type.</typeparam>
public readonly record struct Optional<T>
{
    private readonly T? _value;

    /// <summary>
    /// Gets a value indicating whether this instance has a value.
    /// </summary>
    public bool HasValue { get; }

    /// <summary>
    /// Gets the value if it exists.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown if <see cref="HasValue"/> is <see langword="false"/>.</exception>
    public T Value
    {
        get
        {
            if (!HasValue)
            {
                ThrowHelper.ThrowInvalidOperationException();
            }

            return _value!;
        }
    }

    /// <summary>
    /// Constructs a new instance of <see cref="Optional{T}"/> with a value.
    /// </summary>
    /// <param name="value">The value.</param>
    public Optional(T value)
    {
        _value = value;
        HasValue = true;
    }

    public static implicit operator Optional<T>(T value)
    {
        return new(value);
    }
}

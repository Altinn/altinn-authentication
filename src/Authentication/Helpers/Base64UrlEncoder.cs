#nullable enable

using System;
using System.Buffers;
using System.Buffers.Text;

namespace Altinn.Platform.Authentication.Helpers;

/// <summary>
/// Utility class for base 64 url encoding and decoding using the base64 web safe alphabet.
/// </summary>
internal static class Base64UrlEncoder
{
    private static readonly SearchValues<char> ReplacedValues = SearchValues.Create("-_");
    private static readonly SearchValues<byte> ReplacedValuesUtf8 = SearchValues.Create([(byte)'-', (byte)'_']);

    /// <summary>
    /// Returns the maximum length (in bytes) of the result if you were to decode base 64 encoded text within a char span of size "length".
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when the specified <paramref name="length"/> is less than 0.
    /// </exception>
    public static int GetMaxDecodedLength(int length)
    {
        return Base64.GetMaxDecodedFromUtf8Length(length + 2);
    }

    /// <summary>
    /// Returns the maximum length (in chars) of the result if you were to encode binary data within a byte span of size "length".
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when the specified <paramref name="length"/> is less than 0 or larger than 1610612733 (since encode inflates the data by 4/3).
    /// </exception>
    public static int GetMaxEncodedLength(int length)
    {
        return Base64.GetMaxEncodedToUtf8Length(length);
    }

    /// <summary>
    /// Encode binary data within a byte span into base64 encoded text.
    /// </summary>
    /// <param name="data">The data to encode</param>
    /// <returns>The resulting base64 string</returns>
    public static string Encode(ReadOnlySpan<byte> data)
    {
        var buff = ArrayPool<char>.Shared.Rent(GetMaxEncodedLength(data.Length));
        try
        {
            Convert.TryToBase64Chars(data, buff, out var charsWritten);
            var written = buff.AsSpan(0, charsWritten).TrimEnd('=');
            written.Replace('+', '-');
            written.Replace('/', '_');
            return new string(written);
        }
        finally
        {
            ArrayPool<char>.Shared.Return(buff);
        }
    }

    /// <summary>
    /// Try to encode binary data within a span into base64 encoded utf8 bytes.
    /// </summary>
    /// <param name="data">The data to encode</param>
    /// <param name="destination">The destination to write the utf8 bytes to</param>
    /// <param name="bytesWritten">The number of bytes written</param>
    /// <returns><see langword="true"/> if encoding succeeded, otherwise <see langword="false"/></returns>
    public static bool TryEncode(ReadOnlySpan<byte> data, Span<byte> destination, out int bytesWritten)
    {
        var result = Base64.EncodeToUtf8(data, destination, out var consumed, out var written, isFinalBlock: true);
        if (result != OperationStatus.Done)
        {
            bytesWritten = 0;
            return false;
        }

        if (consumed != data.Length)
        {
            bytesWritten = 0;
            return false;
        }

        var writtenUtf8 = destination[..written].TrimEnd((byte)'=');
        writtenUtf8.Replace((byte)'+', (byte)'-');
        writtenUtf8.Replace((byte)'/', (byte)'_');
        bytesWritten = writtenUtf8.Length;
        return true;
    }

    /// <summary>
    /// Try to decode base 64 encoded text within a char span into a byte span.
    /// </summary>
    /// <param name="encoded">The encoded text</param>
    /// <param name="data">The span of data to write into</param>
    /// <param name="bytesWritten">The number of bytes written</param>
    /// <returns><see langword="true"/> if decoding succeeded, otherwise <see langword="false"/></returns>
    public static bool TryDecode(ReadOnlySpan<char> encoded, Span<byte> data, out int bytesWritten)
    {
        if (encoded.ContainsAny(ReplacedValues) || NeedsPadding(encoded))
        {
            return TryReplaceAndDecode(encoded, data, out bytesWritten);
        }
        else
        {
            return TryDecodeInner(encoded, data, out bytesWritten);
        }
    }

    /// <summary>
    /// Try to decode base 64 encoded text within a utf8 byte span into a byte span.
    /// </summary>
    /// <param name="encoded">The encoded text as utf8 bytes</param>
    /// <param name="data">The span of data to write into</param>
    /// <param name="bytesWritten">The number of bytes written</param>
    /// <returns><see langword="true"/> if decoding succeeded, otherwise <see langword="false"/></returns>
    public static bool TryDecode(ReadOnlySpan<byte> encoded, Span<byte> data, out int bytesWritten)
    {
        if (encoded.ContainsAny(ReplacedValuesUtf8) || NeedsPadding(encoded))
        {
            return TryReplaceAndDecode(encoded, data, out bytesWritten);
        }
        else
        {
            return TryDecodeInner(encoded, data, out bytesWritten);
        }
    }

    private static bool TryReplaceAndDecode(ReadOnlySpan<char> encoded, Span<byte> data, out int bytesWritten)
    {
        var length = encoded.Length;
        var chars = ArrayPool<char>.Shared.Rent(length + 2);
        try
        {
            encoded.CopyTo(chars.AsSpan());
            chars.AsSpan(0, length).Replace('-', '+');
            chars.AsSpan(0, length).Replace('_', '/');

            encoded = PadIfNeeded(chars, length);

            return TryDecodeInner(encoded, data, out bytesWritten);
        }
        finally
        {
            ArrayPool<char>.Shared.Return(chars);
        }
    }

    private static bool TryReplaceAndDecode(ReadOnlySpan<byte> encoded, Span<byte> data, out int bytesWritten)
    {
        var length = encoded.Length;
        var bytes = ArrayPool<byte>.Shared.Rent(length + 2);
        try
        {
            encoded.CopyTo(bytes.AsSpan());
            bytes.AsSpan(0, length).Replace((byte)'-', (byte)'+');
            bytes.AsSpan(0, length).Replace((byte)'_', (byte)'/');

            encoded = PadIfNeeded(bytes, length);

            return TryDecodeInner(encoded, data, out bytesWritten);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(bytes);
        }
    }

    private static bool TryDecodeInner(ReadOnlySpan<char> encoded, Span<byte> data, out int bytesWritten)
    {
        return Convert.TryFromBase64Chars(encoded, data, out bytesWritten);
    }

    private static bool TryDecodeInner(ReadOnlySpan<byte> encoded, Span<byte> data, out int bytesWritten)
    {
        var result = Base64.DecodeFromUtf8(encoded, data, out var consumed, out var written, isFinalBlock: true);
        if (result != OperationStatus.Done)
        {
            bytesWritten = 0;
            return false;
        }

        if (consumed != encoded.Length)
        {
            bytesWritten = 0;
            return false;
        }
        
        bytesWritten = written;
        return true;
    }

    private static bool NeedsPadding(ReadOnlySpan<char> encoded)
    {
        return encoded.Length % 4 != 0;
    }

    private static bool NeedsPadding(ReadOnlySpan<byte> encoded)
    {
        return encoded.Length % 4 != 0;
    }

    private static Span<char> PadIfNeeded(Span<char> chars, int length)
    {
        switch (length % 4)
        {
            case 2:
                chars[length] = '=';
                chars[length + 1] = '=';
                return chars[..(length + 2)];
            case 3:
                chars[length] = '=';
                return chars[..(length + 1)];
            default:
                return chars[..length];
        }
    }

    private static Span<byte> PadIfNeeded(Span<byte> chars, int length)
    {
        switch (length % 4)
        {
            case 2:
                chars[length] = (byte)'=';
                chars[length + 1] = (byte)'=';
                return chars[..(length + 2)];
            case 3:
                chars[length] = (byte)'=';
                return chars[..(length + 1)];
            default:
                return chars[..length];
        }
    }
}

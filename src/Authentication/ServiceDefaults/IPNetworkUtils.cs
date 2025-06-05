using System;
using System.Buffers.Binary;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using CommunityToolkit.Diagnostics;

namespace Altinn.Platform.Authentication.ServiceDefaults;

/// <summary>
/// A util from ServiceDefaults
/// </summary>
internal static class IPNetworkUtils
{
    /// <summary>
    /// IPNetwork.TryParse requires that all the bits of the IP address that are outside the prefix length are set to 0.
    /// This means that for instance the CIDR 10.50.0.15/16 is not valid, because the last octet is not 0. This is too
    /// restrictive for our use case, so we need to implement our own parsing logic. The logic is copied from the source
    /// code for IPNetwork.TryParse, but the parsed IPAddress is truncated to the prefix length.
    /// Note: internal for testing purposes.
    /// </summary>
    /// <param name="s">Readonly span</param>
    /// <param name="network">Network </param>
    /// <param name="address">Address </param>
    /// <returns></returns>
    public static bool TryParseIPNetwork(ReadOnlySpan<char> s, out IPNetwork network, [NotNullWhen(true)] out IPAddress? address)
    {
        var separatorIndex = s.LastIndexOf('/');
        if (separatorIndex >= 0)
        {
            var ipAddressSpan = s[0..separatorIndex];
            var prefixLengthSpan = s[(separatorIndex + 1)..];

            if (IPAddress.TryParse(ipAddressSpan, out address)
                && int.TryParse(prefixLengthSpan, NumberStyles.None, CultureInfo.InvariantCulture, out var prefixLength)
                && prefixLength <= GetMaxPrefixLength(address))
            {
                Debug.Assert(prefixLength >= 0); // Parsing with NumberStyles.None should ensure that prefixLength is always non-negative.
                var prefix = TruncateAddressToPrefixLength(address, prefixLength);

                network = new IPNetwork(prefix, prefixLength);
                return true;
            }
        }

        network = default;
        address = default;
        return false;
    }

    /// <summary>
    /// StartAddress
    /// </summary>
    /// <param name="network">Network </param>
    /// <returns></returns>
    public static IPAddress StartAddress(this IPNetwork network)
        => network.BaseAddress;

    /// <summary>
    /// From 
    /// </summary>
    /// <param name="network">Network</param>
    /// <returns></returns>
    public static IPNetwork From(Microsoft.AspNetCore.HttpOverrides.IPNetwork network)
        => From(network.Prefix, network.PrefixLength);

    /// <summary>
    /// From 
    /// </summary>
    /// <param name="address">Address </param>
    /// <param name="prefixLength">prefix length</param>
    /// <returns></returns>
    public static IPNetwork From(IPAddress address, int prefixLength)
    {
        Guard.IsGreaterThanOrEqualTo(prefixLength, 0);
        Guard.IsLessThanOrEqualTo(prefixLength, GetMaxPrefixLength(address));

        var truncatedAddress = TruncateAddressToPrefixLength(address, prefixLength);
        return new IPNetwork(truncatedAddress, prefixLength);
    }

    /// <summary>
    /// TryFrom
    /// </summary>
    /// <param name="network">Network </param>
    /// <param name="result">Result </param>
    /// <returns></returns>
    public static bool TryFrom(Microsoft.AspNetCore.HttpOverrides.IPNetwork network, out IPNetwork result)
        => TryFrom(network.Prefix, network.PrefixLength, out result);

    /// <summary>
    /// TryFrom
    /// </summary>
    /// <param name="address">Address </param>
    /// <param name="prefixLength">prefixlength </param>
    /// <param name="result">Result </param>
    /// <returns></returns>
    public static bool TryFrom(IPAddress address, int prefixLength, out IPNetwork result)
    {
        if (prefixLength < 0 || prefixLength > GetMaxPrefixLength(address))
        {
            result = default;
            return false;
        }

        result = From(address, prefixLength);
        return true;
    }

    private static int GetMaxPrefixLength(IPAddress address)
        => address.AddressFamily == AddressFamily.InterNetwork ? 32 : 128;

    /// <summary>
    /// EndAddress
    /// </summary>
    /// <param name="network">Network </param>
    /// <returns></returns>
    public static IPAddress EndAddress(this IPNetwork network)
    {
        var address = network.BaseAddress;
        var prefixLength = network.PrefixLength;

        if (address.AddressFamily == AddressFamily.InterNetwork)
        {
            uint value = default;
            var success = address.TryWriteBytes(MemoryMarshal.AsBytes(new Span<uint>(ref value)), out var bytesWritten);
            Debug.Assert(success && bytesWritten == 4); // The address is always 4 bytes long for IPv4 addresses.

            // The cast to long ensures that the mask becomes 0 for the case where 'prefixLength == 0'.
            var mask = (uint)((long)uint.MaxValue << (32 - prefixLength));
            if (BitConverter.IsLittleEndian)
            {
                mask = BinaryPrimitives.ReverseEndianness(mask);
            }

            uint maskedValue = value | ~mask;
            if (maskedValue != value)
            {
                address = new IPAddress(MemoryMarshal.AsBytes(new ReadOnlySpan<uint>(ref maskedValue)));
            }
        }
        else
        {
            UInt128 value = default;
            var success = address.TryWriteBytes(MemoryMarshal.AsBytes(new Span<UInt128>(ref value)), out var bytesWritten);
            Debug.Assert(success && bytesWritten == 16); // The address is always 16 bytes long for IPv6 addresses.

            UInt128 mask = UInt128.MaxValue << (128 - prefixLength);
            if (BitConverter.IsLittleEndian)
            {
                mask = BinaryPrimitives.ReverseEndianness(mask);
            }

            UInt128 maskedValue = value | ~mask;
            if (maskedValue != value)
            {
                address = new IPAddress(MemoryMarshal.AsBytes(new ReadOnlySpan<UInt128>(ref maskedValue)));
            }
        }

        return address;
    }

    private static IPAddress TruncateAddressToPrefixLength(IPAddress address, int prefixLength)
    {
        if (address.AddressFamily == AddressFamily.InterNetwork)
        {
            uint value = default;
            var success = address.TryWriteBytes(MemoryMarshal.AsBytes(new Span<uint>(ref value)), out var bytesWritten);
            Debug.Assert(success && bytesWritten == 4); // The address is always 4 bytes long for IPv4 addresses.

            // The cast to long ensures that the mask becomes 0 for the case where 'prefixLength == 0'.
            var mask = (uint)((long)uint.MaxValue << (32 - prefixLength));
            if (BitConverter.IsLittleEndian)
            {
                mask = BinaryPrimitives.ReverseEndianness(mask);
            }

            uint maskedValue = value & mask;
            if (maskedValue != value)
            {
                address = new IPAddress(MemoryMarshal.AsBytes(new ReadOnlySpan<uint>(ref maskedValue)));
            }
        }
        else
        {
            UInt128 value = default;
            var success = address.TryWriteBytes(MemoryMarshal.AsBytes(new Span<UInt128>(ref value)), out var bytesWritten);
            Debug.Assert(success && bytesWritten == 16); // The address is always 16 bytes long for IPv6 addresses.

            UInt128 mask = UInt128.MaxValue << (128 - prefixLength);
            if (BitConverter.IsLittleEndian)
            {
                mask = BinaryPrimitives.ReverseEndianness(mask);
            }

            UInt128 maskedValue = value & mask;
            if (maskedValue != value)
            {
                address = new IPAddress(MemoryMarshal.AsBytes(new ReadOnlySpan<UInt128>(ref maskedValue)));
            }
        }

        return address;
    }
}
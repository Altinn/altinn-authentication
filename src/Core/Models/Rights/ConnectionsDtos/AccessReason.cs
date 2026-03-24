namespace Altinn.Platform.Authentication.Core.Models.Rights;

/// <summary>
/// Reason for access (Dto)
/// </summary>
public sealed class AccessReason
{
    private readonly AccessReasonFlag flag;

    private IReadOnlyList<AccessReasonRecord>? items;

    public IReadOnlyList<AccessReasonRecord> Items =>
        items ??= AccessReasonMapping.ToRecords(flag);

    internal AccessReason(AccessReasonFlag flag)
    {
        this.flag = flag;
    }

    public AccessReasonFlag ToEnum() => flag;

    public AccessReason Add(AccessReasonFlag additional) =>
        new(flag | additional);

    public AccessReason Remove(AccessReasonFlag remove) =>
        new(flag & ~remove);

    public bool Contains(AccessReasonFlag f) =>
    (flag & f) == f;

    public static AccessReason operator |(
    AccessReason left,
    AccessReasonFlag right)
    {
        return new(left.flag | right);
    }

    public static AccessReason operator |(
        AccessReasonFlag left,
        AccessReason right)
    {
        return new(left | right.flag);
    }

    public static AccessReason operator |(
        AccessReason left,
        AccessReason right)
    {
        return new(left.flag | right.flag);
    }

    public override bool Equals(object? obj)
    {
        if (obj is not AccessReason other)
        {
            return false;
        }

        return flag == other.flag;
    }

    public static bool operator ==(AccessReason left, AccessReason right)
    => left?.flag == right?.flag;

    public static bool operator !=(AccessReason left, AccessReason right)
        => !(left == right);

    public override int GetHashCode()
        => flag.GetHashCode();

    public static implicit operator AccessReason(AccessReasonFlag flag)
        => new(flag);

    public static implicit operator AccessReasonFlag(AccessReason reason)
        => reason.flag;
}

/// <summary>
/// Mapping helper for AccessReason and AccessResonFlag
/// </summary>
public static class AccessReasonMapping
{
    private static readonly Dictionary<AccessReasonFlag, AccessReasonRecord> FlagToItem =
        new()
        {
            {
                AccessReasonFlag.Direct,
                new("direct", "Access granted directly with assignment")
            },
            {
                AccessReasonFlag.ClientDelegation,
                new("client-delegation", "Access granted via client delegation")
            },
            {
                AccessReasonFlag.KeyRole,
                new("keyrole", "Access granted through a key role")
            },
            {
                AccessReasonFlag.RoleMap,
                new("rolemap", "Access granted through role mapping")
            },
            {
                AccessReasonFlag.Parent,
                new("parent", "Access granted through parent party")
            }
        };

    public static AccessReason ToDto(this AccessReasonFlag flag) => new(flag);

    private static readonly AccessReasonFlag KnownMask = FlagToItem.Keys.Aggregate(AccessReasonFlag.None, (current, next) => current | next);

    internal static IReadOnlyList<AccessReasonRecord> ToRecords(AccessReasonFlag flag)
    {
        if (flag == AccessReasonFlag.None)
        {
            return Array.Empty<AccessReasonRecord>();
        }

        var unknownBits = flag & ~KnownMask;
        if (unknownBits != AccessReasonFlag.None)
        {
            throw new InvalidOperationException(
                $"Unknown AccessReasonFlag bits detected: {unknownBits}");
        }

        return FlagToItem
            .Where(kvp => (flag & kvp.Key) == kvp.Key)
            .Select(kvp => kvp.Value)
            .ToList();
    }
}

/// <summary>
/// AccessReason record
/// </summary>
/// <param name="Name">Name</param>
/// <param name="Description">Description</param>
public sealed record AccessReasonRecord(string Name, string Description);

/// <summary>
/// Access reson flags (Internal)
/// </summary>
[Flags]
public enum AccessReasonFlag
{
    /// <summary>
    /// None
    /// </summary>
    None = 0,

    /// <summary>
    /// Access granted directly with assignment
    /// </summary>
    Direct = 1 << 0,

    /// <summary>
    /// Access granted via delegation
    /// </summary>
    ClientDelegation = 1 << 1,

    /// <summary>
    /// Access granted through a key role
    /// </summary>
    KeyRole = 1 << 2,

    /// <summary>
    /// Access granted through rolemapping
    /// </summary>
    RoleMap = 1 << 3,

    /// <summary>
    /// Access granted through parent/child relation
    /// </summary>
    Parent = 1 << 4
}

/*
 AccessReason Pattern

 AccessReasonFlag is the EF/database representation (stored as int).
 AccessReason is a thin wrapper used in DTO/contracts.

 - Implicit conversion exists both ways (Flag ↔ AccessReason).
 - Bitwise operations are performed on the enum.
 - AccessReason exposes display metadata via Items.
 - Unknown flag bits are rejected for safety.

 To add a new reason:
   1. Add a new bit in AccessReasonFlag.
   2. Add corresponding mapping in AccessReasonMapping.

*/

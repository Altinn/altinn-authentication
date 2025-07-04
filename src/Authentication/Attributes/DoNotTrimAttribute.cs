using System;

namespace Altinn.Platform.Authentication.Attributes
{
    /// <summary>
    /// This attribute is used to indicate that a property should not be trimmed of whitespace characters.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property)]
    public class DoNotTrimAttribute : Attribute
    {
    }
}

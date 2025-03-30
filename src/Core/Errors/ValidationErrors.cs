#nullable enable

using Altinn.Authorization.ProblemDetails;

namespace Altinn.Platform.Authentication.Core.Errors;

/// <summary>
/// Validation errors for the Authentication.
/// </summary>
public static class ValidationErrors
{
    private static readonly ValidationErrorDescriptorFactory _factory
        = ValidationErrorDescriptorFactory.New("AUTH");

    /// <summary>
    /// Gets a validation error descriptor for org identifier
    /// </summary>
    public static ValidationErrorDescriptor SystemRegister_InValid_Org_Identifier { get; }
        = _factory.Create(0, "the org number identifier is not valid ISO6523 identifier");

    /// <summary>
    /// Gets a validation error descriptor for invalid system id format
    /// </summary>
    public static ValidationErrorDescriptor SystemRegister_Invalid_SystemId_Format { get; }
        = _factory.Create(1, "The system id does not match the format orgnumber_xxxx...");

    /// <summary>
    /// Gets a validation error descriptor for existing system id
    /// </summary>
    public static ValidationErrorDescriptor SystemRegister_SystemId_Exists { get; }
        = _factory.Create(2, "The system id already exists");

    /// <summary>
    /// Gets a validation error descriptor for non existing resource(rights) id
    /// </summary>
    public static ValidationErrorDescriptor SystemRegister_ResourceId_DoesNotExist { get; }
        = _factory.Create(3, "One or all the resources in rights is not found in altinn's resource register");

    /// <summary>
    /// Gets a validation error descriptor for existing system id
    /// </summary>
    public static ValidationErrorDescriptor SystemRegister_ClientID_Exists { get; }
        = _factory.Create(4, "One of the client id is already tagged with an existing system");

    /// <summary>
    /// Gets a validation error descriptor for invalid redirect url
    /// </summary>
    public static ValidationErrorDescriptor SystemRegister_InValid_RedirectUrlFormat { get; }
        = _factory.Create(5, "One or more of the redirect urls format is not valid. The valid format is https://xxx.xx");

    /// <summary>
    /// Gets a validation error descriptor for duplicate resource(rights) id
    /// </summary>
    public static ValidationErrorDescriptor SystemRegister_ResourceId_Duplicates { get; }
        = _factory.Create(6, "One or more duplicate rights found");

    /// <summary>
    /// Gets a validation error descriptor for duplicate resource(rights) id
    /// </summary>
    public static ValidationErrorDescriptor SystemRegister_AccessPackage_Duplicates { get; }
        = _factory.Create(7, "One or more duplicate access package(s) found");

    /// <summary>
    /// Gets a validation error descriptor for non existing accesspackage id
    /// </summary>
    public static ValidationErrorDescriptor SystemRegister_AccessPackage_NotValid { get; }
        = _factory.Create(8, "One or all the accesspackage(s) is not found in altinn's access packages or is not delegable because they are not part of REGN/REVI/Forretningsfører roller");

    /// <summary>
    /// Gets a validation error descriptor for invalid resource id format
    /// </summary>
    public static ValidationErrorDescriptor SystemRegister_ResourceId_InvalidFormat { get; }
        = _factory.Create(9, "One or more resource id is in wrong format. The vlaid format is urn:altinn:resource");
}

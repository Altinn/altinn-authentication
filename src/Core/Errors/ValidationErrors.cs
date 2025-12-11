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
    /// Gets a validation error descriptor for non existing resource(rights) id or if the resource is not delegable (maskinportenschema, altinn2service)
    /// </summary>
    public static ValidationErrorDescriptor SystemRegister_ResourceId_DoesNotExist { get; }
        = _factory.Create(3, "One or more resources specified in rights were not found in Altinn's resource register.");

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
        = _factory.Create(8, "One or all the accesspackage(s) is not found in altinn's access packages or is not delegable");

    /// <summary>
    /// Gets a validation error descriptor for invalid resource id format
    /// </summary>
    public static ValidationErrorDescriptor SystemRegister_ResourceId_InvalidFormat { get; }
        = _factory.Create(9, "One or more resource id is in wrong format. The valid format is urn:altinn:resource");

    /// <summary>
    /// Gets the validation error descriptor indicating that the request contains duplicate client IDs.
    /// </summary>
    public static ValidationErrorDescriptor SystemRegister_Duplicate_ClientIds { get; }
        = _factory.Create(11, "Request contains duplicate client ids");

    /// <summary>
    /// Gets the validation error descriptor for a mismatch between the system ID in the request body and the system ID
    /// in the URL.
    /// </summary>
    public static ValidationErrorDescriptor SystemId_Mismatch { get; }
        = _factory.Create(12, "The system ID in the request body does not match the system ID in the URL");

    /// <summary>
    /// Gets a validation error descriptor for invalid system id with spaces
    /// </summary>
    public static ValidationErrorDescriptor SystemRegister_Invalid_SystemId_Spaces { get; }
        = _factory.Create(13, "System ID cannot have spaces in id (leading, trailing or in between the id)");

    /// <summary>
    /// Gets a validation error descriptor for system user id that does not exist
    /// </summary>
    public static ValidationErrorDescriptor SystemUser_Missing_SystemUserId { get; }
        = _factory.Create(14, "The agent query parameter is missing or invalid");

    /// <summary>
    /// Gets a validation error descriptor for system user id that does not exist
    /// </summary>
    public static ValidationErrorDescriptor SystemUser_SystemUserId_NotFound { get; }
        = _factory.Create(14, "System user not found");

    /// <summary>
    /// Gets a validation error descriptor for invalid system user id
    /// </summary>
    public static ValidationErrorDescriptor SystemUser_Invalid_SystemUserId { get; }
        = _factory.Create(15, "SystemUser is not a valid system user of type agent");

    /// <summary>
    /// Gets a validation error descriptor for invalid or missing client query parameter
    /// </summary>
    public static ValidationErrorDescriptor SystemUser_Missing_ClientParameter { get; }
        = _factory.Create(16, "The client query parameter is missing or invalid");

    /// <summary>
    /// Gets a validation error descriptor if the resource is of resource type not delegable
    /// </summary>
    public static ValidationErrorDescriptor SystemRegister_ResourceId_NotDelegable { get; }
        = _factory.Create(17, "One or more resources specified in rights is of resource type which is not delegable.");

    /// <summary>
    /// Gets a validation error descriptor when IsVisible is true but access package has IsAssignable false
    /// </summary>
    public static ValidationErrorDescriptor SystemRegister_IsVisible_With_NonAssignable_AccessPackage { get; }
        = _factory.Create(18, "Access packages meant for system user for client relations can't be used in combination with the flag isVisible: true");
}
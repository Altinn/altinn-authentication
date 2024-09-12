namespace Altinn.Platform.Authentication.Core.Models.SystemUsers;

public record CreateRequestSystemUserResponse(
    Guid Id, 
    string ExternalRef, 
    string SystemId, 
    string PartyOrgNo, 
    List<Right> Rights, 
    string Status, 
    string? RedirectURL = default, 
    string? SystemUserId = default);

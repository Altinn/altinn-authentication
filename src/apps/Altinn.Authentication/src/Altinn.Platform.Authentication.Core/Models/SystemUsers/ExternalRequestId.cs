namespace Altinn.Platform.Authentication.Core.Models.SystemUsers;

public record struct ExternalRequestId(string OrgNo, string ExternalRef, string SystemId);

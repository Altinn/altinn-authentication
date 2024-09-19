namespace Altinn.Platform.Authentication.Core.Models.Rights;

public record DelegationCheckResult(bool CanDelegate, List<RightResponses>? RightResponses);
   

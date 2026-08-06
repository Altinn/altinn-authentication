namespace Altinn.Platform.Authentication.Core.Models.Rights;

/// <summary>
/// Dto for list of right keys when calling AM Connections API when delegating resource to systemuser
/// </summary>
public class RightKeyListDto
{
    public IEnumerable<string> DirectRightKeys { get; set; }
}
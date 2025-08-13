namespace Altinn.Platform.Authentication.Core.Models.AccessPackages;
/// <summary>
/// Needed to deserialize list of AccessPackages from the Accessmanagement API
/// </summary>
/// <param name="Links">Pagination links.</param>
/// <param name="Items">The items.</param>
public record PaginatedAccessPackages(
    string Links,
    List<ExternalPackageDTO> Items);


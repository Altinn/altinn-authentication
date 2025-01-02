namespace Altinn.Platform.Authentication.SystemIntegrationTests.Utils;

public class UrlConstants
{
    // API Endpoints changerequest
    public const string ChangeRequestVendorUrl = "v1/systemuser/changerequest/vendor";
    public const string ApproveChangeRequestUrlTemplate = "v1/systemuser/changerequest/{0}/{1}/approve";
    public const string GetRequestByIdUrlTemplate = "v1/systemuser/changerequest/vendor/{0}";
    public const string GetByExternalRefUrlTemplate = "v1/systemuser/changerequest/vendor/byexternalref/{0}/{1}/{2}";
}
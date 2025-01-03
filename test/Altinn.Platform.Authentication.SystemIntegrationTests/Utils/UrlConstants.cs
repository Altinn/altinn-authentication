namespace Altinn.Platform.Authentication.SystemIntegrationTests.Utils;

public class UrlConstants
{
    // API Endpoints SystemRegister
    public const string PostSystemsystemSystemRegister = "v1/systemregister/vendor";
    public const string GetSystemsystemSystemRegister = "v1/systemregister";
    public const string DeleteSystemsystemSystemRegister = "v1/systemregister/vendor";
    
    // API Endpoints SystemUser
    public const string GetSystemUserByPartyIdUrlTemplate = "v1/systemuser/{0}";
    public const string CreateSystemUserRequestBaseUrl = "v1/systemuser/request/vendor";
    public const string GetSystemUserRequestStatusUrlTemplate = "v1/systemuser/request/vendor/{0}";
    public const string ApproveSystemUserRequestUrlTemplate = "v1/systemuser/request/{0}/{1}/approve";
    public const string DeleteSystemUserUrlTemplate = "v1/systemuser/{0}/{1}";
    public const string GetBySystemForVendor = "v1/systemuser/vendor/bysystem/{0}";
    
    // API Endpoints changerequest
    public const string ChangeRequestVendorUrl = "v1/systemuser/changerequest/vendor";
    public const string ApproveChangeRequestUrlTemplate = "v1/systemuser/changerequest/{0}/{1}/approve";
    public const string GetRequestByIdUrlTemplate = "v1/systemuser/changerequest/vendor/{0}";
    public const string GetByExternalRefUrlTemplate = "v1/systemuser/changerequest/vendor/byexternalref/{0}/{1}/{2}";
}
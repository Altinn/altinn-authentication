namespace Altinn.Platform.Authentication.SystemIntegrationTests.Utils
{
    public static class UrlConstants
    {
        // API Endpoints SystemRegister
        public const string PostSystemRegister = "v1/systemregister/vendor";
        public const string GetSystemRegister = "v1/systemregister";
        public const string DeleteSystemRegister = "v1/systemregister/vendor";

        // API Endpoints SystemUser
        public const string GetSystemUserByPartyIdUrlTemplate = "v1/systemuser/{partyId}";
        public const string CreateSystemUserRequestBaseUrl = "v1/systemuser/request/vendor";
        public const string GetSystemUserRequestStatusUrlTemplate = "v1/systemuser/request/vendor/{requestId}";
        public const string ApproveSystemUserRequestUrlTemplate = "v1/systemuser/request/{partyId}/{requestId}/approve";
        public const string DeleteSystemUserUrlTemplate = "v1/systemuser/{partyId}/{systemUserId}";
        public const string GetBySystemForVendor = "v1/systemuser/vendor/bysystem/{systemId}";
        public const string DeleteRequest = "v1/systemuser/request/vendor/{requestId}";
        public const string SystemUserGetByExternalRef = "v1/systemuser/byExternalId";
        public const string GetSystemUserForParty = "v1/systemuser/{party}";
        
        //API Endpoint Maskinporten
        public const string GetByExternalId = "v1/systemuser/byExternalId";

        // API Endpoints changerequest
        public const string ChangeRequestVendorUrl = "v1/systemuser/changerequest/vendor";
        public const string ApproveChangeRequestUrlTemplate = "v1/systemuser/changerequest/{partyId}/{requestId}/approve";
        public const string GetRequestByIdUrlTemplate = "v1/systemuser/changerequest/vendor/{requestId}";
        public const string GetByExternalRefUrlTemplate = "v1/systemuser/changerequest/vendor/byexternalref/{systemId}/{vendor}/{externalRef}";
        public const string GetChangeRequestByRequestIdUrlTemplate = "v1/systemuser/changerequest/vendor/{requestId}";
    }
}
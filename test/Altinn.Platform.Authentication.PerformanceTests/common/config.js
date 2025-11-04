export const api_version = __ENV.API_VERSION || 'v1'; // Default to v1 if not set
export const environment = __ENV.API_ENVIRONMENT || 'yt01'; // Default to yt01 if not set
export const breakpoint = __ENV.breakpoint === 'true' || false; // Default to false if not set
export const stages_duration = (__ENV.stages_duration ?? '1m');
export const stages_target = (__ENV.stages_target ?? '5');
export const abort_on_fail = __ENV.abort_on_fail === 'true' || false; // Default to false if not set


const testBaseUrl = "https://platform.at22.altinn.cloud/authentication/";
const stagingBaseUrl = "https://platform.tt02.altinn.no/authentication/";
const yt01BaseUrl = "https://platform.yt01.altinn.cloud/authentication/";
const testRegisterBaseUrl = "https://platform.at22.altinn.cloud/register/";
const stagingRegisterBaseUrl = "https://platform.tt02.altinn.no/register/";
const yt01RegisterBaseUrl = "https://platform.yt01.altinn.cloud/register/";
const testAmBaseUrl = "https://platform.at22.altinn.cloud/accessmanagement/";
const stagingAmBaseUrl = "https://platform.tt02.altinn.no/accessmanagement/";
const yt01AmBaseUrl = "https://platform.yt01.altinn.cloud/accessmanagement/";

const systemRegister = "api/v1/systemregister/vendor";
const systemUserRequest = "api/v1/systemuser/request/vendor";
const systemUserApprove = "api/v1/systemuser/request/";
const systemUrl = "api/v1/systemregister"
const systemUsersUrl = "api/v1/systemuser/vendor/bysystem/"
const systemUserByExternalIdUrl = "api/v1/systemuser/byExternalId"
const customerList = "api/v1/internal/parties/"
const delegationUrl = "api/v1/systemuser/agent/"
const amDelegationUrl = "api/v1/internal/systemuserclientdelegation"

export const urls = {
    v1: {
        registerSystem: {
            at22: testBaseUrl + systemRegister,
            tt02: stagingBaseUrl + systemRegister,
            yt01: yt01BaseUrl + systemRegister
        },

        requestSystemUser: {
            at22: testBaseUrl + systemUserRequest,
            tt02: stagingBaseUrl + systemUserRequest,
            yt01: yt01BaseUrl + systemUserRequest
        },

        approveSystemUser: {
            at22: testBaseUrl + systemUserApprove,
            tt02: stagingBaseUrl + systemUserApprove,
            yt01: yt01BaseUrl + systemUserApprove
        },
        systemUrl: {
            at22: testBaseUrl + systemUrl,
            tt02: stagingBaseUrl + systemUrl,
            yt01: yt01BaseUrl + systemUrl
        },
        systemUsersUrl: {
            at22: testBaseUrl + systemUsersUrl,
            tt02: stagingBaseUrl + systemUsersUrl,
            yt01: yt01BaseUrl + systemUsersUrl
        },
        systemUserByExternalIdUrl: {
            at22: testBaseUrl + systemUserByExternalIdUrl,
            tt02: stagingBaseUrl + systemUserByExternalIdUrl,
            yt01: yt01BaseUrl + systemUserByExternalIdUrl
        },
        getCustomerList: {
            at22: testRegisterBaseUrl + customerList,
            tt02: stagingRegisterBaseUrl + customerList,
            yt01: yt01RegisterBaseUrl + customerList
        },
        delegationUrl: {
            at22: testBaseUrl + delegationUrl,
            tt02: stagingBaseUrl + delegationUrl,
            yt01: yt01BaseUrl + delegationUrl
        },
        amDelegationUrl: {
            at22: testAmBaseUrl + amDelegationUrl,
            tt02: stagingAmBaseUrl + amDelegationUrl,
            yt01: yt01AmBaseUrl + amDelegationUrl
        }
    }
};

if (!urls[api_version]) {
    throw new Error(`Invalid API version: ${api_version}. Please ensure it's set correctly in your environment variables.`);
}

if (!urls[api_version]["registerSystem"][environment]) {
    throw new Error(`Invalid API environment: ${environment}. Please ensure it's set correctly in your environment variables.`);
}



export const registerSystemUrl = urls[api_version]["registerSystem"][environment];
export const requestSystemUserUrl = urls[api_version]["requestSystemUser"][environment];
export const approveSystemUserUrl = urls[api_version]["approveSystemUser"][environment];
export const getSystemUrl = urls[api_version]["systemUrl"][environment];
export const getSystemUsersUrl = urls[api_version]["systemUsersUrl"][environment];
export const getSystemUserByExternalIdUrl = urls[api_version]["systemUserByExternalIdUrl"][environment];
export const getCustomerListUrl = urls[api_version]["getCustomerList"][environment];
export const postDelegationUrl = urls[api_version]["delegationUrl"][environment];
export const postAmDelegationUrl = urls[api_version]["amDelegationUrl"][environment];
export const tokenGeneratorEnv = environment == "yt01" ? "yt01" : "tt02"; // yt01 is the only environment that has a separate token generator environment

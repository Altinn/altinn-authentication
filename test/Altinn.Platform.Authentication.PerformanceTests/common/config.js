const testBaseUrl = "https://platform.at22.altinn.cloud/authentication/";
const yt01BaseUrl = "https://platform.yt01.altinn.cloud/authentication/";
const testRegisterBaseUrl = "https://platform.at22.altinn.cloud/register/";
const yt01RegisterBaseUrl = "https://platform.yt01.altinn.cloud/register/";
const testAmBaseUrl = "https://platform.at22.altinn.cloud/accessmanagement/";
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
            yt01: yt01BaseUrl + systemRegister
        },

        requestSystemUser: {
            at22: testBaseUrl + systemUserRequest,
            yt01: yt01BaseUrl + systemUserRequest
        },

        approveSystemUser: {
            at22: testBaseUrl + systemUserApprove,
            yt01: yt01BaseUrl + systemUserApprove
        },
        systemUrl: {
            at22: testBaseUrl + systemUrl,
            yt01: yt01BaseUrl + systemUrl
        },
        systemUsersUrl: {
            at22: testBaseUrl + systemUsersUrl,
            yt01: yt01BaseUrl + systemUsersUrl
        },
        systemUserByExternalIdUrl: {
            at22: testBaseUrl + systemUserByExternalIdUrl,
            yt01: yt01BaseUrl + systemUserByExternalIdUrl
        },
        getCustomerList: {
            at22: testRegisterBaseUrl + customerList,
            yt01: yt01RegisterBaseUrl + customerList
        },
        delegationUrl: {
            at22: testBaseUrl + delegationUrl,
            yt01: yt01BaseUrl + delegationUrl
        },
        amDelegationUrl: {
            at22: testAmBaseUrl + amDelegationUrl,
            yt01: yt01AmBaseUrl + amDelegationUrl
        }
    }
};

if (!urls[__ENV.API_VERSION]) {
    throw new Error(`Invalid API version: ${__ENV.API_VERSION}. Please ensure it's set correctly in your environment variables.`);
}

if (!urls[__ENV.API_VERSION]["registerSystem"][__ENV.API_ENVIRONMENT]) {
    throw new Error(`Invalid API environment: ${__ENV.API_ENVIRONMENT}. Please ensure it's set correctly in your environment variables.`);
}

export const registerSystemUrl = urls[__ENV.API_VERSION]["registerSystem"][__ENV.API_ENVIRONMENT];
export const requestSystemUserUrl = urls[__ENV.API_VERSION]["requestSystemUser"][__ENV.API_ENVIRONMENT];
export const approveSystemUserUrl = urls[__ENV.API_VERSION]["approveSystemUser"][__ENV.API_ENVIRONMENT];
export const getSystemUrl = urls[__ENV.API_VERSION]["systemUrl"][__ENV.API_ENVIRONMENT];
export const getSystemUsersUrl = urls[__ENV.API_VERSION]["systemUsersUrl"][__ENV.API_ENVIRONMENT];
export const getSystemUserByExternalIdUrl = urls[__ENV.API_VERSION]["systemUserByExternalIdUrl"][__ENV.API_ENVIRONMENT];
export const getCustomerListUrl = urls[__ENV.API_VERSION]["getCustomerList"][__ENV.API_ENVIRONMENT];
export const postDelegationUrl = urls[__ENV.API_VERSION]["delegationUrl"][__ENV.API_ENVIRONMENT];
export const postAmDelegationUrl = urls[__ENV.API_VERSION]["amDelegationUrl"][__ENV.API_ENVIRONMENT];
export const tokenGeneratorEnv = __ENV.API_ENVIRONMENT == "yt01" ? "yt01" : "tt02"; // yt01 is the only environment that has a separate token generator environment

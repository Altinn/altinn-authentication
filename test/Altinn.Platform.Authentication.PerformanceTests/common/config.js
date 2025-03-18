const testBaseUrl = "https://platform.at22.altinn.cloud/authentication/";
const yt01BaseUrl = "https://platform.yt01.altinn.cloud/authentication/";
const systemRegister = "api/v1/systemregister/vendor";
const systemUserRequest = "api/v1/systemuser/request/vendor";
const systemUserApprove = "api/v1/systemuser/request/";
const systemUrl = "api/v1/systemregister"
const systemUsersUrl = "api/v1/systemuser/vendor/bysystem/"
const systemUserByExternalIdUrl = "api/v1/systemuser/byExternalId"
export const urls = {
    v1: {
        registerSystem: {
            test: testBaseUrl + systemRegister,
            yt01: yt01BaseUrl + systemRegister
        },

        requestSystemUser: {
            test: testBaseUrl + systemUserRequest,
            yt01: yt01BaseUrl + systemUserRequest
        },

        approveSystemUser: {
            test: testBaseUrl + systemUserApprove,
            yt01: yt01BaseUrl + systemUserApprove
        },
        systemUrl: {
            test: testBaseUrl + systemUrl,
            yt01: yt01BaseUrl + systemUrl
        },
        systemUsersUrl: {
            test: testBaseUrl + systemUsersUrl,
            yt01: yt01BaseUrl + systemUsersUrl
        },
        systemUserByExternalIdUrl: {
            test: testBaseUrl + systemUserByExternalIdUrl,
            yt01: yt01BaseUrl + systemUserByExternalIdUrl
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
export const tokenGeneratorEnv = __ENV.API_ENVIRONMENT == "yt01" ? "yt01" : "tt02"; // yt01 is the only environment that has a separate token generator environment

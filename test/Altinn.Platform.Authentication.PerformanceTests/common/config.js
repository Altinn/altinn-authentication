const testBaseUrl = "https://platform.at22.altinn.cloud/authentication/";
const yt01BaseUrl = "https://platform.yt01.altinn.cloud/authentication/";
const systemRegister = "api/v1/systemregister/vendor";
const systemUserRequest = "api/v1/systemuser/request/vendor";
const systemUserApprove = "api/v1/systemuser/request/";
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
export const tokenGeneratorEnv = __ENV.API_ENVIRONMENT == "yt01" ? "yt01" : "tt02"; // yt01 is the only environment that has a separate token generator environment

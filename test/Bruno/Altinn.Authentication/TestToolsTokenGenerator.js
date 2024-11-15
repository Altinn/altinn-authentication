
exports.getToken = async function () {
  const axios = require("axios");
  const btoa = require("btoa");

  const tokenBaseUrl = "https://altinn-testtools-token-generator.azurewebsites.net";
  const basicAuthUser = bru.getEnvVar("tokenBasicAuthUser");
  const basicAuthPw = bru.getEnvVar("tokenBasicAuthPw");
  const Authorization = 'Basic ' + btoa(`${basicAuthUser}:${basicAuthPw}`);

  const tokenEnv = bru.getEnvVar("tokenEnv");
  const tokenType = bru.getEnvVar("auth_tokenType");
  const tokenUser = bru.getEnvVar("auth_userId");
  const tokenParty = bru.getEnvVar("auth_partyId");
  const tokenPid = bru.getEnvVar("auth_ssn");
  const tokenScopes = bru.getEnvVar("auth_scopes");
  const tokenOrg = bru.getEnvVar("auth_org");
  const tokenOrgNo = bru.getEnvVar("auth_orgNo");
  const tokenUsername = bru.getEnvVar("auth_username");

  console.log({
    tokenEnv,
    tokenType,
    tokenUser,
    tokenParty,
    tokenPid,
    tokenScopes,
    tokenOrg,
    tokenOrgNo,
  });

  let tokenUrl;
  if (tokenType == "Personal") {
    tokenUrl = `${tokenBaseUrl}/api/Get${tokenType}Token?env=${tokenEnv}&scopes=${tokenScopes}&pid=${tokenPid}&userid=${tokenUser}&partyid=${tokenParty}&authLvl=3&ttl=3000`;
  }
  else if (tokenType == "Enterprise") {
    tokenUrl = `${tokenBaseUrl}/api/Get${tokenType}Token?env=${tokenEnv}&scopes=${tokenScopes}&org=${tokenOrg}&orgNo=${tokenOrgNo}&ttl=30`;
  }
  else if (tokenType == "EnterpriseUser") {
    tokenUrl = `${tokenBaseUrl}/api/Get${tokenType}Token?env=${tokenEnv}&scopes=${tokenScopes}&orgNo=${tokenOrgNo}&userId=${tokenUser}&partyId=${tokenParty}&userName=${tokenUserName}&ttl=30`;
  } else {
    throw new Error("Unknown tokenType: " + tokenType);
  }

  console.log("tokenUrl:" +  tokenUrl);

  const response = await axios.get(tokenUrl, {
    headers: { Authorization }
  });

  console.log(response.data);
  bru.setVar("bearerToken", response.data);
}

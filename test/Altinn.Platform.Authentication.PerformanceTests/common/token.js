import http from "k6/http";
import encoding from "k6/encoding";
import { tokenGeneratorEnv } from "./config.js";

const tokenUsername = __ENV.TOKEN_GENERATOR_USERNAME;
const tokenPassword = __ENV.TOKEN_GENERATOR_PASSWORD;

const tokenTtl = parseInt(__ENV.TTL) || 3600;
const tokenMargin = 10;

const credentials = `${tokenUsername}:${tokenPassword}`;
const encodedCredentials = encoding.b64encode(credentials);
const tokenRequestOptions = {
  headers: {
    Authorization: `Basic ${encodedCredentials}`,
  },
};

let cachedTokens = {};
let cachedTokensIssuedAt = {};

function getCacheKey(tokenType, tokenOptions) {
  return `${tokenType}|${tokenOptions.scopes}|${tokenOptions.orgName}|${tokenOptions.orgNo}|${tokenOptions.ssn}`;
}

export function fetchToken(url, tokenOptions, type) {
  const currentTime = Math.floor(Date.now() / 1000);  
  const cacheKey = getCacheKey(type, tokenOptions);

  if (!cachedTokens[cacheKey] || (currentTime - cachedTokensIssuedAt[cacheKey] >= tokenTtl - tokenMargin)) {
    
    let response = http.get(url, tokenRequestOptions);

    if (response.status != 200) {
        throw new Error(`Failed getting ${type} token: ${response.status_text}`);
    }
    cachedTokens[cacheKey] = response.body;
    cachedTokensIssuedAt[cacheKey] = currentTime;
  }

  return cachedTokens[cacheKey];
}

export function getEnterpriseToken(serviceOwner) {  
    const tokenOptions = {
        scopes: serviceOwner.scopes, 
        orgNo: serviceOwner.orgno
    }
    const url = `https://altinn-testtools-token-generator.azurewebsites.net/api/GetEnterpriseToken?env=${tokenGeneratorEnv}&scopes=${encodeURIComponent(tokenOptions.scopes)}&orgNo=${tokenOptions.orgNo}&ttl=${tokenTtl}`;
    return fetchToken(url, tokenOptions, `enterprise token (orgno:${tokenOptions.orgNo}, scopes:${tokenOptions.scopes},  tokenGeneratorEnv:${tokenGeneratorEnv})`);
}

export function getEnterpriseTokenWithType(serviceOwner, type) {  
  const tokenOptions = {
      scopes: serviceOwner.scopes, 
      orgNo: serviceOwner.orgno
  }
  const url = `https://altinn-testtools-token-generator.azurewebsites.net/api/GetEnterpriseToken?env=${tokenGeneratorEnv}&scopes=${encodeURIComponent(tokenOptions.scopes)}&orgNo=${tokenOptions.orgNo}&ttl=${tokenTtl}`;
  return fetchToken(url, tokenOptions, `enterprise token (orgno:${tokenOptions.orgNo}, type:${type}, scopes:${tokenOptions.scopes},  tokenGeneratorEnv:${tokenGeneratorEnv})`);
}

  export function getPersonalToken(endUser) {
    const tokenOptions = {
        scopes: endUser.scopes, 
        userId: endUser.userId
    }
    const url = `https://altinn-testtools-token-generator.azurewebsites.net/api/GetPersonalToken?env=${tokenGeneratorEnv}&userId=${tokenOptions.userId}&scopes=${tokenOptions.scopes}&ttl=${tokenTtl}`;
    return fetchToken(url, tokenOptions, `personal token (userId:${tokenOptions.userId}, scopes:${tokenOptions.scopes}, tokenGeneratorEnv:${tokenGeneratorEnv})`);
  }

  export function getAmToken(organization) {
    const tokenOptions = {
        scopes: "altinn:portal/enduser",
        pid: organization.ssn,
        userid: organization.userId,
        partyid: organization.partyId,
        partyuuid: organization.orgUuid
    }
    const url = new URL(`https://altinn-testtools-token-generator.azurewebsites.net/api/GetPersonalToken`);
    url.searchParams.append('env', tokenGeneratorEnv);
    url.searchParams.append('userid', tokenOptions.userid);
    url.searchParams.append('partyuuid', tokenOptions.partyuuid);
    url.searchParams.append('scopes', tokenOptions.scopes);
    url.searchParams.append('ttl', tokenTtl);
    return fetchToken(url.toString(), tokenOptions, `personal token (userId:${tokenOptions.userid}, partyuuid:${tokenOptions.partyuuid}, scopes:${tokenOptions.scopes}, tokenGeneratorEnv:${tokenGeneratorEnv})`);
  }
  
  
  
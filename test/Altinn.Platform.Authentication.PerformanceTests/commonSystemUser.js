import http from 'k6/http';
import encoding from 'k6/encoding';
import { uuidv4, URL} from './common/k6-utils.js';
import { expect, expectStatusFor } from "./common/testimports.js";
import { describe } from './common/describe.js';
import { registerSystemUrl, requestSystemUserUrl, approveSystemUserUrl } from './common/config.js';
import { getCreateSystemBody, getCreateSystemUserBody } from './testdata/postData.js';
import { getPersonalToken, getEnterpriseTokenWithType } from './common/token.js';

const traceCalls = (__ENV.traceCalls ?? 'false') === 'true';

export const createSystemOwnerLabel = "Create system";
export const createSystemUserLabel = "Create system user";
export const approveSystemUserLabel = "Approve system user";

export function createSystem(systemOwner, systemId, resource, token, clientId, type) {
    const params = getParams(createSystemOwnerLabel);
    params.headers.Authorization = "Bearer " + token;
    const url = new URL(registerSystemUrl);
    const body = getCreateSystemBody(systemOwner, systemId, clientId, resource, type);
    let id = null;
    describe('Create system', () => {
        let r = http.post(url.toString(), JSON.stringify(body),params);
        expectStatusFor(r).to.equal(200);
        expect(r, 'response').to.have.validJsonBody();  
        id = r.json();      
    });
    return id;
}

export function createSystemUser(systemId, organization, resources, token, type) {
    const params = getParams(createSystemUserLabel);
    params.headers.Authorization = "Bearer " + token;
    let url = requestSystemUserUrl;
    if (type === "accessPackage") {
        url += "/agent";
    }
    const body = getCreateSystemUserBody(systemId, organization.orgNo, resources, type);
    let id = null;
    describe('Create system user', () => {
        let r = http.post(url, JSON.stringify(body), params);
        expectStatusFor(r).to.equal(201);
        expect(r, 'response').to.have.validJsonBody();
        id = r.json().id;
    });
    return id;
} 

export function approveSystemUser(organization, systemUserId, type) {
    const approveToken = getApproveSystemUserToken(organization.userId);
    const params = getParams(approveSystemUserLabel);
    params.headers.Authorization = "Bearer " + approveToken;
    let url = `${approveSystemUserUrl}${organization.partyId}/${systemUserId}/approve`;
    if (type === 'accessPackage') {
        url = `${approveSystemUserUrl}agent/${organization.partyId}/${systemUserId}/approve`; 
    }  
    describe('Approve system user', () => {
        let r = http.post(url, null, params);
        expectStatusFor(r).to.equal(200);
    });
}

export function getParams(label) {
    const traceparent = uuidv4();
    const params = {
        headers: {
            traceparent: traceparent,
            Accept: 'application/json',
            'Content-Type': 'application/json',
            'User-Agent': 'systembruker-k6',
        },
        tags: { name: label }
    }

    if (traceCalls) {
        params.tags.traceparent = traceparent;
    }
    return params;
}

export function getSystemOwnerTokenAndClientId(systemOwner, iteration) {
    const tokenOptions = {
        scopes: "altinn:authentication/systemregister.write altinn:authentication/systemuser.request.write altinn:authentication/systemuser.request.read altinn:authorization/authorize",
        orgno: systemOwner
    }
    const token = getEnterpriseTokenWithType(tokenOptions, iteration);
    const parts = token.split('.');
    const jwt = JSON.parse(encoding.b64decode(parts[1].toString(), "rawstd", 's'))
    return [token, jwt.client_id];   
}

function getApproveSystemUserToken(userId) {
    const tokenOptions = {
        scopes: "altinn:portal/enduser",
        userId: userId
    }
    return getPersonalToken(tokenOptions);
}


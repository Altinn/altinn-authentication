import http from 'k6/http';
import encoding from 'k6/encoding';
import { 
    expect, 
    uuidv4, 
    URL, 
    describe, 
    getPersonalToken, 
    getEnterpriseToken 
} from "./common/testimports.js";
import { 
    registerSystemUrl, 
    requestSystemUserUrl, 
    approveSystemUserUrl, 
    postDelegationUrl, 
    postAmDelegationUrl,
    stages_duration,
    stages_target,
    breakpoint,
    abort_on_fail,
    environment
} from './common/config.js';
import { getCreateSystemBody, getCreateSystemUserBody } from './testdata/postData.js';
import { getDelegationBody, getAmDelegationBody } from './testdata/postData.js';

const traceCalls = (__ENV.traceCalls ?? 'false') === 'true';

export const tokenGenLabel = "Token generator";
export const createSystemOwnerLabel = "Create system";
export const createSystemUserLabel = "Create system user";
export const approveSystemUserLabel = "Approve system user";
export const postDelegationLabel = "Delegate system user";

export let options = {
    summaryTrendStats: ['avg', 'min', 'med', 'max', 'p(95)', 'p(99)', 'p(99.5)', 'p(99.9)', 'count'],
    thresholds: {
        checks: ['rate>=1.0']
    }
};

export function buildOptions(labels) {
    let options = {
        summaryTrendStats: ['avg', 'min', 'med', 'max', 'p(95)', 'p(99)', 'count'],
        thresholds: {
            checks: ['rate>=1.0'],
            [`http_req_duration{name:${tokenGenLabel}}`]: [],
            [`http_req_failed{name:${tokenGenLabel}}`]: ['rate<=0.0']
        }
    };
    if (breakpoint) {
        for (var label of labels) {
            options.thresholds[[`http_req_duration{name:${label}}`]] = [{ threshold: "max<5000", abortOnFail: abort_on_fail }];
            options.thresholds[[`http_req_failed{name:${label}}`]] = [{ threshold: 'rate<=0.0', abortOnFail: abort_on_fail }];
        }
        //options.executor = 'ramping-arrival-rate';
        options.stages = [
            { duration: stages_duration, target: stages_target },
        ];
    }
    else {
        for (var label of labels) {
            options.thresholds[[`http_req_duration{name:${label}}`]] = [];
            options.thresholds[[`http_req_failed{name:${label}}`]] = ['rate<=0.0'];
        }
    }
    return options;
}

export function createSystem(systemOwner, systemId, resources, token, clientId, type) {
    const params = getParams(createSystemOwnerLabel);
    params.headers.Authorization = "Bearer " + token;
    const url = new URL(registerSystemUrl);
    const body = getCreateSystemBody(systemOwner, systemId, clientId, resources, type);
    let id = null;
    describe('Create system', () => {
        let r = http.post(url.toString(), JSON.stringify(body),params);
        expect(r.status, "response status").to.equal(200);
        expect(r, 'response').to.have.validJsonBody();  
        id = r.json();      
    });
    return id;
}

export function createSystemUser(systemId, organization, resources, token, type, systemResponse) {
    if (!systemResponse) {
        console.log("System response is null");
        return null;
    }
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
        expect(r.status, "response status").to.equal(201);
        expect(r, 'response').to.have.validJsonBody();
        id = r.json().id;
    });
    return id;
} 

export function approveSystemUser(organization, systemUserId, type) {
    if (!systemUserId) {
        console.log("System user id is null");
        return false;
    }
    const approveToken = getApproveSystemUserToken(organization.userId);
    const params = getParams(approveSystemUserLabel);
    params.headers.Authorization = "Bearer " + approveToken;
    let url = `${approveSystemUserUrl}${organization.partyId}/${systemUserId}/approve`;
    if (type === 'accessPackage') {
        url = `${approveSystemUserUrl}agent/${organization.partyId}/${systemUserId}/approve`; 
    } 
    return describe('Approve system user', () => {
        let r = http.post(url, null, params); 
        expect(r.status, "response status").to.equal(200);
        expect(r.body).to.equal("true");
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

export function delegateSystemUser(customer, organization, systemUserId) {
    const params = getParams(postDelegationLabel);
    params.headers.Authorization = "Bearer " + getAmToken(organization);
    const url = `${postDelegationUrl}${organization.partyId}/${systemUserId}/delegation`;
    const body = getDelegationBody(customer.partyUuid, organization.orgUuid);
    let id = null;
    describe('Delegate system user', () => {
        let r = http.post(url, JSON.stringify(body), params);
        expect(r.status, "response status").to.equal(200);
        expect(r, 'response').to.have.validJsonBody();
        id = r.json().map((x) => x.delegationId);
    });
    return id;
}

export function delegateAmSystemUser(customer, organization, systemUserId, resources) {
    const params = getParams(postDelegationLabel);
    params.headers.Authorization = "Bearer " + getAmToken(organization);
    const url = new URL(postAmDelegationUrl);
    url.searchParams.append('party', organization.orgUuid);
    const body = getAmDelegationBody(customer.partyUuid, systemUserId, resources, organization.orgType);
    describe('Delegate system user', () => {
        let r = http.post(url.toString(), JSON.stringify(body), params);
        expect(r.status, "response status").to.equal(200);
        expect(r, 'response').to.have.validJsonBody();
    });
}

export function getSystemOwnerTokenAndClientId(systemOwner, iteration) {
    const tokenOptions = {
        scopes: "altinn:authentication/systemregister.write altinn:authentication/systemuser.request.write altinn:authentication/systemuser.request.read altinn:authorization/authorize",
        orgno: systemOwner
    }
    const token = getEnterpriseToken(tokenOptions, iteration, environment);
    const parts = token.split('.');
    const jwt = JSON.parse(encoding.b64decode(parts[1].toString(), "rawstd", 's'))
    return [token, jwt.client_id];   
}

function getApproveSystemUserToken(userId) {
    const tokenOptions = {
        scopes: "altinn:portal/enduser",
        userId: userId
    }
    return getPersonalToken(tokenOptions, environment);
}

function getAmToken(organization) {
    const tokenOptions = {
        scopes: "altinn:portal/enduser",
        pid: organization.ssn,
        userid: organization.userId,
        partyid: organization.partyId,
        partyuuid: organization.orgUuid
    }
    return getPersonalToken(tokenOptions, environment);
}


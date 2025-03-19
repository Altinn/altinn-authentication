import http from 'k6/http';
import encoding from 'k6/encoding';
import exec from 'k6/execution';
import { randomItem, uuidv4, URL} from './common/k6-utils.js';
import { expect, expectStatusFor } from "./common/testimports.js";
import { describe } from './common/describe.js';
import { splitSystemUsers, resources } from './common/readTestdata.js';
import { getEnterpriseToken, getPersonalToken } from './common/token.js';
import { registerSystemUrl, requestSystemUserUrl, approveSystemUserUrl } from './common/config.js';
import { getCreateSystemBody, getCreateSystemUserBody } from './testdata/postData.js';

const traceCalls = (__ENV.traceCalls ?? 'false') === 'true';

const systemOwner = "713431400"; 
const createSystemOwnerLabel = "Create system";
const createSystemUserLabel = "Create system user";
const approveSystemUserLabel = "Approve system user";
const labels = [createSystemOwnerLabel, createSystemUserLabel, approveSystemUserLabel];

export let options = {
    summaryTrendStats: ['avg', 'min', 'med', 'max', 'p(95)', 'p(99)', 'p(99.5)', 'p(99.9)', 'count'],
    thresholds: {
        checks: ['rate>=1.0']
    }
};
for (var label of labels) {
    options.thresholds[[`http_req_duration{name:${label}}`]] = [];
    options.thresholds[[`http_req_failed{name:${label}}`]] = ['rate<=0.0'];
}

export function setup() {
    const systemUsersParts = splitSystemUsers();
    const [token, clientId] = getSystemOwnerTokenAndClientId(systemOwner);
    const resource = randomItem(resources);
    const systemId = `${systemOwner}_${uuidv4()}`;
    createSystem(systemOwner, systemId, resource, token, clientId);
    const data = {
        systemId: systemId,
        clientId: clientId, 
        token: token,
        resource: resource,
        organizationParts: systemUsersParts
    };
    return data;
}

export default function(data) {
    let mySystemUsers = data.organizationParts[exec.vu.idInTest - 1];
    if (mySystemUsers.length > 0) {
        const organization = randomItem(mySystemUsers);
        data.organizationParts[exec.vu.idInTest - 1] = data.organizationParts[exec.vu.idInTest - 1].filter(item => item.orgNo != organization.orgNo);
        let systemUserId = createSystemUser(data.systemId, organization, data.resource, data.token);
        if (systemUserId) {
            approveSystemUser(organization, systemUserId);
        }
        else {
            console.log("System user not created");
        }
    }
    else {
        console.log("No more system users to create");
    }
}        

function createSystem(systemOwner, systemId, resource, token, clientId) {
    const traceparent = uuidv4();
    const paramsWithToken = {
        headers: {
            Authorization: "Bearer " + token,
            traceparent: traceparent,
            Accept: 'application/json',
            'Content-Type': 'application/json',
            'User-Agent': 'systembruker-k6',
        },
        tags: { name: createSystemOwnerLabel }
    }

    if (traceCalls) {
        paramsWithToken.tags.traceparent = traceparent;
    }

    const url = new URL(registerSystemUrl);
    const body = getCreateSystemBody(systemOwner, systemId, clientId, [resource]);
    let id = null;
    describe('Create system', () => {
        let r = http.post(url.toString(), JSON.stringify(body),paramsWithToken);
        expectStatusFor(r).to.equal(200);
        expect(r, 'response').to.have.validJsonBody();  
        id = r.json();      
    });
    return id;
}

function createSystemUser(systemId, organization, resource, token) {
    const traceparent = uuidv4();
    const paramsWithToken = {
        headers: {
            Authorization: "Bearer " + token,
            traceparent: traceparent,
            Accept: 'application/json',
            'Content-Type': 'application/json',
            'User-Agent': 'systembruker-k6',
        },
        tags: { name: createSystemUserLabel }
    }

    if (traceCalls) {
        paramsWithToken.tags.traceparent = traceparent;
    }

    const url = new URL(requestSystemUserUrl);
    const body = getCreateSystemUserBody(systemId, organization.orgNo, [resource]);
    let id = null;
    describe('Create system user', () => {
        let r = http.post(url.toString(), JSON.stringify(body), paramsWithToken);
        if (r.status != 201) {
            console.log(r.body);
        }
        expectStatusFor(r).to.equal(201);
        expect(r, 'response').to.have.validJsonBody();
        id = r.json().id;
    });
    return id;
} 

function approveSystemUser(organization, systemUserId) {
    const approveToken = getApproveSystemUserToken(organization.userId);
    const traceparent = uuidv4();
    const paramsWithToken = {
        headers: {
            Authorization: "Bearer " + approveToken,
            traceparent: traceparent,
            Accept: 'application/json',
            'Content-Type': 'application/json',
            'User-Agent': 'systembruker-k6',
        },
        tags: { name: approveSystemUserLabel }
    };

    const url = `${approveSystemUserUrl}${organization.partyId}/${systemUserId}/approve`;
    describe('Approve system user', () => {
        let r = http.post(url, null, paramsWithToken);
        expectStatusFor(r).to.equal(200);
    });

}

function getSystemOwnerTokenAndClientId(systemOwner) {
    const tokenOptions = {
        scopes: "altinn:authentication/systemregister.write altinn:authentication/systemuser.request.write altinn:authentication/systemuser.request.read altinn:authorization/authorize",
        orgno: systemOwner
    }
    const token = getEnterpriseToken(tokenOptions);
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

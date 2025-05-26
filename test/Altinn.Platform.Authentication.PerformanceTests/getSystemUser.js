import http from 'k6/http';
import { expect, randomItem, uuidv4, URL, describe, getEnterpriseToken } from "./common/testimports.js";
import { getSystemUrl, getSystemUsersUrl, getSystemUserByExternalIdUrl} from './common/config.js';

const traceCalls = (__ENV.traceCalls ?? 'false') === 'true';

const getSystemsLabel = "Get systems";
const getSystemLabel = "Get system";
const getSystemUsersLabel = "Get system users";
const getSystemUserLabel = "Get system user by external id";
const labels = [getSystemsLabel, getSystemLabel, getSystemUsersLabel, getSystemUserLabel];

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

function getParams(label) {
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

export function setup() {
    return getSystems();
}

export default function(data) {
    const system = randomItem(data);
    const systemId = system.systemId;
    const systemVendorOrgNo = system.systemVendorOrgNumber;
    const [clientId, token] = getSystem(systemId, systemVendorOrgNo);
    const systemUser = getSystemUsers(systemId, token);
    if (systemUser) {
        getSystemUser(systemVendorOrgNo, clientId, systemUser.reporteeOrgNo);
    }
}        

function getSystems() {
    const params = getParams(getSystemsLabel);
    const url = new URL(getSystemUrl);
    let systems = null;
    describe('Get all systems', () => {
        let r = http.get(url.toString(), params);
        expect(r.status, "response status").to.equal(200);
        expect(r, 'response').to.have.validJsonBody();  
        systems = r.json();      
    });
    return systems;
}

function getSystem(systemId, orgno) {
    const params = getParams(getSystemLabel);
    const token = getSystemOwnerToken(orgno);
    params.headers.Authorization = "Bearer " + token;
    const url = new URL(getSystemUrl + "/vendor/" + systemId);
    let system = null;
    describe('Get system details', () => {
        let r = http.get(url.toString(), params);
        expect(r.status, "response status").to.equal(200);
        expect(r, 'response').to.have.validJsonBody();  
        system = r.json();      
    });
    return [system.clientId, token];
}

function getSystemUsers(systemId, token) {
    const params = getParams(getSystemUsersLabel);
    params.headers.Authorization = "Bearer " + token;
    const url = new URL(getSystemUsersUrl + systemId);
    let systemUsers = null;
    describe('Get system users', () => {
        let r = http.get(url.toString(), params);
        expect(r.status, "response status").to.equal(200);
        expect(r, 'response').to.have.validJsonBody();
        systemUsers = r.json();
    });
    return randomItem(systemUsers.data);
}

function getSystemUser(systemProviderOrgNo, clientId, systemUserOwnerOrgNo) {
    const params = getParams(getSystemUserLabel);
    const token = getSystemUserToken(systemProviderOrgNo);
    params.headers.Authorization = "Bearer " + token;
    const url = new URL(getSystemUserByExternalIdUrl);
    url.searchParams.append('systemProviderOrgNo', systemProviderOrgNo);
    url.searchParams.append('clientId', clientId);
    url.searchParams.append('systemUserOwnerOrgNo', systemUserOwnerOrgNo);
    describe('Get system user', () => {
        let r = http.get(url.toString(), params);
        expect(r.status, "response status").to.equal(200);
        expect(r, 'response').to.have.validJsonBody();
    });
}

function getSystemOwnerToken(systemOwner) {
    const tokenOptions = {
        scopes: "altinn:authentication/systemregister.write altinn:authentication/systemuser.request.write altinn:authentication/systemuser.request.read altinn:authorization/authorize",
        orgno: systemOwner
    }
    const token = getEnterpriseToken(tokenOptions);
    return token;   
}

function getSystemUserToken(systemOwner) {
    const tokenOptions = {
        scopes: "altinn:maskinporten/systemuser.read",
        orgno: systemOwner
    }
    const token = getEnterpriseToken(tokenOptions);
    return token;   
}

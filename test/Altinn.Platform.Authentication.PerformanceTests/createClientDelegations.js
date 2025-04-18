import http from 'k6/http';
import exec from 'k6/execution';
import { randomItem, uuidv4, URL} from './common/k6-utils.js';
import { expect, expectStatusFor } from "./common/testimports.js";
import { describe } from './common/describe.js';
import { splitSystemUsers, regnskapsforerUrns, forretningsforerUrns, revisorUrns } from './common/readTestdata.js';
import { getEnterpriseToken } from './common/token.js';
import { getCustomerListUrl, getSystemUsersUrl } from './common/config.js';
import { createSystem, createSystemUser, approveSystemUser, getParams, getSystemOwnerTokenAndClientId, delegateAmSystemUser as delegateSystemUser } from './commonSystemUser.js';
import { createSystemOwnerLabel, createSystemUserLabel, approveSystemUserLabel, postDelegationLabel } from './commonSystemUser.js';
import { options as _options } from './commonSystemUser.js';

const subscription_key = __ENV.subscription_key;

const systemOwner = "713431400"; 

const getCustormerListLabel = "Get customer list";
const getSystemUsersLabel = "Get system users";
const labels = [createSystemOwnerLabel, createSystemUserLabel, approveSystemUserLabel, getCustormerListLabel, getSystemUsersLabel, postDelegationLabel];

export let options = _options;
for (var label of labels) {
    options.thresholds[[`http_req_duration{name:${label}}`]] = [];
    options.thresholds[[`http_req_failed{name:${label}}`]] = ['rate<=0.0'];
}

export function setup() {
    return splitSystemUsers();
}

export default function(data) {
    let mySystemUsers = data[exec.vu.idInTest - 1];
    if (mySystemUsers.length == 0) {
        console.log("No more system users to create");
        return;
    }
    const organization = randomItem(mySystemUsers);
    // Remove the organization from the list
    data[exec.vu.idInTest - 1] = data[exec.vu.idInTest - 1].filter(item => item.orgNo != organization.orgNo);
    const systemId = `${systemOwner}_${uuidv4()}`;

    // get token to create system, systemuser and read systemid.
    // clientId used to create system
    const [token, clientId] = getSystemOwnerTokenAndClientId(systemOwner, __ITER);
    const resources = getPackages(organization.orgType);

        // create system and system user
    const systemResponse = createSystem(systemOwner, systemId, resources, token, clientId, "accessPackage");

    // create system user, approve, get customer list and delegate system user
    const approveSystemUserId = createSystemUser(systemId, organization, resources, token, "accessPackage", systemResponse);
    if (!approveSystemUserId) {
        console.log("System user not created");
        return;
    }
    if (!approveSystemUser(organization, approveSystemUserId, "accessPackage")) {
        console.log("System user not approved");
        return;
    }

    let customerList = getCustomerList(systemOwner, organization.orgUuid, organization.orgType);
    let systemUserId = getSystemUserId(systemId, token);
    if (!systemUserId || !customerList) {
        console.log("No customer list or system user id");
        return;
    }
    let noOfDelegations = 0;
    // delegate system user to 10 customers
    for (let customer of customerList.data) {
        let delegationId = delegateSystemUser(customer, organization, systemUserId, resources);
        noOfDelegations++;
        if (noOfDelegations >=10) {
            break;
        }
    }
}
       

function getCustomerList(systemOwner, orgUuid, orgType) {
    const tokenOptions = {
        scopes: "altinn:register/partylookup.admin",
        orgno: systemOwner
    }
    const token = getEnterpriseToken(tokenOptions);
    const params = getParams(getCustormerListLabel);
    params.headers.Authorization = "Bearer " + token;
    params.headers['Ocp-Apim-Subscription-Key'] = subscription_key;
    const url = `${getCustomerListUrl}${orgUuid}/customers/ccr/${orgType}`;
    let customer_list = null;
    describe('Get customer list', () => {
        let r = http.get(url, params);
        expectStatusFor(r).to.equal(200);
        expect(r, 'response').to.have.validJsonBody();  
        customer_list = r.json();      
    });
    return customer_list;
}

function getSystemUserId(systemId, token) {
    const params = getParams(getSystemUsersLabel);
    params.headers.Authorization = "Bearer " + token;
    const url = new URL(getSystemUsersUrl + systemId);
    let systemUsers = null;
    describe('Get system users', () => {
        let r = http.get(url.toString(), params);
        expectStatusFor(r).to.equal(200);
        expect(r, 'response').to.have.validJsonBody();
        systemUsers = r.json();
    });
    if (systemUsers.data.length > 0) {
        return systemUsers.data[0].id;
    }
    return null;
}

function getPackages(type) {
    if (type == "revisor") {
        return revisorUrns;
    }
    else if (type == "regnskapsforer") {
        return regnskapsforerUrns;
    }
    else {
        return forretningsforerUrns;
    }
}

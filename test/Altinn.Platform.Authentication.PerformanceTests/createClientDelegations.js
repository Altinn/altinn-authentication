import http from 'k6/http';
import encoding from 'k6/encoding';
import exec from 'k6/execution';
import { randomItem, uuidv4, URL} from './common/k6-utils.js';
import { expect, expectStatusFor } from "./common/testimports.js";
import { describe } from './common/describe.js';
import { splitSystemUsers, regnskapsforerUrns, forretningsforerUrns, revisorUrns } from './common/readTestdata.js';
import { getEnterpriseToken, getAmToken } from './common/token.js';
import { getCustomerListUrl, getSystemUsersUrl, postDelegationUrl, postAmDelegationUrl } from './common/config.js';
import { getDelegationBody, getAmDelegationBody } from './testdata/postData.js';
import { createSystem, createSystemUser, approveSystemUser, getParams, createSystemOwnerLabel, createSystemUserLabel, approveSystemUserLabel, getSystemOwnerTokenAndClientId } from './commonSystemUser.js';

const subscription_key = __ENV.subscription_key;

const systemOwner = "713431400"; 

const getCustormerListLabel = "Get customer list";
const getSystemUsersLabel = "Get system users";
const postDelegationLabel = "Delegate system user";
const labels = [createSystemOwnerLabel, createSystemUserLabel, approveSystemUserLabel, getCustormerListLabel, getSystemUsersLabel, postDelegationLabel];

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
    return splitSystemUsers();
}

export default function(data) {
    let mySystemUsers = data[exec.vu.idInTest - 1];
    if (mySystemUsers.length > 0) {
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
        if (systemResponse) {

            // create system user, approve, get customer list and delegate system user
            let approveSystemUserId = createSystemUser(systemId, organization, resources, token, "accessPackage");
            if (approveSystemUserId) {
                approveSystemUser(organization, approveSystemUserId, "accessPackage");
                let customerList = getCustomerList(systemOwner, organization.orgUuid, organization.orgType);
                let systemUserId = getSystemUserId(systemId, token);
                let noOfDelegations = 0;
                if (customerList && systemUserId) {
                    for (let customer of customerList.data) {
                        let delegationId = delegateSystemUser(customer, organization, systemUserId);
                        noOfDelegations++;
                        if (noOfDelegations >=10) {
                            break;
                        }
                        //delegateAmSystemUser(customer, organization, systemUserId, data.resource);
                    }
                }
                else {
                    console.log("No customer list or system user id", customerList, systemUserId);
                }
            }
            else {
                console.log("System user not created");
            }
        }
        else {
            console.log("System not created");
        }
    }
    else {
        console.log("No more system users to create");
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
    console.log(url);
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

function delegateSystemUser(customer, organization, systemUserId) {
    const params = getParams(postDelegationLabel);
    params.headers.Authorization = "Bearer " + getAmToken(organization);
    const url = `${postDelegationUrl}${organization.partyId}/${systemUserId}/delegation`;
    const body = getDelegationBody(customer.partyUuid, organization.orgUuid);
    console.log(body);
    console.log(url); 
    let id = null;
    describe('Delegate system user', () => {
        let r = http.post(url, JSON.stringify(body), params);
        expectStatusFor(r).to.equal(200);
        expect(r, 'response').to.have.validJsonBody();
        id = r.json().map((x) => x.delegationId);
    });
    return id;
}

function delegateAmSystemUser(customer, organization, systemUserId, resource) {
    const params = getParams(postDelegationLabel);
    params.headers.Authorization = "Bearer " + getAmToken(organization);

    const url = new URL(postAmDelegationUrl);
    url.searchParams.append('party', organization.orgUuid);
    const rolePackages = [{ "roleIdentifier": organization.orgType, "packageUrn": resource }];
    const body = getAmDelegationBody(customer.partyUuid, systemUserId, rolePackages);
    describe('Delegate system user', () => {
        let r = http.post(url.toString(), JSON.stringify(body), params);
        expectStatusFor(r).to.equal(200);
        expect(r, 'response').to.have.validJsonBody();
    });
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

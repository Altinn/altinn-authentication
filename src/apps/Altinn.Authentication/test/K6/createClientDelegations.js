import http from 'k6/http';
import exec from 'k6/execution';
import { 
    expect, 
    randomItem, 
    uuidv4, 
    URL, 
    describe, 
    getEnterpriseToken 
} from "./common/testimports.js";
import { regnskapsforerUrns, forretningsforerUrns, revisorUrns, systemOwner } from './common/readTestdata.js';
import { getCustomerListUrl, getSystemUsersUrl } from './common/config.js';
import { 
    createSystem, 
    createSystemUser, 
    approveSystemUser, 
    getParams, 
    getSystemOwnerTokenAndClientId, 
    delegateAmSystemUser as delegateSystemUser,
    createSystemOwnerLabel, 
    createSystemUserLabel, 
    approveSystemUserLabel, 
    postDelegationLabel,
    buildOptions
} from './commonSystemUser.js';
export { splitSystemUsers as setup } from './common/readTestdata.js';

const subscription_key = __ENV.subscription_key;
const environment = __ENV.API_ENVIRONMENT;

const getCustormerListLabel = "Get customer list";
const getSystemUsersLabel = "Get system users";
const labels = [createSystemOwnerLabel, createSystemUserLabel, approveSystemUserLabel, getCustormerListLabel, getSystemUsersLabel, postDelegationLabel];

export let options = buildOptions(labels);

export default function(data) {
    let mySystemUsers = data[exec.vu.idInTest - 1];
    if (mySystemUsers.length == 0) {
        //console.log("No more system users to create");
        exec.test.abort("No more system users to create");
    }
    const organization = randomItem(mySystemUsers);
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
        let _ = delegateSystemUser(customer, organization, systemUserId, resources);
        noOfDelegations++;
        if (noOfDelegations >=1) {
            break;
        }
    }
}
       

function getCustomerList(systemOwner, orgUuid, orgType) {
    const tokenOptions = {
        scopes: "altinn:register/partylookup.admin",
        orgno: systemOwner
    }
    const token = getEnterpriseToken(tokenOptions, 0, environment);
    const params = getParams(getCustormerListLabel);
    params.headers.Authorization = "Bearer " + token;
    params.headers['Ocp-Apim-Subscription-Key'] = subscription_key;
    const url = `${getCustomerListUrl}${orgUuid}/customers/ccr/${orgType}`;
    let customer_list = null;
    describe('Get customer list', () => {
        let r = http.get(url, params);
        expect(r.status, "response status").to.equal(200);
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
        expect(r.status, "response status").to.equal(200);
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

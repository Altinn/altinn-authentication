import exec from 'k6/execution';
import { randomItem, uuidv4 } from './common/k6-utils.js';
import { splitSystemUsers, resources } from './common/readTestdata.js';
import { createSystem, approveSystemUser, createSystemOwnerLabel, createSystemUserLabel, approveSystemUserLabel, getSystemOwnerTokenAndClientId, createSystemUser } from './commonSystemUser.js';



const systemOwner = "713431400"; 
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
    createSystem(systemOwner, systemId, [resource], token, clientId, "resource");
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
        let systemUserId = createSystemUser(data.systemId, organization, [data.resource], data.token, "resource");
        if (systemUserId) {
            approveSystemUser(organization, systemUserId, "resource");
        }
        else {
            console.log("System user not created");
        }
    }
    else {
        console.log("No more system users to create");
    }
}



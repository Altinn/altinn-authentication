import exec from 'k6/execution';
import { randomItem, uuidv4 } from './common/testimports.js';
import { splitSystemUsers, resources } from './common/readTestdata.js';
import { 
    createSystem, 
    approveSystemUser, 
    getSystemOwnerTokenAndClientId, 
    createSystemUser 
} from './commonSystemUser.js';
import { 
    createSystemOwnerLabel, 
    createSystemUserLabel, 
    approveSystemUserLabel, 
} from './commonSystemUser.js';
import { options as _options } from './commonSystemUser.js';

const systemOwner = "713431400"; 
const labels = [createSystemOwnerLabel, createSystemUserLabel, approveSystemUserLabel];

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
    const [token, clientId] = getSystemOwnerTokenAndClientId(systemOwner, __ITER);
    const resource = randomItem(resources);
    const systemId = `${systemOwner}_${uuidv4()}`;
    const systemResponse = createSystem(systemOwner, systemId, [resource], token, clientId, "resource");
    const organization = randomItem(mySystemUsers);
    data[exec.vu.idInTest - 1] = data[exec.vu.idInTest - 1].filter(item => item.orgNo != organization.orgNo);
    let systemUserId = createSystemUser(systemId, organization, [resource], token, "resource", systemResponse);
    approveSystemUser(organization, systemUserId, "resource", systemUserId);
}



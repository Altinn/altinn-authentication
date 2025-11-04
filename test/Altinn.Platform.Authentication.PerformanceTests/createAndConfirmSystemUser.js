import exec from 'k6/execution';
import { randomItem, uuidv4 } from './common/testimports.js';
import { splitSystemUsers, resources, systemOwner } from './common/readTestdata.js';
import { 
    createSystem, 
    approveSystemUser, 
    getSystemOwnerToken, 
    createSystemUser, 
    buildOptions,
    createSystemOwnerLabel, 
    createSystemUserLabel, 
    approveSystemUserLabel, 
} from './commonSystemUser.js';

const labels = [createSystemOwnerLabel, createSystemUserLabel, approveSystemUserLabel];

export let options = buildOptions(labels);

export function setup() {
    return splitSystemUsers();
}

export default function(data) {
    let mySystemUsers = data[exec.vu.idInTest - 1];
    const clientId = uuidv4();
    const token = getSystemOwnerToken(systemOwner);
    const resource = randomItem(resources);
    const systemId = `${systemOwner}_${uuidv4()}`;
    const systemResponse = createSystem(systemOwner, systemId, [resource], token, clientId, "resource");
    const organization = randomItem(mySystemUsers);
    let systemUserId = createSystemUser(systemId, organization, [resource], token, "resource", systemResponse);
    approveSystemUser(organization, systemUserId, "resource", systemUserId);
}



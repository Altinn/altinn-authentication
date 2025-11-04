import http from 'k6/http';
import { SharedArray } from 'k6/data';
import { randomIntBetween, randomItem } from './common/testimports.js';
import { getEnterpriseToken, URL, expect, describe } from './common/testimports.js';
import { getParams, buildOptions } from './commonSystemUser.js';
import { environment } from './common/config.js';
import { readCsv } from './common/readTestdata.js';
//import { organizations, siUsers, endUsers } from './common/readPartiesQueryData.js';

const getPartyLabel = "Get party";
const labels = [getPartyLabel]; 

const runAsDialogporten = (__ENV.runAsDialogporten ?? 'false') === 'true';

const systemOwner = "713431400"; 
const endUsersFilename = './testdata/tt02-users-only.csv';

const endUsers = new SharedArray('endUsers', function () {
  const csv = readCsv(endUsersFilename);
  console.log(`Read ${csv.length} end users from ${endUsersFilename}`);
  return csv;
});

export let options = buildOptions(labels);

export function setup() {
    const data = {
        token: getToken()
    }
    return data;
}

export default function(data) {
    const params = getParams(getPartyLabel);
    params.headers.Authorization = "Bearer " + data.token;
    params.headers['Ocp-Apim-Subscription-Key'] = __ENV.subscription_key;
    const endUser = endUsers[__ITER % endUsers.length].userId;
    const body = createBody(endUser); 
    //const fieldsParams = getFieldsParams();
    const url = new URL('https://platform.tt02.altinn.no/register/api/v1/dialogporten/parties/query');
    //if (fieldsParams) {
    //    url.searchParams.append('fields', fieldsParams);
    //}
    //let orgUuid = null;
    describe('Get party', () => {
        let r = http.post(url.toString(), JSON.stringify(body), params);
        if (r.status !== 200) {
            console.log(r.status, r.status_text);
            console.log(url.toString());
            console.log(JSON.stringify(body));
        }
        expect(r.status, "response status").to.equal(200);
        expect(r, 'response').to.have.validJsonBody();
        const response = r.json();
        console.log(response.data[0].partyId)
        //orgUuid = response.data[0].partyUuid;
    });
}

function createBody(userId) {
    
    return {"data": [userId]};
}

function listOfIdentifiers() {   
    let body = {"data": []};
    for (let i = 0; i < randomIntBetween(1, 100); i++) {
        let org = randomItem(organizations);
        let siUser = randomItem(siUsers);
        let user = randomItem(endUsers);
        let identifier = randomItem(identifiers);
        switch (identifier) {
            case "urn:altinn:party:id":
                body.data.push(identifier + ":" + org.id);
                break;
            case "urn:altinn:party:uuid":
                body.data.push(identifier + ":" + org.uuid);
                break;
            case "urn:altinn:organization:identifier-no":
                body.data.push(identifier + ":" + org.organization_identifier);
                break;
            case "urn:altinn:person:identifier-no":
                body.data.push(identifier + ":" + user.person_identifier);
                break;
            case "urn:altinn:user:id":
                if (i %2 === 0) {
                    body.data.push(identifier + ":" + siUser.user_id);
                } else {
                    body.data.push(identifier + ":" + user.user_id);
                }
                break;
        }
    }
    return body;
}

function getFieldsParams() {
    if (runAsDialogporten) {
        return "identifiers,display-name";
    }
    const num = randomIntBetween(1, 100);
    if (num <= 10) {
        return null; // No fields requested
    }   
    if (num <= 50) {
        return fields.join(',');
    } else {
        return randomItem(fields);
    }
}

function getToken() {   
    const tokenOptions = {
        scopes: "altinn:register/partylookup.admin",
        orgno: systemOwner
    }
    const token = getEnterpriseToken(tokenOptions, environment);
    return token;   
}

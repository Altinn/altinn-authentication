//

import { uuidv4 } from "../common/testimports.js";

export function getCreateSystemBody(systemOwner, systemId, clientId, resources, type) {
    let body = {
        "id": systemId,
        "vendor": {
          "ID": "0192:" + systemOwner,
        },
        "name": {
          "en": "perftest",
          "nb": "perftest",
          "nn": "string"
        },
        "description": {
          "en": "test",
          "nb": "test",
          "nn": "test"
        },
        "rights": [
          {
            
            "resource": [
              
            ]
          }
        ],
        "clientId": [
            clientId
        ],
        "isVisible": true,
        "allowedRedirectUrls": [ "https://digdir.no"
        ]
      };
    if (type === "accessPackage") {
      body.accessPackages = [];
      for (var resource of resources) {
          body.accessPackages.push({
            "urn": resource,
          });
      }     
    }
    else {
      for (var resource of resources) {
          body.rights[0].resource.push({
              "id": "urn:altinn:resource",
              "value": resource
          });
      }
    }
    return body;

}

export function getCreateSystemUserBody(systemId, partyOrgNo, resources, type) {
    const body = {
        "systemId": systemId,
        "partyOrgNo":partyOrgNo,
        "rights": [
          {
            "resource": [

            ]
          }
          ],
          //"accessPackages": [],
          "redirectUrl": ""
      };
      if (type === "accessPackage") {
        body.accessPackages = [];
        for (var resource of resources) {
            body.accessPackages.push({
              "urn": resource,
            });
        }     
      }
      else {
        for (var resource of resources) {
            body.rights[0].resource.push({
                "id": "urn:altinn:resource",
                "value": resource
            });
        }
      }
    return body;
}

export function getDelegationBody(customerPartyId, facilitatorId) {
    const body = {
      "customerId": customerPartyId,   
      "facilitatorId": facilitatorId
    };
    return body;
}

export function getAmDelegationBody(clientId, agentId, resources, orgtype) {
    const body = {
      "clientId": clientId,
      "agentId": agentId,
      "agentName": "Performance test",
      "agentRole": "agent",
      "rolePackages": []
    };

    for (var resource of resources) {
        body.rolePackages.push({
            "roleIdentifier": orgtype,
            "packageUrn": resource
        });
    } 

    return body;
}

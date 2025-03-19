//

export function getCreateSystemBody(systemOwner, systemId, clientId, resources) {
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
    for (var resource of resources) {
        body.rights[0].resource.push({
            "id": "urn:altinn:resource",
            "value": resource
        });
    }
    return body;

}

export function getCreateSystemUserBody(systemId, partyOrgNo, resources) {
    const body = {
        "systemId": systemId,
        "partyOrgNo":partyOrgNo,
        "rights": [
          {
            "resource": [

            ]
          }
          ],
          "redirectUrl": ""
      };
    for (var resource of resources) {
        body.rights[0].resource.push({
            "id": "urn:altinn:resource",
            "value": resource
        });
    };
    return body;
}

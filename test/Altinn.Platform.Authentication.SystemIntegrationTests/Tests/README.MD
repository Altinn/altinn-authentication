# 🔐 Klientdelegerings

## 🧪 Test User for frontend

- **Environment:** AT22
- **Login:** `06857897380`
- **Select Actor:** `314250052`

---

## 🧪 Test Scenario – Klientdelegering (Client Delegation)

### 1. Opprette system for leverandør (f.eks. Visma)

**SystemId-format:** `Regnskapssystem_{OrgNr}`  
Eksempel: `Regnskapssystem_98241033911`

**Endpoint:**


/POST 

**Request body:**
```json
{
  "Id": "98241033911_{Name}",
  "Vendor": {
    "ID": "0192:{vendorId}"
  },
  "Name": {
    "en": "{Name}",
    "nb": "{Name}",
    "nn": "{Name}"
  },
  "Description": {
    "en": "Integration test for authentication",
    "nb": "Integrasjonstest for authentication",
    "nn": "integrasjonstest for authentication"
  },
  "accessPackages": [
    {
      "urn": "urn:altinn:accesspackage:regnskapsforer-lonn"
    }
  ],
  "allowedRedirectUrls": [
    "https://sluttbrukersystem.no/relevantpath"
  ],
  "isVisible": true,
  "ClientId": [
    "{maskinportenKlientId}"
  ]
}
```
# Opprett forespørsel
```
{
"externalRef": "{externalRef}",
"systemId": "{systemId}",
"partyOrgNo": "{partyOrgNo}",
"accessPackages": [
{
"urn": "urn:altinn:accesspackage:regnskapsforer-lonn"
}
],
"redirectUrl": "https://sluttbrukersystem/relevantpath?systemId=312605031_183a7773-a6bc-4965-b4f7-d4d12d288941"
}
```
### Respons
```
{
"confirmUrl": "https://am.ui.at22.altinn.cloud/accessmanagement/ui/systemuser/agentrequest?id=8d4ba51e-e73d-47d9-b612-4cbdb5e85131"
}
```


# Godkjenne i frontend
- Frontend henter kundelista fra Registeret via BFF:
```'GET /accessmanagement/api/v1/systemuser/agentdelegation/{facilitatorPartyId}/{systemUserId}/{accessPackageUrn}/customers```

# Utfør delegering
- ``` POST /v1/systemuser/agent/{customerPartyId}/{systemUserId}/delegation ```
Fra GUI
```
{
"customerId": "{customerPartyId}",    //Baker Hansen sitt party uuid (organisasjons partyUUID)
"facilitatorId": "{AltinnPartyUuid}"// Regnskapsfører AS - partyUUID til orgnummeret
}
```
som skal mappes til:
```
{
  "clientId": "f1c7ca59-5bf9-4036-bdb8-15062d992eaa",
  "agentId": "41856122-b68c-4451-b7bf-67c8279169ab",
  "agentName": "Gjenskape delegation for Authentication",
  "agentRole": "agent",
  "rolePackages": [
    {
      "roleIdentifier": "regnskapsforer",
      "packageUrn": "urn:altinn:accesspackage:regnskapsforer-lonn"
    }
  ]
}
```
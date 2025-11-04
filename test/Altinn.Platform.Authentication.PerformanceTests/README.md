# Authentication performance tests
This directory holds the performance tests for authentication systemuser (systembruker). Currently we have tests for creating a system and systemusers and for reading system and systemusers.

## Prerequisites
* Either
  * [Grafana K6](https://k6.io/) must be installed and `k6` available in `PATH` 
  * or Docker (available av `docker` in `PATH`)
* Powershell or Bash (should work on any platform supported by K6)

## Tests
### Create and confirm systemuser
File: 
1. Create system
2. Create systemuser on system
3. Confirm systemuser
### Get systemuser
File: `getSystemUser.js`
1. Get systems
2. Get system for a random system
3. Get systemusers for system
4. Get systemuser for a random systemuser 
### Create client delegations
File: `createClientDelegations.js`
1. Create system
2. Create systemuser on system
3. Confirm systemuser
4. Delegate systemuser
### Parties query
File: `partiesQuery.js`
1. Create randomized request body
2. Call get parties with request body

## Test data
File: `testdata/data-<env>-all-customers.csv` 

Testdatafile used by `createAndConfirmSystemUsers.js` and `createClientDelegations.js` 

| Attibute | Description | In use |
|----------| ------------| -------|
| orgNo    | organisation number as in BR |
| partyId | The organisations party id |
| ssn | DAGL for the organisation |
| userId  | the user id of the ssn |
| userPartyId | the party id of the ssn |
| orgUuid | the uuid for the organisation |
| orgType | the type of customers the organisation has |




## Run test
### From cli
1. Navigate to the following directory:
```shell
cd test/Altinn.Platform.Authentication.PerformanceTests
```
2. Run the test using the following command. Replace `<test file>`, `<vus>` and `<duration>` with the desired values:
```shell
TOKEN_GENERATOR_USERNAME=<username> TOKEN_GENERATOR_PASSWORD=<passwd> \
k6 run <test file> -e API_VERSION=v1 \
-e API_ENVIRONMENT=yt01 \
--vus=<vus> --duration=<duration>
```
3. Refer to the k6 documentation for more information on usage.

### From GitHub Actions
To run the performance test using GitHub Actions, follow these steps:
1. Go to the [GitHub Actions](https://github.com/altinn/altinn-authentication/actions/workflows/run-performance.yml) page.
2. Select "Run workflow" and fill in the required parameters.
3. Tag the performance test with a descriptive name.

### GitHub Action with act
To run the performance test locally using GitHub Actions and act, perform the following steps:
1. [Install act](https://nektosact.com/installation/).
2. Navigate to the root of the repository.
3. Create a `.secrets` file that matches the GitHub secrets used. Example:
```file
TOKEN_GENERATOR_USERNAME:<username>
TOKEN_GENERATOR_PASSWORD:<passwd>
```
    Replace `<username>` and `<passwd>`, same as for generating tokens above. 
##### IMPORTANT: Ensure this file is added to .gitignore to prevent accidental commits of sensitive information. Never commit actual credentials to version control.
4. Run `act` using the command below. Replace `<path-to-testscript>`, `<vus>` and `<duration>` with the desired values:
```shell
act workflow_dispatch -j k6-performance -s GITHUB_TOKEN=`gh auth token` \
--container-architecture linux/amd64 --artifact-server-path $HOME/.act \ 
--input vus=<vus> --input duration=<duration> \ 
--input testSuitePath=<path-to-testscript>
```

## Test Results
Test results can be found in GitHub action run log, grafana and in App Insights. 

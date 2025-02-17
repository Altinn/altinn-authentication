## System integration test / to end tests

### How to

Ask someone on the project, e.g., @Nyeng, for the following config files: 
- Resources/Environment/environment.json
- Run `dotnet` test to execute the tests

#### Debugging workflow using act
- `act -j build-and-run-integration-tests --container-architecture linux/amd64 -s SYSTEMINTEGRATIONTEST_JSON="$SYSTEMINTEGRATIONTEST_JSON"`

### Nice to know
- To run tests in other environments, the resource used in resource registry must be added there, or "someone" must api support in the tests to post directly if resource does not already exist
- To add other Maskinporten clients, use this project: https://github.com/Altinn/altinn-authorization-utils to generate a 
    - `altinn-jwks create authentication-systemintegration-tests`
    - add the generated jwk here: https://sjolvbetjening.test.samarbeid.digdir.no and add add the jwk
    - in Dev: use https://sjolvbetjening.samarbeid.digdir.dev






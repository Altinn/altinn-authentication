## System integration test / to end tests

### How to

Ask someone on the project, e.g., @Nyeng, for the following config files: 
- Resources/Environment/environment.json
- Resources/Jwks/jwks.json
- Run `dotnet` test to execute the tests

### Nice to know
- To run tests on other environments, the resource used in resource registry must be added there, or "someone" must api support in the tests to post directly if resource does not already exist
- To add other Maskinporten clients, use this project: https://github.com/Altinn/altinn-authorization-utils to generate a 
    - `altinn-jwks create authentication-systemintegration-tests`
    - add the generated jwk here: https://sjolvbetjening.test.samarbeid.digdir.no and add add the jwk locally to the
      folder Resources/Jwks/ + client info in environment.json






# Altinn Platform Authentication

## Build status
[![Storage build status](https://dev.azure.com/brreg/altinn-studio/_apis/build/status/altinn-platform/authentication-master?label=platform/authentication)](https://dev.azure.com/brreg/altinn-studio/_build/latest?definitionId=41)


## Getting Started

These instructions will get you a copy of the authentication component up and running on your machine for development and testing purposes.

### Prerequisites

1. [.NET 6.0 SDK](https://dotnet.microsoft.com/download/dotnet/6.0)
2. Code editor of your choice
3. Newest [Git](https://git-scm.com/downloads)
4. [Docker CE](https://www.docker.com/get-docker)
5. Solution is cloned


## Running the storage component

### In a docker container

Clone [Altinn Authentication repo](https://github.com/Altinn/altinn-authentication) and navigate to the root folder.

```cmd
docker-compose up -d --build
```

### With .NET

The Authentication components can be run locally when developing/debugging. Follow the install steps above if this has not already been done.

Stop the container running Authentication

```cmd
docker stop altinn-authentication
```

Navigate to the src/Authentication, and build and run the code from there, or run the solution using you selected code editor

```cmd
dotnet run
```

The Authentication component is now available locally at http://localhost:5040/api/v1

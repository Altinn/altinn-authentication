FROM mcr.microsoft.com/dotnet/sdk:10.0-alpine@sha256:d17d8d6511aee3dd54b4d9e8e03d867b1c3df28fb518ee9dd69e50305b7af4ee AS build
WORKDIR /app

# Copy everything and build
COPY . .
RUN dotnet build ./src/Authentication/Altinn.Platform.Authentication.csproj -c Release -o app_output \
    && dotnet publish ./src/Authentication/Altinn.Platform.Authentication.csproj -c Release -r linux-x64 -o app_output --no-self-contained

FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine@sha256:049f2d7d7acfcbf09e1d15eb4faccec6453b0a98f0cb54d53bcbdc3ed91e96c8 AS final
EXPOSE 5040 
WORKDIR /app
COPY --from=build /app/app_output .

COPY src/Persistance/Migration /app/Persistance/Migration 

# setup the user and group
# the user will have no password, using shell /bin/false and using the group dotnet
RUN addgroup -g 3000 dotnet && adduser -u 1000 -G dotnet -D -s /bin/false dotnet
# update permissions of files if neccessary before becoming dotnet user
USER dotnet
RUN mkdir /tmp/logtelemetry

ENTRYPOINT ["dotnet", "Altinn.Platform.Authentication.dll"]

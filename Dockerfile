FROM mcr.microsoft.com/dotnet/sdk:10.0-alpine@sha256:7d98d5883675c6bca25b1db91f393b24b85125b5b00b405e55404fd6b8d2aead AS build
WORKDIR /app

# Copy everything and build
COPY . .
RUN dotnet build ./src/Authentication/Altinn.Platform.Authentication.csproj -c Release -o app_output \
    && dotnet publish ./src/Authentication/Altinn.Platform.Authentication.csproj -c Release -r linux-x64 -o app_output --no-self-contained

FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine@sha256:8e21337e482e353c958681789872b3451e966e07c259b9a6f9a8b7902749a785 AS final
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

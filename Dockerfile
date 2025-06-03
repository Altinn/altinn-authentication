FROM mcr.microsoft.com/dotnet/sdk:9.0-alpine@sha256:2244f80ac7179b0feaf83ffca8fe82d31fbced5b7e353755bf9515a420eba711 AS build
WORKDIR /app

# Copy everything and build
COPY . .
RUN dotnet build ./src/Authentication/Altinn.Platform.Authentication.csproj -c Release -o app_output \
    && dotnet publish ./src/Authentication/Altinn.Platform.Authentication.csproj -c Release -r linux-x64 -o app_output --no-self-contained

FROM mcr.microsoft.com/dotnet/aspnet:9.0-alpine@sha256:30fdbd1b5963bba6ed66190d72d877b750d4203a671c9b54592f4551b8c5a087 AS final
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

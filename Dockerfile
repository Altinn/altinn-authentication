FROM mcr.microsoft.com/dotnet/sdk:10.0-alpine@sha256:2073294f05c2676f2e11b1782188654435e77e28adaed77a1138b0ca4693dd3b AS build
WORKDIR /app

# Copy everything and build
COPY . .
RUN dotnet build ./src/Authentication/Altinn.Platform.Authentication.csproj -c Release -o app_output \
    && dotnet publish ./src/Authentication/Altinn.Platform.Authentication.csproj -c Release -r linux-x64 -o app_output --no-self-contained

FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine@sha256:27b6b84beeede74fd16886177d360799c8e4299ceadfbd64eef57bafead7878a AS final
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

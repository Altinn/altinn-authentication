FROM mcr.microsoft.com/dotnet/sdk:8.0.204-alpine3.18 AS build
WORKDIR /app


COPY src/Authentication/Altinn.Platform.Authentication.csproj ./src/Authentication/Altinn.Platform.Authentication.csproj
COPY src/Core/Altinn.Platform.Authentication.Core.csproj ./src/Core/Altinn.Platform.Authentication.Core.csproj
COPY src/Persistance/Altinn.Platform.Authentication.Persistance.csproj ./src/Persistance/Altinn.Platform.Authentication.Persistance.csproj

RUN dotnet restore ./src/Authentication/Altinn.Platform.Authentication.csproj

COPY src/ ./src
RUN dotnet build ./src/Authentication/Altinn.Platform.Authentication.csproj -c Release -o app_output \
    && dotnet publish ./src/Authentication/Altinn.Platform.Authentication.csproj -c Release -r linux-x64 -o app_output --no-self-contained

FROM mcr.microsoft.com/dotnet/aspnet:8.0.5-alpine3.18 AS final
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

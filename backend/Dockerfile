FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY UserMgmt.Api/*.csproj UserMgmt.Api/
RUN dotnet restore UserMgmt.Api/UserMgmt.Api.csproj
COPY UserMgmt.Api/ UserMgmt.Api/
RUN dotnet publish UserMgmt.Api/UserMgmt.Api.csproj -c Release -o /app

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app .
COPY entrypoint.sh .
RUN chmod +x entrypoint.sh
ENV DOTNET_HOSTBUILDER__RELOADCONFIGONCHANGE=false
ENV DOTNET_USE_POLLING_FILE_WATCHER=true
ENV DOTNET_SYSTEM_NET_DISABLEIPV6=1
EXPOSE 8080
ENTRYPOINT ["./entrypoint.sh"]

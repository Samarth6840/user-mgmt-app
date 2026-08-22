#!/bin/sh
set -e
export ASPNETCORE_URLS="http://+:${PORT:-8080}"
exec dotnet UserMgmt.Api.dll

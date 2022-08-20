#!/usr/env bash
set -euo pipefail

dotnet new sln --force -n duplicate-file-finder
dotnet sln duplicate-file-finder.sln add ./src/**/*.csproj

#!/bin/bash
set -e  # Stop script on any error
set -x  # Echo each command to the terminal

dotnet tool install --global dotnet-ef --version 8
dotnet restore WriteFluencyApi
dotnet ef database update -p ./WriteFluencyApi/src/WriteFluency.Infrastructure/WriteFluency.Infrastructure.csproj -s ./WriteFluencyApi/src/WriteFluency.WebApi/WriteFluency.WebApi.csproj
dotnet dev-certs https
cd WriteFluencyApp
npm install

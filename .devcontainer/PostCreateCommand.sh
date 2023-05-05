dotnet tool install --global dotnet-ef --version 7.0.5
dotnet restore WriteFluencyApi/WriteFluencyApi.csproj
dotnet ef database update --project WriteFluencyApi/WriteFluencyApi.csproj
dotnet dev-certs https
cd WriteFluencyApp
npm install
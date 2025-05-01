dotnet tool install --global dotnet-ef --version 7.0.5
dotnet restore WriteFluencyApi/WriteFluency.WebApi.csproj
dotnet ef database update --project WriteFluencyApi/WriteFluency.WebApi.csproj
dotnet dev-certs https
cd WriteFluencyApp
npm install
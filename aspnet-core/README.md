## About this solution

This is a minimalist, non-layered startup solution with the ABP Framework. All the fundamental ABP modules are already installed.

## How to run

The application needs to connect to a database. Run the following command in the `WriteFluency` directory:

````bash
dotnet run --migrate-database
````

This will create and seed the initial database. Then you can run the application with any IDE that supports .NET.

## Run postgres image on docker to use as local database

````bash
docker run --name wfdb -p 5432:5432 -e POSTGRES_PASSWORD=postgres postgres
````

Happy coding..!




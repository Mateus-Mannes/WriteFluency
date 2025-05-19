# WriteFluency
WriteFluency is an interactive web application designed to help users practice and improve their English writing skills.

# About the code
This repo includes:
- WriteFluencyApp: a simple angular app with the user interface
- WriteFluencyApi: a simple .net9 api

# Features
## Listen And Write

Listen and write is a route of the site where the user can generate an audio, based on a complexity level and subject he chose, and then verify it. the generation of the audio runs at the api, it does a request to chat gpt api to generate a text based on the user input, then an audio is generated due a request to speech-to-text google cloud api. The verification of the text is also made on the api, using an algorithm that combine Needleman Wunsch Alignment with Levenshtein Distance to compare the user text with the original text. 

# API/SERVER

## Command to create migrations (relative path to WriteFluencyApi directory):

```bash
dotnet ef migrations add initial_migration -p ./src/WriteFluency.Infrastructure/WriteFluency.Infrastructure.csproj -s ./src/WriteFluency.WebApi/WriteFluency.WebApi.csproj 
```
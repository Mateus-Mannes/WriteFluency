# WriteFluency

WriteFluency is an interactive web application designed to help users practice and improve their English writing skills through listening and transcription exercises.

## 🎯 Overview

The application provides real-world English listening comprehension exercises based on current news articles. Users can listen to AI-generated audio and transcribe what they hear, receiving instant feedback on accuracy with detailed text comparison analysis.

## 🏗️ Architecture

This repository follows a **microservices architecture** orchestrated by **.NET Aspire**, featuring:

### Backend Services (.NET 9)
- **WriteFluency.WebApi**: REST API providing endpoints for propositions, text comparisons, and authentication
- **WriteFluency.NewsWorker**: Background service that automatically fetches news articles, generates summaries, and creates audio exercises on a cron schedule
- **WriteFluency.DbMigrator**: Database migration service ensuring schema is up-to-date before other services start

### Frontend Application
- **Angular 21**: Modern, server-side rendered (SSR) web application with Material Design components
- **OpenTelemetry**: Integrated telemetry and monitoring with Application Insights support

### Infrastructure & Orchestration
- **AppHost (.NET Aspire 9.3)**: Orchestrates all services with dependency management, health checks, and service discovery
- **PostgreSQL 14.3**: Primary relational database for storing propositions, user data, and news articles
- **MinIO**: S3-compatible object storage for audio files and images
- **Docker Compose**: Alternative deployment option for development environments

## 🧩 Project Structure

```
WriteFluency/
├── src/
│   ├── host/                                    # .NET Aspire Orchestration
│   │   ├── WriteFluency.AppHost/               # Aspire AppHost - orchestrates all services
│   │   └── WriteFluency.ServiceDefaults/       # Shared service configurations (telemetry, health checks)
│   │
│   ├── propositions-service/                   # Backend Microservice
│   │   ├── WriteFluency.WebApi/               # REST API layer
│   │   ├── WriteFluency.Application/          # Application logic and services
│   │   ├── WriteFluency.Application.Contracts/ # DTOs and interfaces
│   │   ├── WriteFluency.Domain/               # Domain models and business logic
│   │   ├── WriteFluency.Infrastructure/       # Data access, external APIs, file storage
│   │   ├── WriteFluency.NewsWorker/          # Background worker for news processing
│   │   ├── WriteFluency.DbMigrator/          # Database migration service
│   │   └── WriteFluency.Shared/              # Shared utilities and extensions
│   │
│   └── webapp/                                 # Angular Frontend
│       ├── src/app/                           # Application components
│       └── src/api/                           # Auto-generated API clients (OpenAPI)
│
└── tests/
    ├── WriteFluency.Application.Tests/        # Application layer tests
    └── WriteFluency.Infrastructure.Tests/     # Infrastructure layer tests
```

## ✨ Key Features

### 🎧 Listen and Write Exercise
Users can:
1. Select a **complexity level** (Beginner, Intermediate, Advanced) and **subject/topic**
2. Listen to AI-generated audio based on real news articles
3. Transcribe what they hear
4. Receive instant **accuracy feedback** with detailed text comparison

### 📊 Advanced Text Comparison Algorithm
The verification system uses a sophisticated algorithm combining:
- **Needleman-Wunsch Alignment**: Global sequence alignment for optimal text matching
- **Levenshtein Distance**: Edit distance calculation for accuracy measurement
- **Token-based Analysis**: Word-level comparison with contextual awareness
- **Visual Feedback**: Highlighted differences showing correct, incorrect, and missing text

### 🤖 AI-Powered Content Generation
- **OpenAI GPT Integration**: Generates contextual text summaries from news articles
- **Google Cloud Text-to-Speech**: Creates natural-sounding audio with configurable voices
- **Automated News Pipeline**: Background worker fetches articles, validates images, generates content, and stores results

### 🔐 Authentication & Authorization
- ASP.NET Core Identity with JWT tokens
- Google OAuth integration
- Secure password management

## 🛠️ Technology Stack

### Backend
- **.NET 9** with C# 13
- **ASP.NET Core Minimal APIs**
- **Entity Framework Core** with PostgreSQL provider
- **.NET Aspire** for cloud-native orchestration
- **FluentResults** for functional error handling
- **Microsoft.Extensions.AI** for AI abstraction

### Frontend
- **Angular 21** with standalone components
- **Angular Material** for UI components
- **RxJS** for reactive programming
- **Server-Side Rendering (SSR)** for performance
- **OpenTelemetry** for observability

### Infrastructure & Storage
- **PostgreSQL 14.3** - Relational database
- **MinIO** - S3-compatible object storage
- **Docker & Docker Compose** - Containerization
- **Kubernetes** - Production deployment (K8s manifests available)

### External APIs
- **OpenAI API** (GPT-4) - Text generation
- **Google Cloud Text-to-Speech** - Audio generation
- **News APIs** - Article sourcing (configurable)

## 🚀 Getting Started

### Prerequisites
- **.NET 9 SDK**
- **Node.js 20+** and npm
- **Docker Desktop** (for running dependencies)
- **Visual Studio 2022** or **JetBrains Rider** (recommended) or **VS Code**

### Running with .NET Aspire (Recommended)

1. **Clone the repository**
   ```bash
   git clone https://github.com/yourusername/WriteFluency.git
   cd WriteFluency
   ```

2. **Configure secrets** (appsettings or user secrets)
   ```json
   {
     "OpenAI": {
       "Key": "your-openai-api-key",
       "BaseAddress": "https://api.openai.com/v1"
     },
     "TextToSpeech": {
       "ApiKey": "your-google-cloud-api-key"
     }
   }
   ```

3. **Run the AppHost**
   ```bash
   cd src/host/WriteFluency.AppHost
   dotnet run
   ```

   The Aspire dashboard will open automatically showing all services, logs, and metrics.

   `users-progress-service` (Azure Functions isolated) is wired into AppHost for local run mode only.
   Its configuration should be defined in the function app itself (`appsettings`, environment variables, or user secrets), not in AppHost.

4. **Access the application**
   - **Frontend**: http://localhost:4200
   - **API**: http://localhost:5050
   - **Aspire Dashboard**: Shown in terminal output

### Running with Docker Compose

1. **Start infrastructure services**
   ```bash
   docker-compose -f docker-compose.services.yml up -d
   ```

2. **Run database migrations**
   ```bash
   cd src/propositions-service/WriteFluency.DbMigrator
   dotnet run
   ```

3. **Start the API**
   ```bash
   cd src/propositions-service/WriteFluency.WebApi
   dotnet run
   ```

4. **Start the web application**
   ```bash
   cd src/webapp
   npm install
   npm start
   ```

## 🧪 Testing

Run all tests:
```bash
dotnet test
```

Run specific test projects:
```bash
dotnet test tests/WriteFluency.Application.Tests
dotnet test tests/WriteFluency.Infrastructure.Tests
```

## 📝 Database Migrations

### Create a new migration
```bash
dotnet ef migrations add MigrationName \
  -p ./src/propositions-service/WriteFluency.Infrastructure/WriteFluency.Infrastructure.csproj \
  -s ./src/propositions-service/WriteFluency.WebApi/WriteFluency.WebApi.csproj
```

### Apply migrations
The **DbMigrator** service automatically applies migrations on startup when using Aspire.

For manual migration:
```bash
cd src/propositions-service/WriteFluency.DbMigrator
dotnet run
```

## 🐳 Docker Deployment

### Build Docker images
```bash
cd src/host/WriteFluency.AppHost
./build-k8s-images.sh
```

### Regenerate Aspirate manifests (non-interactive)
Use this command to regenerate manifests without rebuilding images and without interactive prompts for AppHost inputs:

```bash
cd src/host/WriteFluency.AppHost
aspirate generate --skip-build --non-interactive \
  -pa wf-infra-minio-user=minioadmin \
  -pa wf-infra-minio-password=admin123 \
  -pa wf-infra-postgres-password=postgres \
  -pa wf-infra-redis-password=admin123
```

Parameters passed with `-pa`:
- `wf-infra-minio-user`: MinIO root user.
- `wf-infra-minio-password`: MinIO root password.
- `wf-infra-postgres-password`: Postgres password.
- `wf-infra-redis-password`: Redis password.

Notes:
- `users-progress-service` is intentionally excluded from Aspirate-generated manifests because it is deployed independently to Azure Functions (`deploy-users-progress*.yml` workflows).

### Deploy to Kubernetes
```bash
./deploy-k8s.sh
```

## 📊 Monitoring & Observability

The application includes:
- **Health checks** for all services and dependencies
- **OpenTelemetry** tracing and metrics
- **Aspire Dashboard** for local development
- **Application Insights** support for production

### Email Deliverability (Users Service)
- Deliverability runbook for self-hosted SMTP (SPF/DKIM/DMARC, complaint and bounce thresholds, and incident playbook):
  - `src/users-service/WriteFluency.Users.WebApi/Email/DELIVERABILITY_RUNBOOK.md`

## 🔧 Development Tools

- **Swagger/OpenAPI**: API documentation at `/swagger` (development only)
- **Aspire Dashboard**: Service orchestration and monitoring
- **Hot Reload**: Both .NET and Angular support hot reload during development

## 🤝 Contributing

1. Fork the repository
2. Create a feature branch (`git checkout -b feature/amazing-feature`)
3. Commit your changes (`git commit -m 'Add amazing feature'`)
4. Push to the branch (`git push origin feature/amazing-feature`)
5. Open a Pull Request

## 📄 License

This project is licensed under the terms specified in [LICENSE](LICENSE).

## 🙏 Acknowledgments

- **OpenAI** for GPT API
- **Google Cloud** for Text-to-Speech API
- **.NET Aspire** for cloud-native orchestration
- **Angular Team** for the excellent framework
- News providers for article content


## Users Service (US2) Local Smoke Check

Run AppHost and validate users-service foundation:

1. Start orchestration:
   ```bash
   cd src/host/WriteFluency.AppHost
   dotnet run
   ```
2. Probe users-service health:
   ```bash
   curl -i http://localhost:5100/health
   ```
3. Verify users DB migration applied:
   - Check `wf-users-db-migrator` logs in Aspire dashboard for `Users DB migrations applied successfully.`
   - Connect to PostgreSQL and confirm Identity tables exist in `wf-users-postgresdb` (for example `AspNetUsers`, `AspNetRoles`).
4. Confirm existing flows still boot:
   - `wf-propositions-api` and `wf-propositions-news-worker` should be healthy in Aspire.

## Users Service (US4) Social Login Local Validation

Google and Microsoft login are exposed by `users-service` under `/users/auth/external/*`.

### Required local user secrets

`users-service` and `propositions-service` share the same `UserSecretsId`, so set credentials once (for example from `src/propositions-service/WriteFluency.WebApi`):

```bash
cd src/propositions-service/WriteFluency.WebApi
dotnet user-secrets set "Authentication:Google:ClientId" "<google-client-id>"
dotnet user-secrets set "Authentication:Google:ClientSecret" "<google-client-secret>"
dotnet user-secrets set "Authentication:Microsoft:ClientId" "<microsoft-client-id>"
dotnet user-secrets set "Authentication:Microsoft:ClientSecret" "<microsoft-client-secret>"
```

### OAuth provider callback URLs (local)

Configure these redirect/callback URLs in provider consoles:

- Google: `https://localhost:5101/users/signin-google`
- Microsoft: `https://localhost:5101/users/signin-microsoft`

### Swagger-first local test flow

1. Run AppHost:
   ```bash
   cd src/host/WriteFluency.AppHost
   dotnet run
   ```
2. Open users Swagger at `https://localhost:5101/users/swagger`.
3. Use these endpoints:
   - `GET /users/auth/external/providers`
   - `GET /users/auth/external/google/start?returnUrl=/users/swagger/index.html`
   - `GET /users/auth/external/microsoft/start?returnUrl=/users/swagger/index.html`
4. Start endpoint redirects to provider login, then back to Swagger with query params:
   - Success: `?auth=success&provider=<google|microsoft>`
   - Error: `?auth=error&provider=<google|microsoft>&code=<error_code>`

### Future frontend integration contract

Frontend can reuse the same start endpoint by passing an allowlisted frontend callback URL in `returnUrl` (for example `http://localhost:4200/auth/callback`).

## Users Service (US5) Login Location Tracking

Successful logins are audited in `users-service` and persisted to `UserLoginActivities` with:

- auth method/provider
- client IP address
- geo lookup status
- country/city (best effort from IP)

### Configuration

`src/users-service/WriteFluency.Users.WebApi/appsettings.json` includes:

```json
"LoginLocation": {
  "Enabled": true,
  "GeoLite2CityBlobUri": "https://wfusersdpprod01.blob.core.windows.net/geolite/GeoLite2-City.mmdb",
  "BlobMetadataRefreshMinutes": 60,
  "GeoLite2CityDbPath": "/app/data/GeoLite2-City.mmdb"
}
```

### Deployment requirement

- Assign the users API identity `Storage Blob Data Reader` on the `geolite` container.
- Keep the latest `GeoLite2-City.mmdb` uploaded at the configured blob URI.
- The API checks blob metadata periodically and reloads the in-memory reader when the blob ETag changes.

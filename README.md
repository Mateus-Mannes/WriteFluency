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

4. **Access the application**
   - **Frontend**: http://localhost:4200
   - **API**: http://localhost:5000
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



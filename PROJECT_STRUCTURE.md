# 📁 Mikroservis Proje Yapısı
## 1 Milyon Kullanıcı için Üretim Hazır Mimari

## 🏗️ Genel Yapı

```
📦 edu-platform/
├── 📁 services/                     # Mikroservisler
│   ├── 📁 identity-service/
│   ├── 📁 speed-reading-service/
│   ├── 📁 coaching-service/
│   ├── 📁 blog-service/
│   ├── 📁 interactive-content-service/
│   ├── 📁 exam-service/
│   ├── 📁 analytics-service/
│   ├── 📁 notification-service/
│   └── 📁 logging-service/
│
├── 📁 gateway/                      # API Gateway
│   └── 📁 yarp-gateway/
│
├── 📁 shared/                       # Paylaşılan Kütüphaneler
│   ├── 📁 EduPlatform.Shared.Kernel/
│   ├── 📁 EduPlatform.Shared.Contracts/
│   └── 📁 EduPlatform.Shared.Infrastructure/
│
├── 📁 clients/                      # Frontend Uygulamaları
│   ├── 📁 web-angular/
│   └── 📁 mobile-flutter/
│
├── 📁 infrastructure/               # Altyapı Konfigürasyonları
│   ├── 📁 docker/
│   ├── 📁 kubernetes/
│   └── 📁 terraform/
│
└── 📁 docs/                         # Dokümantasyon
```

---

## 🔧 Mikroservis Şablonu (.NET 8)

Her mikroservis aşağıdaki Clean Architecture yapısını kullanır:

```
📦 speed-reading-service/
├── 📁 src/
│   ├── 📁 SpeedReading.API/
│   │   ├── Controllers/
│   │   │   └── ExercisesController.cs
│   │   ├── Middleware/
│   │   │   ├── ExceptionMiddleware.cs
│   │   │   └── CorrelationIdMiddleware.cs
│   │   ├── Program.cs
│   │   ├── appsettings.json
│   │   └── Dockerfile
│   │
│   ├── 📁 SpeedReading.Application/
│   │   ├── 📁 Commands/
│   │   │   ├── CreateExercise/
│   │   │   │   ├── CreateExerciseCommand.cs
│   │   │   │   ├── CreateExerciseHandler.cs
│   │   │   │   └── CreateExerciseValidator.cs
│   │   │   └── CompleteExercise/
│   │   ├── 📁 Queries/
│   │   │   ├── GetExercises/
│   │   │   └── GetStudentProgress/
│   │   ├── 📁 Behaviors/
│   │   │   ├── LoggingBehavior.cs
│   │   │   ├── ValidationBehavior.cs
│   │   │   └── PerformanceBehavior.cs
│   │   ├── 📁 DTOs/
│   │   ├── 📁 Mappings/
│   │   └── DependencyInjection.cs
│   │
│   ├── 📁 SpeedReading.Domain/
│   │   ├── 📁 Entities/
│   │   │   ├── Exercise.cs
│   │   │   ├── StudentProgress.cs
│   │   │   └── ReadingSession.cs
│   │   ├── 📁 ValueObjects/
│   │   │   ├── ReadingSpeed.cs
│   │   │   └── ComprehensionScore.cs
│   │   ├── 📁 Events/
│   │   │   ├── ExerciseCompletedEvent.cs
│   │   │   └── ProgressUpdatedEvent.cs
│   │   ├── 📁 Interfaces/
│   │   │   ├── IExerciseRepository.cs
│   │   │   └── IProgressRepository.cs
│   │   └── 📁 Enums/
│   │
│   └── 📁 SpeedReading.Infrastructure/
│       ├── 📁 Persistence/
│       │   ├── ApplicationDbContext.cs
│       │   ├── 📁 Configurations/
│       │   │   └── ExerciseConfiguration.cs
│       │   ├── 📁 Repositories/
│       │   │   └── ExerciseRepository.cs
│       │   └── 📁 Migrations/
│       ├── 📁 Messaging/
│       │   ├── RabbitMqPublisher.cs
│       │   └── EventConsumer.cs
│       ├── 📁 Caching/
│       │   └── RedisCacheService.cs
│       └── DependencyInjection.cs
│
├── 📁 tests/
│   ├── 📁 SpeedReading.UnitTests/
│   ├── 📁 SpeedReading.IntegrationTests/
│   └── 📁 SpeedReading.ArchitectureTests/
│
└── 📄 SpeedReading.sln
```

---

## 🔐 Identity Service (Keycloak Entegrasyonu)

```
📦 identity-service/
├── 📁 src/
│   └── 📁 Identity.API/
│       ├── Controllers/
│       │   ├── UsersController.cs
│       │   └── RolesController.cs
│       ├── Services/
│       │   ├── KeycloakService.cs
│       │   └── UserSyncService.cs
│       ├── Program.cs
│       └── Dockerfile
│
├── 📁 keycloak/
│   ├── realm-export.json          # Realm konfigürasyonu
│   ├── themes/                    # Custom theme
│   └── docker-compose.keycloak.yml
│
└── 📄 Identity.sln
```

---

## 🌐 API Gateway (YARP)

```
📦 gateway/
└── 📁 yarp-gateway/
    ├── 📁 src/
    │   └── 📁 Gateway.API/
    │       ├── Program.cs
    │       ├── appsettings.json
    │       ├── yarp.json           # Route konfigürasyonu
    │       ├── 📁 Middleware/
    │       │   ├── RateLimitingMiddleware.cs
    │       │   ├── CorrelationIdMiddleware.cs
    │       │   └── RequestLoggingMiddleware.cs
    │       ├── 📁 Transforms/
    │       │   └── AuthHeaderTransform.cs
    │       └── Dockerfile
    │
    └── 📄 Gateway.sln
```

**yarp.json örneği:**
```json
{
  "ReverseProxy": {
    "Routes": {
      "identity-route": {
        "ClusterId": "identity-cluster",
        "Match": { "Path": "/api/identity/{**catch-all}" }
      },
      "speedreading-route": {
        "ClusterId": "speedreading-cluster",
        "Match": { "Path": "/api/speedreading/{**catch-all}" }
      }
    },
    "Clusters": {
      "identity-cluster": {
        "Destinations": {
          "destination1": { "Address": "http://identity-service:8080" }
        }
      },
      "speedreading-cluster": {
        "LoadBalancingPolicy": "RoundRobin",
        "Destinations": {
          "destination1": { "Address": "http://speed-reading-1:8080" },
          "destination2": { "Address": "http://speed-reading-2:8080" }
        }
      }
    }
  }
}
```

---

## 📦 Shared Libraries

```
📦 shared/
├── 📁 EduPlatform.Shared.Kernel/
│   ├── 📁 Primitives/
│   │   ├── Entity.cs
│   │   ├── AggregateRoot.cs
│   │   ├── ValueObject.cs
│   │   └── DomainEvent.cs
│   ├── 📁 Results/
│   │   ├── Result.cs
│   │   └── Error.cs
│   └── EduPlatform.Shared.Kernel.csproj
│
├── 📁 EduPlatform.Shared.Contracts/
│   ├── 📁 Events/
│   │   ├── StudentRegisteredEvent.cs
│   │   ├── ExerciseCompletedEvent.cs
│   │   └── ExamSubmittedEvent.cs
│   ├── 📁 DTOs/
│   │   ├── StudentDto.cs
│   │   └── ProgressDto.cs
│   └── EduPlatform.Shared.Contracts.csproj
│
└── 📁 EduPlatform.Shared.Infrastructure/
    ├── 📁 Messaging/
    │   ├── RabbitMqConnection.cs
    │   └── IEventPublisher.cs
    ├── 📁 Caching/
    │   └── ICacheService.cs
    ├── 📁 Logging/
    │   └── SerilogConfiguration.cs
    └── EduPlatform.Shared.Infrastructure.csproj
```

---

## 🅰️ Angular Frontend

```
📦 clients/web-angular/
├── 📁 src/
│   ├── 📁 app/
│   │   ├── 📁 core/
│   │   │   ├── 📁 auth/
│   │   │   │   ├── auth.service.ts
│   │   │   │   ├── auth.guard.ts
│   │   │   │   └── keycloak.config.ts
│   │   │   ├── 📁 interceptors/
│   │   │   │   ├── auth.interceptor.ts
│   │   │   │   ├── error.interceptor.ts
│   │   │   │   └── loading.interceptor.ts
│   │   │   ├── 📁 services/
│   │   │   │   └── api.service.ts
│   │   │   └── core.module.ts
│   │   │
│   │   ├── 📁 shared/
│   │   │   ├── 📁 components/
│   │   │   ├── 📁 directives/
│   │   │   ├── 📁 pipes/
│   │   │   └── shared.module.ts
│   │   │
│   │   ├── 📁 features/
│   │   │   ├── 📁 dashboard/
│   │   │   ├── 📁 speed-reading/
│   │   │   │   ├── components/
│   │   │   │   ├── services/
│   │   │   │   ├── models/
│   │   │   │   └── speed-reading.module.ts
│   │   │   ├── 📁 coaching/
│   │   │   ├── 📁 exams/
│   │   │   ├── 📁 content/
│   │   │   └── 📁 admin/
│   │   │
│   │   ├── 📁 layouts/
│   │   │   ├── main-layout/
│   │   │   ├── auth-layout/
│   │   │   └── admin-layout/
│   │   │
│   │   ├── app.component.ts
│   │   ├── app.routes.ts
│   │   └── app.config.ts
│   │
│   ├── 📁 assets/
│   ├── 📁 environments/
│   └── 📁 styles/
│
├── angular.json
├── package.json
└── Dockerfile
```

---

## 📱 Flutter Mobile

```
📦 clients/mobile-flutter/
├── 📁 lib/
│   ├── 📁 core/
│   │   ├── 📁 constants/
│   │   │   ├── api_endpoints.dart
│   │   │   └── app_constants.dart
│   │   ├── 📁 errors/
│   │   │   ├── failures.dart
│   │   │   └── exceptions.dart
│   │   ├── 📁 network/
│   │   │   ├── dio_client.dart
│   │   │   └── api_interceptor.dart
│   │   ├── 📁 storage/
│   │   │   └── secure_storage.dart
│   │   └── 📁 theme/
│   │       └── app_theme.dart
│   │
│   ├── 📁 data/
│   │   ├── 📁 datasources/
│   │   │   ├── remote/
│   │   │   │   ├── auth_remote_datasource.dart
│   │   │   │   └── exercise_remote_datasource.dart
│   │   │   └── local/
│   │   │       └── exercise_local_datasource.dart
│   │   ├── 📁 models/
│   │   │   ├── user_model.dart
│   │   │   └── exercise_model.dart
│   │   └── 📁 repositories/
│   │       └── exercise_repository_impl.dart
│   │
│   ├── 📁 domain/
│   │   ├── 📁 entities/
│   │   │   ├── user.dart
│   │   │   └── exercise.dart
│   │   ├── 📁 repositories/
│   │   │   └── exercise_repository.dart
│   │   └── 📁 usecases/
│   │       ├── get_exercises.dart
│   │       └── complete_exercise.dart
│   │
│   ├── 📁 presentation/
│   │   ├── 📁 blocs/
│   │   │   ├── auth/
│   │   │   └── exercise/
│   │   ├── 📁 pages/
│   │   │   ├── home/
│   │   │   ├── speed_reading/
│   │   │   ├── coaching/
│   │   │   └── exams/
│   │   └── 📁 widgets/
│   │
│   ├── injection_container.dart
│   └── main.dart
│
├── 📁 test/
├── pubspec.yaml
└── Dockerfile  # Web build için
```

---

## 🐳 Infrastructure

```
📦 infrastructure/
├── 📁 docker/
│   ├── docker-compose.yml           # Full stack
│   ├── docker-compose.dev.yml       # Development
│   ├── docker-compose.infra.yml     # DB, Redis, RabbitMQ
│   └── 📁 dockerfiles/
│       ├── api.Dockerfile
│       ├── gateway.Dockerfile
│       └── web.Dockerfile
│
├── 📁 kubernetes/
│   ├── 📁 base/
│   │   ├── namespace.yaml
│   │   ├── configmap.yaml
│   │   ├── secrets.yaml
│   │   ├── 📁 services/
│   │   │   ├── identity-service.yaml
│   │   │   ├── speed-reading-service.yaml
│   │   │   └── ...
│   │   └── 📁 infrastructure/
│   │       ├── postgresql.yaml
│   │       ├── redis.yaml
│   │       ├── rabbitmq.yaml
│   │       └── elasticsearch.yaml
│   │
│   ├── 📁 overlays/
│   │   ├── 📁 development/
│   │   ├── 📁 staging/
│   │   └── 📁 production/
│   │       ├── kustomization.yaml
│   │       ├── hpa.yaml
│   │       └── ingress.yaml
│   │
│   └── 📁 helm-charts/
│       └── edu-platform/
│           ├── Chart.yaml
│           ├── values.yaml
│           └── templates/
│
└── 📁 terraform/
    ├── main.tf
    ├── variables.tf
    ├── outputs.tf
    ├── 📁 modules/
    │   ├── 📁 kubernetes/
    │   ├── 📁 postgresql/
    │   ├── 📁 redis/
    │   └── 📁 networking/
    └── 📁 environments/
        ├── dev.tfvars
        ├── staging.tfvars
        └── prod.tfvars
```

---

## 📊 Monitoring & Logging

```
📦 infrastructure/monitoring/
├── 📁 prometheus/
│   ├── prometheus.yml
│   └── alert-rules.yml
│
├── 📁 grafana/
│   ├── provisioning/
│   │   ├── dashboards/
│   │   │   ├── services-dashboard.json
│   │   │   └── kubernetes-dashboard.json
│   │   └── datasources/
│   │       └── datasources.yml
│   └── grafana.ini
│
├── 📁 elasticsearch/
│   └── elasticsearch.yml
│
├── 📁 logstash/
│   └── logstash.conf
│
├── 📁 kibana/
│   └── kibana.yml
│
└── 📁 jaeger/
    └── jaeger.yml
```

---

## 🔧 Geliştirme Ortamı Kurulumu

```bash
# 1. Repository clone
git clone https://github.com/your-org/edu-platform.git
cd edu-platform

# 2. Shared libraries build
cd shared
dotnet build

# 3. Infrastructure başlat
cd ../infrastructure/docker
docker-compose -f docker-compose.infra.yml up -d

# 4. Keycloak realm import
docker exec keycloak /opt/keycloak/bin/kc.sh import --file /opt/keycloak/data/import/realm-export.json

# 5. Database migrations
cd ../../services/identity-service
dotnet ef database update

# 6. Tüm servisleri başlat
cd ../../infrastructure/docker
docker-compose up -d

# 7. Frontend başlat (development)
cd ../../clients/web-angular
npm install
npm run start
```

---

*Güncelleme: 2024-12-20 - Mikroservis Mimarisi v2.0*

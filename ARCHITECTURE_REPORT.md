# 🎓 Eğitim Platformu Mimari Tasarım Raporu
## %100 Ücretsiz ve Açık Kaynak Teknolojilerle

> Arşiv/tasarım referansı: Bu belge ilk hedef mimariyi ve alternatifleri anlatır;
> güncel çalışan servis sınırları ve production sözleşmeleri için
> `docs/BOUNDED_CONTEXT_CONTRACTS.md`, `docs/API_CONTRACTS.md` ve
> `README.md` esas alınmalıdır. Bu belgede geçen Keycloak, mobil istemci ve
> gelecek ML bileşenleri mevcut production kapsamı olarak yorumlanmamalıdır.

## 📋 Yönetici Özeti

Bu rapor, **1 milyon aktif kullanıcı** için tasarlanmış, **tamamen ücretsiz ve açık kaynak** teknolojiler kullanan modüler bir eğitim platformunun mikroservis mimarisini içermektedir.

**Teknoloji Stack'i (Tamamı Ücretsiz/Açık Kaynak):**
- **Backend:** .NET 8+ (ASP.NET Core) - MIT Lisans
- **Frontend Web:** Angular - MIT Lisans
- **Mobil:** Flutter - BSD Lisans
- **Veritabanı:** PostgreSQL - PostgreSQL Lisans (Ücretsiz)
- **Gelecek:** ML.NET, Python ML (Ücretsiz)

---

## 🏗️ 1. Mikroservis Mimarisi (1M+ Kullanıcı)

### 1.1 Servis Ayrımı

```
┌─────────────────────────────────────────────────────────────────────┐
│                        LOAD BALANCER (Nginx/HAProxy)                │
└─────────────────────────────┬───────────────────────────────────────┘
                              │
┌─────────────────────────────▼───────────────────────────────────────┐
│                     API GATEWAY (YARP - Ücretsiz)                   │
│              Routing, Rate Limiting, Authentication                  │
└─────────────────────────────┬───────────────────────────────────────┘
                              │
        ┌─────────────────────┼─────────────────────┐
        │                     │                     │
        ▼                     ▼                     ▼
┌───────────────┐   ┌───────────────┐   ┌───────────────┐
│   Identity    │   │ Speed Reading │   │   Coaching    │
│   Service     │   │   Service     │   │   Service     │
│  (Keycloak)   │   │               │   │               │
└───────────────┘   └───────────────┘   └───────────────┘
        │                     │                     │
        ▼                     ▼                     ▼
┌───────────────┐   ┌───────────────┐   ┌───────────────┐
│     Blog      │   │  Interactive  │   │     Exam      │
│   Service     │   │   Content     │   │   Service     │
│               │   │   Service     │   │               │
└───────────────┘   └───────────────┘   └───────────────┘
        │                     │                     │
        ▼                     ▼                     ▼
┌───────────────┐   ┌───────────────┐   ┌───────────────┐
│   Analytics   │   │ Notification  │   │   Logging     │
│   Service     │   │   Service     │   │   Service     │
└───────────────┘   └───────────────┘   └───────────────┘
```

### 1.2 Servis Detayları

| Servis | Sorumluluk | Veritabanı | Ölçekleme |
|--------|------------|------------|-----------|
| **Identity** | Auth, Users, Roles | PostgreSQL | 3-10 replika |
| **Speed Reading** | Okuma egzersizleri, ilerleme | PostgreSQL + Redis | 5-20 replika |
| **Coaching** | Koç-öğrenci eşleştirme, hedefler | PostgreSQL | 3-15 replika |
| **Blog** | İçerik yönetimi, SEO | PostgreSQL + Elasticsearch | 3-10 replika |
| **Interactive Content** | Etkileşimli dersler, medya | PostgreSQL + MinIO | 5-25 replika |
| **Exam** | Soru bankası, sınavlar, puanlama | PostgreSQL + Redis | 5-30 replika |
| **Analytics** | Öğrenme analitiği, raporlar | TimescaleDB/ClickHouse | 3-10 replika |
| **Notification** | Push, Email, SMS | Redis + RabbitMQ | 3-10 replika |
| **Logging** | Log aggregation, audit | Elasticsearch | 3-5 replika |

---

## 🆓 2. %100 Ücretsiz Kütüphane Stack'i

### 2.1 CQRS/Mediator Pattern
 
 **✅ MediatR (MIT Lisans - Standart)**
 
 ```csharp
 // MediatR Kurulum
 builder.Services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(assembly));
 
 // Command Örneği
 public record CreateStudentCommand(string Name, string Email) : IRequest<StudentDto>;
 
 public class CreateStudentHandler : IRequestHandler<CreateStudentCommand, StudentDto>
 {
     public async Task<StudentDto> Handle(CreateStudentCommand request, 
         CancellationToken cancellationToken)
     {
         // Logic here
         return new StudentDto { Id = Guid.NewGuid(), Name = request.Name };
     }
 }
 ```
 
 **Kullanılan Diğer Desenler:**
 | Desen | Amaç | Kullanım Yeri |
 |-------|------|---------------|
 | **Outbox** | Event kaybını önleme | Domain Events -> Integration Events |
 | **Result** | Hata yönetimi | Tüm Service Methodları |
 | **Specification** | Query Logic ayrıştırma | Repository Filtreleme |
 | **Unit of Work** | Transaction yönetimi | Command Handler'lar |

### 2.2 Authentication & Authorization

**❌ IdentityServer (Ücretli)**
**✅ Keycloak + OpenIddict (Açık Kaynak)**

#### Seçenek 1: Keycloak (Önerilen - Enterprise Ready)

```yaml
# docker-compose.yml
services:
  keycloak:
    image: quay.io/keycloak/keycloak:latest
    environment:
      - KEYCLOAK_ADMIN=${KEYCLOAK_ADMIN:?set via secret manager}
      - KEYCLOAK_ADMIN_PASSWORD=${KEYCLOAK_ADMIN_PASSWORD:?set via secret manager}
    command: start-dev
    ports:
      - "8080:8080"
```

```csharp
// .NET Integration
services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.Authority = "http://keycloak:8080/realms/edu-platform";
        options.Audience = "edu-api";
        options.RequireHttpsMetadata = false; // Dev only
    });
```

**Keycloak Özellikleri:**
- ✅ OAuth 2.0 / OpenID Connect
- ✅ SSO (Single Sign-On)
- ✅ Social Login (Google, Facebook, Apple)
- ✅ Multi-Factor Authentication
- ✅ User Federation (LDAP, Active Directory)
- ✅ Role-Based Access Control
- ✅ Admin Console UI
- ✅ Apache 2.0 Lisans (Ücretsiz)

#### Seçenek 2: OpenIddict (Kendi STS)

```csharp
// OpenIddict kurulum
services.AddOpenIddict()
    .AddCore(options =>
    {
        options.UseEntityFrameworkCore()
               .UseDbContext<ApplicationDbContext>();
    })
    .AddServer(options =>
    {
        options.SetAuthorizationEndpointUris("/connect/authorize")
               .SetTokenEndpointUris("/connect/token")
               .SetUserinfoEndpointUris("/connect/userinfo");
               
        options.AllowAuthorizationCodeFlow()
               .AllowRefreshTokenFlow();
               
        options.AddDevelopmentEncryptionCertificate()
               .AddDevelopmentSigningCertificate();
               
        options.UseAspNetCore()
               .EnableAuthorizationEndpointPassthrough()
               .EnableTokenEndpointPassthrough();
    });
```

### 2.3 Tam Ücretsiz Kütüphane Listesi

```
📦 Backend (.NET 8+) - Tamamı Ücretsiz
├── 🔧 Core Framework
│   ├── ASP.NET Core 8          (MIT) - Web API
│   ├── Entity Framework Core 8  (MIT) - ORM
│   └── Dapper                   (Apache 2.0) - Micro ORM
│
├── 🎯 CQRS & Mediator
│   └── TS.MediatR               (MIT) - Mediator Pattern
│
├── 🔐 Authentication
│   ├── Keycloak                 (Apache 2.0) - IAM Server
│   └── OpenIddict               (Apache 2.0) - OpenID Connect
│
├── ✅ Validation
│   └── FluentValidation         (Apache 2.0)
│
├── 🔄 Mapping
│   └── AutoMapper               (MIT)
│
├── 📝 Logging
│   ├── Serilog                  (Apache 2.0)
│   ├── Serilog.Sinks.Seq        (Apache 2.0)
│   └── Serilog.Sinks.Elasticsearch (Apache 2.0)
│
├── 🛡️ Resilience
│   └── Polly                    (BSD-3-Clause)
│
├── ⏰ Background Jobs
│   ├── Hangfire (Basic)         (LGPL) - Free tier
│   └── Quartz.NET               (Apache 2.0) - Alternatif
│
├── 📡 Real-time
│   └── SignalR                  (MIT)
│
├── 🌐 API Gateway
│   └── YARP                     (MIT)
│
├── 📨 Message Queue
│   └── RabbitMQ.Client          (Apache 2.0/MPL 2.0)
│
├── 💾 Caching
│   ├── StackExchange.Redis      (MIT)
│   └── Microsoft.Extensions.Caching (MIT)
│
├── 🔍 Search
│   └── NEST (Elasticsearch)     (Apache 2.0)
│
├── 📊 Health Checks
│   └── AspNetCore.HealthChecks  (Apache 2.0)
│
└── 🤖 Machine Learning
    └── ML.NET                   (MIT)
```

---

## 🗄️ 3. Veritabanı Mimarisi (1M Kullanıcı)

### 3.1 Database Per Service Pattern

```
┌─────────────────────────────────────────────────────────────────┐
│                    PostgreSQL Cluster (Citus/Patroni)           │
├─────────────────────────────────────────────────────────────────┤
│  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐          │
│  │ identity_db  │  │ speedread_db │  │ coaching_db  │          │
│  │              │  │              │  │              │          │
│  │ - users      │  │ - exercises  │  │ - goals      │          │
│  │ - roles      │  │ - progress   │  │ - sessions   │          │
│  │ - sessions   │  │ - scores     │  │ - matches    │          │
│  └──────────────┘  └──────────────┘  └──────────────┘          │
│                                                                  │
│  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐          │
│  │   blog_db    │  │  content_db  │  │   exam_db    │          │
│  │              │  │              │  │              │          │
│  │ - posts      │  │ - lessons    │  │ - questions  │          │
│  │ - comments   │  │ - media      │  │ - exams      │          │
│  │ - categories │  │ - interacts  │  │ - results    │          │
│  └──────────────┘  └──────────────┘  └──────────────┘          │
└─────────────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────────────┐
│                     Redis Cluster (6+ nodes)                     │
├─────────────────────────────────────────────────────────────────┤
│  Sessions | API Cache | Rate Limiting | Real-time Data         │
└─────────────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────────────┐
│                   Elasticsearch Cluster                          │
├─────────────────────────────────────────────────────────────────┤
│  Full-text Search | Logs | Analytics                            │
└─────────────────────────────────────────────────────────────────┘
```

### 3.2 PostgreSQL Sharding Stratejisi

```sql
-- Citus ile Distributed Tables
-- 1. Extension kurulumu
CREATE EXTENSION citus;

-- 2. Student activities için sharding (student_id ile)
SELECT create_distributed_table('student_activities', 'student_id');

-- 3. Time-based partitioning for analytics
CREATE TABLE learning_events (
    id BIGSERIAL,
    student_id BIGINT NOT NULL,
    event_type VARCHAR(50),
    event_data JSONB,
    created_at TIMESTAMPTZ DEFAULT NOW()
) PARTITION BY RANGE (created_at);

-- Monthly partitions
CREATE TABLE learning_events_2024_01 
    PARTITION OF learning_events
    FOR VALUES FROM ('2024-01-01') TO ('2024-02-01');
```

### 3.3 Connection Pooling (PgBouncer)

```ini
# pgbouncer.ini
[databases]
identity_db = host=pg-primary port=5432 dbname=identity_db
speedread_db = host=pg-primary port=5432 dbname=speedread_db

[pgbouncer]
listen_port = 6432
listen_addr = *
auth_type = md5
pool_mode = transaction
max_client_conn = 10000
default_pool_size = 100
min_pool_size = 10
```

---

## 📨 4. Event-Driven Architecture

### 4.1 RabbitMQ Yapılandırması

```yaml
# docker-compose.yml
services:
  rabbitmq:
    image: rabbitmq:4.3.4-management-alpine
    ports:
      - "5672:5672"
      - "15672:15672"
    environment:
      RABBITMQ_DEFAULT_USER: ${RABBITMQ_DEFAULT_USER:?set via secret manager}
      RABBITMQ_DEFAULT_PASS: ${RABBITMQ_DEFAULT_PASS:?set via secret manager}
    volumes:
      - rabbitmq_data:/var/lib/rabbitmq
```

### 4.2 Event Publishing Pattern

```csharp
// Domain Event
public record StudentRegisteredEvent(
    Guid StudentId,
    string Email,
    string Name,
    DateTime RegisteredAt
) : INotification;

// Event Publisher
public class EventPublisher : IEventPublisher
{
    private readonly IConnection _connection;
    private readonly IModel _channel;

    public async Task PublishAsync<T>(T @event) where T : class
    {
        var message = JsonSerializer.Serialize(@event);
        var body = Encoding.UTF8.GetBytes(message);
        
        _channel.BasicPublish(
            exchange: "edu-platform-events",
            routingKey: typeof(T).Name,
            basicProperties: null,
            body: body
        );
    }
}

// Event Consumer (Notification Service)
public class StudentRegisteredConsumer : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        _channel.BasicConsume(
            queue: "notification-queue",
            autoAck: false,
            consumer: consumer
        );
        return Task.CompletedTask;
    }
}
```

### 4.3 Event Topics

| Exchange | Routing Key | Consumer Services |
|----------|-------------|-------------------|
| `user-events` | `user.registered` | Notification, Analytics, Coaching |
| `user-events` | `user.login` | Analytics, Logging |
| `learning-events` | `exercise.completed` | Analytics, Coaching, Notification |
| `learning-events` | `exam.submitted` | Analytics, Notification |
| `content-events` | `lesson.viewed` | Analytics |

---

## 🚀 5. Kubernetes Deployment

### 5.1 Namespace ve Resource Quotas

```yaml
# namespace.yaml
apiVersion: v1
kind: Namespace
metadata:
  name: edu-platform
---
apiVersion: v1
kind: ResourceQuota
metadata:
  name: edu-platform-quota
  namespace: edu-platform
spec:
  hard:
    requests.cpu: "100"
    requests.memory: 200Gi
    limits.cpu: "200"
    limits.memory: 400Gi
    pods: "500"
```

### 5.2 Service Deployment Örneği

```yaml
# speed-reading-service.yaml
apiVersion: apps/v1
kind: Deployment
metadata:
  name: speed-reading-service
  namespace: edu-platform
spec:
  replicas: 5
  selector:
    matchLabels:
      app: speed-reading-service
  template:
    metadata:
      labels:
        app: speed-reading-service
    spec:
      containers:
      - name: api
        image: edu-platform/speed-reading:latest
        ports:
        - containerPort: 8080
        resources:
          requests:
            memory: "256Mi"
            cpu: "250m"
          limits:
            memory: "512Mi"
            cpu: "500m"
        livenessProbe:
          httpGet:
            path: /health/live
            port: 8080
          initialDelaySeconds: 10
          periodSeconds: 10
        readinessProbe:
          httpGet:
            path: /health/ready
            port: 8080
          initialDelaySeconds: 5
          periodSeconds: 5
        env:
        - name: ConnectionStrings__DefaultConnection
          valueFrom:
            secretKeyRef:
              name: db-secrets
              key: speedread-connection
---
apiVersion: autoscaling/v2
kind: HorizontalPodAutoscaler
metadata:
  name: speed-reading-hpa
  namespace: edu-platform
spec:
  scaleTargetRef:
    apiVersion: apps/v1
    kind: Deployment
    name: speed-reading-service
  minReplicas: 5
  maxReplicas: 30
  metrics:
  - type: Resource
    resource:
      name: cpu
      target:
        type: Utilization
        averageUtilization: 70
  - type: Resource
    resource:
      name: memory
      target:
        type: Utilization
        averageUtilization: 80
```

### 5.3 Ingress Configuration (Nginx - Ücretsiz)

```yaml
# ingress.yaml
apiVersion: networking.k8s.io/v1
kind: Ingress
metadata:
  name: edu-platform-ingress
  namespace: edu-platform
  annotations:
    nginx.ingress.kubernetes.io/rate-limit: "100"
    nginx.ingress.kubernetes.io/rate-limit-window: "1m"
spec:
  ingressClassName: nginx
  tls:
  - hosts:
    - api.eduplatform.com
    secretName: tls-secret
  rules:
  - host: api.eduplatform.com
    http:
      paths:
      - path: /api/identity
        pathType: Prefix
        backend:
          service:
            name: identity-service
            port:
              number: 80
      - path: /api/speedreading
        pathType: Prefix
        backend:
          service:
            name: speed-reading-service
            port:
              number: 80
```

---

## 📊 6. Observability Stack (Tamamı Ücretsiz)

### 6.1 Logging: Serilog + ELK

```csharp
// Program.cs
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
    .Enrich.FromLogContext()
    .Enrich.WithCorrelationId()
    .Enrich.WithMachineName()
    .Enrich.WithEnvironmentName()
    .WriteTo.Console(new JsonFormatter())
    .WriteTo.Elasticsearch(new ElasticsearchSinkOptions(
        new Uri("http://elasticsearch:9200"))
    {
        IndexFormat = "edu-platform-logs-{0:yyyy.MM.dd}",
        AutoRegisterTemplate = true
    })
    .CreateLogger();
```

### 6.2 Metrics: Prometheus + Grafana

```yaml
# prometheus.yml
global:
  scrape_interval: 15s

scrape_configs:
  - job_name: 'edu-platform-services'
    kubernetes_sd_configs:
      - role: pod
    relabel_configs:
      - source_labels: [__meta_kubernetes_pod_annotation_prometheus_io_scrape]
        action: keep
        regex: true
```

### 6.3 Tracing: OpenTelemetry + Jaeger

```csharp
// OpenTelemetry Configuration
services.AddOpenTelemetry()
    .WithTracing(builder =>
    {
        builder
            .SetResourceBuilder(ResourceBuilder.CreateDefault()
                .AddService("speed-reading-service"))
            .AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation()
            .AddEntityFrameworkCoreInstrumentation()
            .AddJaegerExporter(options =>
            {
                options.AgentHost = "jaeger";
                options.AgentPort = 6831;
            });
    })
    .WithMetrics(builder =>
    {
        builder
            .AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation()
            .AddPrometheusExporter();
    });
```

---

## 🤖 7. ML/AI Pipeline (Ücretsiz)

### 7.1 Data Collection

```csharp
// Learning Event Collector
public class LearningEventCollector
{
    private readonly IKafkaProducer<LearningEvent> _producer;

    public async Task CollectAsync(LearningEvent @event)
    {
        await _producer.ProduceAsync("learning-events", @event);
    }
}

public record LearningEvent(
    Guid StudentId,
    string EventType,
    Dictionary<string, object> Properties,
    DateTime Timestamp
);
```

### 7.2 ML.NET ile Model Training

```csharp
// Student Performance Prediction
public class StudentPerformanceModel
{
    public static ITransformer TrainModel(MLContext mlContext, IDataView data)
    {
        var pipeline = mlContext.Transforms.Categorical
            .OneHotEncoding("LearningStyleEncoded", "LearningStyle")
            .Append(mlContext.Transforms.Concatenate("Features",
                "StudyHours", "ExerciseCount", "AvgScore", "LearningStyleEncoded"))
            .Append(mlContext.Regression.Trainers.FastTree(
                labelColumnName: "PredictedScore",
                featureColumnName: "Features"));

        return pipeline.Fit(data);
    }
}
```

### 7.3 Recommendation Engine

```csharp
// Content Recommendation Service
public class RecommendationService
{
    public async Task<List<ContentRecommendation>> GetRecommendationsAsync(
        Guid studentId, int count = 5)
    {
        // Collaborative Filtering + Content-Based Hybrid
        var studentProfile = await _studentRepo.GetProfileAsync(studentId);
        var similarStudents = await FindSimilarStudentsAsync(studentProfile);
        var contentScores = await CalculateContentScoresAsync(
            studentProfile, similarStudents);
        
        return contentScores
            .OrderByDescending(x => x.Score)
            .Take(count)
            .ToList();
    }
}
```

---

## 💰 8. Maliyet Analizi (Ücretsiz Yazılım)

### 8.1 Yazılım Maliyeti: $0

| Kategori | Ücretli Alternatif | Ücretsiz Seçim | Tasarruf/Yıl |
|----------|-------------------|----------------|--------------|
| CQRS/Mediator | MediatR (MIT) | MediatR | $0 |
| Identity Server | Duende ($1,500+/yıl) | Keycloak | $1,500+ |
| APM | Datadog (~$15/host/ay) | Prometheus+Grafana | $3,600+ |
| Logging | Splunk (~$150/GB) | ELK Stack | $10,000+ |
| Message Queue | AWS SQS (~$0.40/M msg) | RabbitMQ | Variable |
| **TOPLAM** | | | **$15,000+/yıl** |

### 8.2 Altyapı Maliyeti (Tahmini)

| Kaynak | Miktar | Aylık Maliyet (Cloud) |
|--------|--------|----------------------|
| Kubernetes Nodes | 10x 8vCPU, 32GB | ~$2,000 |
| PostgreSQL (Managed) | 3x HA cluster | ~$800 |
| Redis Cluster | 6 nodes | ~$400 |
| Elasticsearch | 3 nodes | ~$500 |
| Object Storage | 5TB | ~$100 |
| Bandwidth | 10TB/ay | ~$500 |
| **TOPLAM** | | **~$4,300/ay** |

---

## 📋 9. Checklist: Production Readiness

### Pre-Launch
- [ ] Tüm servisler containerize edildi
- [ ] Health check endpoint'leri aktif
- [ ] Logging ve tracing konfigüre edildi
- [ ] Secret management kuruldu (Vault/K8s Secrets)
- [ ] SSL/TLS sertifikaları hazır
- [ ] Database backup stratejisi belirlendi
- [ ] Disaster recovery planı oluşturuldu

### Security
- [ ] OWASP Top 10 kontrolleri yapıldı
- [ ] Rate limiting aktif
- [ ] Input validation tüm endpoint'lerde
- [ ] JWT token rotation
- [ ] CORS policy tanımlı
- [ ] KVKK/GDPR uyumluluğu sağlandı

### Performance
- [ ] Load testing (1M concurrent users simülasyonu)
- [ ] Database query optimization
- [ ] Caching stratejisi uygulandı
- [ ] CDN konfigürasyonu tamamlandı
- [ ] Auto-scaling test edildi

---

## 📚 10. Referanslar

### Akademik Çalışmalar
1. **Learning Analytics**: Clickstream analysis for student behavior prediction
2. **Adaptive Learning Systems**: AI-powered personalized learning paths
3. **EdTech Scalability**: Khan Academy case study (100M+ users)

### Açık Kaynak Projeler
- [TS.MediatR](https://github.com/TS-NuGet-Packages/TS.MediatR) - MIT License
- [Keycloak](https://www.keycloak.org/) - Apache 2.0
- [OpenIddict](https://github.com/openiddict) - Apache 2.0
- [Wolverine](https://wolverine.netlify.app/) - MIT License

---

*Rapor Tarihi: 2024-12-20*
*Versiyon: 2.0 - %100 Ücretsiz Teknolojiler*

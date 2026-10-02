$ErrorActionPreference = 'Stop'
if ($env:E2E_DISPOSABLE_ENV -ne 'true') { throw 'Only the disposable local E2E environment is supported.' }
$planningRoot = (Resolve-Path (Join-Path $PSScriptRoot '../../..')).Path
$planningAssembly = Join-Path $planningRoot 'services/coaching-service/Coaching.API/bin/Debug/net10.0/Coaching.API.dll'
if (-not (Test-Path -LiteralPath $planningAssembly)) { throw 'Build Coaching.API before starting the fixture.' }
$env:ASPNETCORE_ENVIRONMENT = 'Development'
$env:ASPNETCORE_URLS = 'http://127.0.0.1:5006'
$env:ConnectionStrings__Redis = '127.0.0.1:6379,abortConnect=false'
$env:JWT_SECRET = 'local-planning-e2e-signing-key-20261002-never-use-in-production-123456789'
$env:INTERNAL_SERVICE_API_KEY = 'local-planning-e2e-internal-key-never-use-in-production-123456789'
$env:RABBITMQ_HOST = '127.0.0.1'
$env:RABBITMQ_DEFAULT_USER = 'planning_test'
$env:RABBITMQ_DEFAULT_PASS = 'disposable-planning-only'
$env:Coaching__Attachments__Scanner__Provider = 'Local'
$env:Coaching__Attachments__RootPath = Join-Path $planningRoot 'artifacts/local-planning-e2e/attachments'
$env:Coaching__CmsMedia__RootPath = Join-Path $planningRoot 'artifacts/local-planning-e2e/media'
$env:Logging__LogLevel__Default = 'Warning'
Set-Location -LiteralPath $planningRoot
& dotnet $planningAssembly --contentRoot (Join-Path $planningRoot 'services/coaching-service/Coaching.API') `
    --ConnectionStrings:DefaultConnection 'Host=127.0.0.1;Port=55441;Database=coaching_planning_e2e;Username=planning_test;Password=disposable-planning-only' `
    --Services:IdentityService 'http://127.0.0.1:4600'

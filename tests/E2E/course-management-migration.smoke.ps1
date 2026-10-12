$ErrorActionPreference = 'Stop'
$containerName = 'course-foundation-test-' + [Guid]::NewGuid().ToString('N')
$previousConnection = $env:COURSE_MANAGEMENT_CONNECTION_STRING
$previousTestConnection = $env:COURSE_MANAGEMENT_TEST_CONNECTION_STRING
$previousTestContainer = $env:COURSE_MANAGEMENT_TEST_DB_CONTAINER
$repoRoot = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$projectPath = Join-Path $repoRoot 'services/course-management-service/CourseManagement.Infrastructure/CourseManagement.Infrastructure.csproj'
try {
    docker run -d --name $containerName -e POSTGRES_DB=course_test -e POSTGRES_USER=course_test -e POSTGRES_PASSWORD=local-test-only -p '127.0.0.1::5432' postgres:16-alpine | Out-Null
    if ($LASTEXITCODE -ne 0) { throw 'Disposable database could not start.' }
    $ready = $false
    for ($attempt = 0; $attempt -lt 30; $attempt++) {
        docker exec $containerName pg_isready -U course_test -d course_test *> $null
        if ($LASTEXITCODE -eq 0) { $ready = $true; break }
        Start-Sleep -Milliseconds 500
    }
    if (-not $ready) { throw 'Disposable database did not become ready.' }
    $binding = docker port $containerName '5432/tcp'
    if ($LASTEXITCODE -ne 0 -or $binding -notmatch '^127\.0\.0\.1:(\d+)$') { throw 'Unexpected test database binding.' }
    $env:COURSE_MANAGEMENT_CONNECTION_STRING = "Host=127.0.0.1;Port=$($Matches[1]);Database=course_test;Username=course_test;Password=local-test-only"
    dotnet ef database update --project $projectPath --no-build
    if ($LASTEXITCODE -ne 0) { throw 'Initial migration failed.' }
    dotnet ef database update --project $projectPath --no-build
    if ($LASTEXITCODE -ne 0) { throw 'Repeated migration failed.' }
    $count = docker exec $containerName psql -U course_test -d course_test -tAc "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema='public' AND table_name IN ('UserProfiles', 'Workspaces');"
    if ($LASTEXITCODE -ne 0 -or $count.Trim() -ne '2') { throw 'Expected product tables are missing.' }
    $env:COURSE_MANAGEMENT_TEST_CONNECTION_STRING = $env:COURSE_MANAGEMENT_CONNECTION_STRING
    $env:COURSE_MANAGEMENT_TEST_DB_CONTAINER = $containerName
    node --test (Join-Path $PSScriptRoot 'course-management-auth.integration.test.mjs')
    if ($LASTEXITCODE -ne 0) { throw 'PostgreSQL API isolation tests failed.' }
    Write-Output 'PASS: Initial migration and repeat application on an isolated PostgreSQL database.'
} finally {
    $env:COURSE_MANAGEMENT_CONNECTION_STRING = $previousConnection
    $env:COURSE_MANAGEMENT_TEST_CONNECTION_STRING = $previousTestConnection
    $env:COURSE_MANAGEMENT_TEST_DB_CONTAINER = $previousTestContainer
    docker rm -f $containerName | Out-Null
}

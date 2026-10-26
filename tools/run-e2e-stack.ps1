<#
.SYNOPSIS
  Runs CI's "Distributed privacy erasure E2E" job on a developer machine: the whole
  service stack in Docker (Identity, Coaching, Notification, Speed Reading, Gateway,
  Course Management, PostgreSQL, Redis, RabbitMQ) and the Docker-backed E2E tests.
  Results go to e2e-results/ (one log per step, summary.txt, service logs on failure).

.PARAMETER Keep
  Leave the stack running afterwards (Gateway on the port from .env.example) for manual checks.
  Remove it later with:  docker compose -p privacy-e2e-local down -v

.EXAMPLE
  powershell -ExecutionPolicy Bypass -File tools\run-e2e-stack.ps1
#>
[CmdletBinding()]
param([switch]$Keep)

$ErrorActionPreference = 'Continue'
$root = Split-Path -Parent $PSScriptRoot
$out = Join-Path $root 'e2e-results'
New-Item -ItemType Directory -Force -Path $out | Out-Null
Set-Location $root

docker info --format '{{.ServerVersion}}' *> $null
if ($LASTEXITCODE -ne 0) { throw 'Docker is not running. Start Docker Desktop and run the script again.' }
# Get-Command, not $LASTEXITCODE: a missing executable leaves the previous exit code in place.
if (-not (Get-Command node -ErrorAction SilentlyContinue)) {
    throw 'Node.js 22+ is required for the E2E tests. Install it (winget install OpenJS.NodeJS.LTS), open a new PowerShell and run again.'
}

# Checkouts made before .gitattributes pinned *.sh to LF may still hold CRLF copies,
# which Linux containers cannot execute (PostgreSQL init scripts exit with 126).
foreach ($script in (git ls-files -- '*.sh')) {
    if ((Get-Content -Raw $script) -match "`r`n") {
        Remove-Item $script
        git checkout -- $script
        Write-Host "Re-checked out with LF line endings: $script"
    }
}

# Same disposable settings as CI; a separate project name keeps your own dev stack untouched.
$env:COMPOSE_PROJECT_NAME = 'privacy-e2e-local'
$env:E2E_COMPOSE_ENV_FILE = '.env.example'
$env:E2E_DISPOSABLE_ENV = 'true'
$env:ENVIRONMENT = 'Development'
$env:POSTGRES_USER = 'eduplatform'
$env:POSTGRES_PASSWORD = 'ci-disposable-postgres-012345678901234567890123'
$env:RABBITMQ_DEFAULT_USER = 'eduplatform'
$env:RABBITMQ_DEFAULT_PASS = 'ci-disposable-rabbit-012345678901234567890123'
$env:REDIS_PASSWORD = 'ci-disposable-redis-012345678901234567890123'
$env:COURSE_MANAGEMENT_DB_PASSWORD = 'ci-disposable-course-db-012345678901234567890123'
$env:E2E_COURSE_MANAGEMENT_ENABLED = 'true'
$env:SPEED_READING_OWNED_CONNECTION_STRING = 'Host=postgres;Port=5432;Database=speedreading_owned_db;Username=eduplatform;Password=ci-disposable-postgres-012345678901234567890123'

$compose = @('compose', '--env-file', '.env.example', '-f', 'docker-compose.yml', '-f', 'docker-compose.course-management.yml', '--profile', 'speed-reading')
$summary = @("E2E run $(Get-Date -Format s)  commit $(git rev-parse --short HEAD)", '')
$failed = $false

# Windows PowerShell strips the inner quotes of {{.Label "..."}} on the way to docker, so read
# the whole label list and pick the compose project out of it.
function Get-ComposeProject([string]$labels) {
    if ($labels -match 'com\.docker\.compose\.project=([^,|]+)') { $Matches[1] } else { '' }
}

function Step([string]$name, [scriptblock]$action) {
    $log = Join-Path $out (($name -replace '[^A-Za-z0-9-]+', '-') + '.log')
    Write-Host "=== $name"
    $started = Get-Date
    & $action *>&1 | Tee-Object -FilePath $log | Out-Null
    $code = $LASTEXITCODE
    $minutes = [math]::Round(((Get-Date) - $started).TotalMinutes, 1)
    $line = "{0,-60} exit {1}  {2} min" -f $name, $code, $minutes
    $script:summary += $line
    Write-Host $line
    return $code -eq 0
}

try {
    # A previous run that was interrupted can leave this disposable project behind; remove it first.
    docker @compose down -v --remove-orphans *> $null

    # The compose file pins container names (postgres, rabbitmq, redis, mailcatcher) and host ports,
    # so another stack that uses them must be stopped first. Name the owner instead of guessing.
    $conflicts = @()
    foreach ($name in 'postgres', 'rabbitmq', 'redis', 'mailcatcher') {
        $labels = docker ps -a --filter "name=^/$name$" --format '{{.Labels}}'
        if ($labels) { $conflicts += "container '$name' (compose project '$(Get-ComposeProject $labels)')" }
    }
    $ports = Select-String -Path .env.example -Pattern '^(POSTGRES_PORT|REDIS_PORT|RABBITMQ_PORT|GATEWAY_PORT)=(\d+)' |
        ForEach-Object { [int]$_.Matches[0].Groups[2].Value }
    foreach ($port in $ports) {
        if ((Test-NetConnection 127.0.0.1 -Port $port -WarningAction SilentlyContinue).TcpTestSucceeded) {
            $owner = docker ps --filter "publish=$port" --format '{{.Names}}|{{.Labels}}' | Select-Object -First 1
            $conflicts += "port $port used by $(if ($owner) { '{0} (compose project {1})' -f $owner.Split('|')[0], (Get-ComposeProject $owner) } else { 'a program outside Docker' })"
        }
    }
    if ($conflicts) {
        $projects = docker ps -a --format '{{.Labels}}' | ForEach-Object { Get-ComposeProject $_ } | Where-Object { $_ } | Sort-Object -Unique
        throw ("Another stack is in the way: " + ($conflicts -join '; ') +
            ". Stop it and run again, e.g.: docker compose -p <project> stop   (projects running now: $($projects -join ', '))")
    }

    $ok = Step 'start-stack' { docker @compose up -d --build postgres redis rabbitmq identity-service coaching-service notification-service speed-reading-service api-gateway course-management-db }
    if ($ok) {
        $ok = Step 'course-management-migrate-and-start' {
            docker @compose build course-management-service
            docker @compose run --rm --no-deps course-management-service --migrate-only
            if ($LASTEXITCODE -eq 0) { docker @compose up -d course-management-service }
        }
    }
    $tests = [ordered]@{
        'erasure-and-restore' = @('tests/E2E/support/privacy-docker.test.mjs', 'tests/E2E/privacy-erasure.docker.test.mjs',
            'tests/E2E/coaching-backup-restore.docker.test.mjs', 'tests/E2E/coaching-data-export-route.contract.test.mjs')
        'coaching-export'     = @('tests/E2E/coaching-data-export.docker.test.mjs')
        'broker-recovery'     = @('tests/E2E/coaching-broker-recovery.docker.test.mjs')
        'attachments'         = @('tests/E2E/coaching-attachment.docker.test.mjs')
        'admin-tenant'        = @('tests/E2E/coaching-admin-tenant.docker.test.mjs')
    }
    if ($ok) {
        foreach ($name in $tests.Keys) {
            # Unlike CI, keep going after a failure so one run shows every broken step.
            $files = $tests[$name]
            if (-not (Step $name { node --test @files })) { $failed = $true }
        }
    }
    else { $failed = $true }
}
catch {
    $summary += "ERROR: $($_.Exception.Message)"
    $failed = $true
}
finally {
    if ($failed) {
        docker @compose logs --no-color --tail 200 identity-service coaching-service notification-service speed-reading-service api-gateway course-management-service *>&1 |
            Set-Content -Encoding UTF8 (Join-Path $out 'service-logs.txt')
    }
    if (-not $Keep) { docker @compose down -v --remove-orphans *> $null }
    $summary += ''
    $summary += $(if ($failed) { 'RESULT: FAILED (see the step logs and service-logs.txt)' } else { 'RESULT: ALL PASSED' })
    $summary | Set-Content -Encoding UTF8 (Join-Path $out 'summary.txt')
}
Write-Host ''
Write-Host "Done. Summary: $(Join-Path $out 'summary.txt')"

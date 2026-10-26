<#
.SYNOPSIS
  Runs every .NET test project the way CI does (Release, Docker-backed Testcontainers)
  and writes one TRX, one console log per project and a summary to test-results/.

.EXAMPLE
  # From a clean worktree, so your working copy is untouched:
  git fetch origin fix/integration-test-suite
  git worktree add ..\microservice-testrun origin/fix/integration-test-suite
  powershell -ExecutionPolicy Bypass -File ..\microservice-testrun\tools\run-full-tests.ps1
#>
[CmdletBinding()]
param(
    [string]$Configuration = 'Release',
    # Optional test filter applied to every project, e.g. 'FullyQualifiedName~Notification'.
    [string]$Filter
)

# Continue: Windows PowerShell turns native stderr lines into errors, and dotnet/docker
# write progress there. Failures are checked through exit codes instead.
$ErrorActionPreference = 'Continue'
$root = Split-Path -Parent $PSScriptRoot
$out = Join-Path $root 'test-results'
New-Item -ItemType Directory -Force -Path $out | Out-Null
Set-Location $root

docker info --format '{{.ServerVersion}}' *> $null
if ($LASTEXITCODE -ne 0) { throw 'Docker is not running. Start Docker Desktop and run the script again.' }

# Gateway tests expect Redis on 127.0.0.1:6379 (a CI service). Start a throwaway one if needed.
$redisContainer = $null
$redisUp = (Test-NetConnection 127.0.0.1 -Port 6379 -WarningAction SilentlyContinue).TcpTestSucceeded
if (-not $redisUp) {
    $redisContainer = 'eduplatform-testrun-redis'
    docker rm -f $redisContainer *> $null
    docker run -d --rm --name $redisContainer -p 127.0.0.1:6379:6379 redis:7-alpine | Out-Null
    Start-Sleep -Seconds 3
}

$projects = @(
    'tests/Unit/EduPlatform.Gateway.UnitTests/EduPlatform.Gateway.UnitTests.csproj',
    'tests/Unit/SpeedReading.Application.UnitTests/SpeedReading.Application.UnitTests.csproj',
    'tests/Unit/CourseManagement.Application.UnitTests/CourseManagement.Application.UnitTests.csproj',
    'tests/Integration/Identity.API.IntegrationTests/Identity.API.IntegrationTests.csproj',
    'tests/Integration/CourseManagement.IntegrationTests/CourseManagement.IntegrationTests.csproj',
    'tests/Integration/Coaching.Curriculum.IntegrationTests/Coaching.Curriculum.IntegrationTests.csproj',
    'tests/Integration/SpeedReading.ProgramProgression.IntegrationTests/SpeedReading.ProgramProgression.IntegrationTests.csproj'
)

$env:GATEWAY_TEST_REDIS_HOST = '127.0.0.1'
$env:GATEWAY_TEST_REDIS_PORT = '6379'
$env:RUN_GATEWAY_HEALTH_TESTS = 'false'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'

$summary = @("Test run $(Get-Date -Format s)  commit $(git rev-parse --short HEAD)", '')
try {
    foreach ($project in $projects) {
        $name = [IO.Path]::GetFileNameWithoutExtension($project)
        $log = Join-Path $out "$name.log"
        Write-Host "=== $name"
        $arguments = @('test', $project, '--configuration', $Configuration,
            '--logger', "trx;LogFileName=$name.trx", '--logger', 'console;verbosity=normal',
            '--results-directory', $out)
        if ($Filter) { $arguments += @('--filter', $Filter) }
        $started = Get-Date
        & dotnet @arguments *>&1 | Tee-Object -FilePath $log | Out-Null
        $code = $LASTEXITCODE
        $minutes = [math]::Round(((Get-Date) - $started).TotalMinutes, 1)

        $counts = 'no TRX (build or run failure, see log)'
        $failures = @()
        $trx = Join-Path $out "$name.trx"
        if (Test-Path $trx) {
            [xml]$xml = Get-Content $trx
            $c = $xml.TestRun.ResultSummary.Counters
            $counts = "total $($c.total), passed $($c.passed), failed $($c.failed), not run $($c.notExecuted)"
            $failed = $xml.TestRun.Results.UnitTestResult | Where-Object { $_.outcome -eq 'Failed' }
            foreach ($test in $failed) {
                $message = ($test.Output.ErrorInfo.Message -replace '\s+', ' ')
                if ($message.Length -gt 300) { $message = $message.Substring(0, 300) }
                $failures += "    FAILED $($test.testName) :: $message"
            }
        }
        $line = "{0,-55} exit {1}  {2} min  {3}" -f $name, $code, $minutes, $counts
        $summary += $line
        $summary += $failures
        Write-Host $line
    }
}
finally {
    if ($redisContainer) { docker rm -f $redisContainer *> $null }
    $summary | Set-Content -Encoding UTF8 (Join-Path $out 'summary.txt')
}

Write-Host ''
Write-Host "Done. Summary: $(Join-Path $out 'summary.txt')"

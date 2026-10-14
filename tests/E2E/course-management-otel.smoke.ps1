[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Debug'
)

$ErrorActionPreference = 'Stop'
$smokeId = [Guid]::NewGuid().ToString('N')
$networkName = "course-otel-$smokeId"
$tempoName = "course-otel-tempo-$smokeId"
$collectorName = "course-otel-collector-$smokeId"
$prometheusName = "course-otel-prometheus-$smokeId"
$repoRoot = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$apiAssemblyPath = Join-Path $repoRoot "services/course-management-service/CourseManagement.API/bin/$Configuration/net10.0/CourseManagement.API.dll"
$tempoConfigPath = Join-Path $repoRoot 'monitoring/tempo/tempo.yml'
$collectorConfigPath = Join-Path $repoRoot 'monitoring/otel/otel-collector.yml'
$prometheusConfigPath = Join-Path $repoRoot 'monitoring/prometheus'
$smokeDataRoot = Join-Path ([System.IO.Path]::GetTempPath()) "course-otel-smoke-$smokeId"
$attachmentRoot = Join-Path $smokeDataRoot 'attachments'
$apiProcess = $null
$apiStdOut = $null
$apiStdErr = $null

function Get-FreeLoopbackPort {
    $listener = [System.Net.Sockets.TcpListener]::new([System.Net.IPAddress]::Loopback, 0)
    try {
        $listener.Start()
        return $listener.LocalEndpoint.Port
    }
    finally {
        $listener.Stop()
    }
}

function Get-PublishedLoopbackPort([string]$container, [string]$containerPort) {
    $binding = & docker port $container $containerPort
    if ($LASTEXITCODE -ne 0 -or $binding -notmatch '^127\.0\.0\.1:(\d+)$') {
        throw "Could not resolve the loopback port for $container/$containerPort."
    }
    return [int]$Matches[1]
}

function Wait-HttpReady([string]$uri, [int]$timeoutSeconds, [string]$description) {
    $deadline = [DateTimeOffset]::UtcNow.AddSeconds($timeoutSeconds)
    while ([DateTimeOffset]::UtcNow -lt $deadline) {
        try {
            $response = Invoke-WebRequest -Uri $uri -TimeoutSec 3 -SkipHttpErrorCheck
            if ($response.StatusCode -eq 200) { return }
        }
        catch { }
        Start-Sleep -Milliseconds 500
    }
    throw "$description did not become ready."
}

try {
    if (-not (Test-Path -LiteralPath $apiAssemblyPath)) { throw 'Course Management API build output is missing.' }
    New-Item -ItemType Directory -Path $attachmentRoot -Force | Out-Null
    & docker network create $networkName | Out-Null
    if ($LASTEXITCODE -ne 0) { throw 'The isolated telemetry network could not be created.' }

    $tempoVolume = "${tempoConfigPath}:/etc/tempo/tempo.yml:ro"
    & docker run --detach --name $tempoName --network $networkName --network-alias tempo `
        --publish '127.0.0.1::3200' --volume $tempoVolume grafana/tempo:2.8.2 `
        '-config.file=/etc/tempo/tempo.yml' | Out-Null
    if ($LASTEXITCODE -ne 0) { throw 'Tempo could not start.' }
    $tempoPort = Get-PublishedLoopbackPort $tempoName '3200/tcp'
    Wait-HttpReady "http://127.0.0.1:$tempoPort/ready" 45 'Tempo'

    $collectorVolume = "${collectorConfigPath}:/etc/otelcol-contrib/otel-collector.yml:ro"
    & docker run --detach --name $collectorName --network $networkName --network-alias otel-collector `
        --publish '127.0.0.1::4317' --publish '127.0.0.1::9464' --publish '127.0.0.1::13133' `
        --volume $collectorVolume otel/opentelemetry-collector-contrib:0.133.0 `
        '--config=/etc/otelcol-contrib/otel-collector.yml' | Out-Null
    if ($LASTEXITCODE -ne 0) { throw 'The OpenTelemetry Collector could not start.' }
    $collectorPort = Get-PublishedLoopbackPort $collectorName '4317/tcp'
    $metricsPort = Get-PublishedLoopbackPort $collectorName '9464/tcp'
    $collectorHealthPort = Get-PublishedLoopbackPort $collectorName '13133/tcp'
    Wait-HttpReady "http://127.0.0.1:$collectorHealthPort/" 30 'The OpenTelemetry Collector'

    $prometheusVolume = "${prometheusConfigPath}:/etc/prometheus:ro"
    & docker run --detach --name $prometheusName --network $networkName `
        --publish '127.0.0.1::9090' --volume $prometheusVolume prom/prometheus:v3.5.0 `
        '--config.file=/etc/prometheus/prometheus.yml' '--storage.tsdb.path=/prometheus' | Out-Null
    if ($LASTEXITCODE -ne 0) { throw 'Prometheus could not start.' }
    $prometheusPort = Get-PublishedLoopbackPort $prometheusName '9090/tcp'
    $prometheusUrl = "http://127.0.0.1:$prometheusPort"
    Wait-HttpReady "$prometheusUrl/-/ready" 45 'Prometheus'

    $apiPort = Get-FreeLoopbackPort
    $apiUrl = "http://127.0.0.1:$apiPort"
    $apiStdOut = Join-Path $smokeDataRoot 'api.stdout.log'
    $apiStdErr = Join-Path $smokeDataRoot 'api.stderr.log'
    $smokeEnvironment = @{
        ASPNETCORE_ENVIRONMENT = 'Development'
        ASPNETCORE_URLS = $apiUrl
        AllowedHosts = 'localhost;course-management'
        COURSE_MANAGEMENT_CONNECTION_STRING = 'Host=127.0.0.1;Port=1;Database=course_management;Username=smoke;Password=smoke;Timeout=1;Command Timeout=1'
        JWT_SECRET = 'course-management-local-smoke-secret-not-for-real-tokens'
        JWT_ISSUER = 'http://course-management-smoke.local'
        JWT_AUDIENCE = 'course-management-smoke'
        CourseManagement__AssignmentAttachments__RootPath = $attachmentRoot
        CourseManagement__AssignmentAttachments__ScannerProvider = 'Development'
        OTEL_EXPORTER_OTLP_ENDPOINT = "http://127.0.0.1:$collectorPort"
        OTEL_SERVICE_NAME = 'EduPlatform.CourseManagement'
        OTEL_TRACES_SAMPLER_ARG = '1.0'
        OTEL_BSP_SCHEDULE_DELAY = '1000'
        OTEL_METRIC_EXPORT_INTERVAL = '5000'
    }
    $previousEnvironment = @{}
    foreach ($key in $smokeEnvironment.Keys) {
        $previousEnvironment[$key] = [Environment]::GetEnvironmentVariable($key, 'Process')
        [Environment]::SetEnvironmentVariable($key, $smokeEnvironment[$key], 'Process')
    }
    try {
        $startParameters = @{
            FilePath = (Get-Command dotnet).Source
            ArgumentList = @($apiAssemblyPath)
            WorkingDirectory = Split-Path $apiAssemblyPath
            PassThru = $true
            RedirectStandardOutput = $apiStdOut
            RedirectStandardError = $apiStdErr
        }
        if ($IsWindows) { $startParameters.WindowStyle = 'Hidden' }
        $apiProcess = Start-Process @startParameters
    }
    finally {
        foreach ($key in $smokeEnvironment.Keys) {
            [Environment]::SetEnvironmentVariable($key, $previousEnvironment[$key], 'Process')
        }
    }

    $apiListening = $false
    for ($attempt = 0; $attempt -lt 40; $attempt++) {
        if (Test-Path -LiteralPath $apiStdOut) {
            $apiListening = Select-String -Path $apiStdOut -SimpleMatch "Now listening on: $apiUrl" -Quiet
            if ($apiListening) { break }
        }
        if ($apiProcess.HasExited) {
            if (Test-Path -LiteralPath $apiStdErr) { Get-Content -LiteralPath $apiStdErr -Tail 60 }
            throw 'Course Management API exited before listening.'
        }
        Start-Sleep -Milliseconds 500
    }
    if (-not $apiListening) { throw 'Course Management API did not start listening.' }

    foreach ($request in 1..3) {
        $response = Invoke-WebRequest -Uri "$apiUrl/health/live" -Headers @{ Host = 'localhost' } `
            -TimeoutSec 5 -SkipHttpErrorCheck
        if ($response.StatusCode -ne 200) { throw "Course Management liveness request $request returned $($response.StatusCode)." }
    }

    $tempoSearch = "http://127.0.0.1:$tempoPort/api/search?tags=service.name%3DEduPlatform.CourseManagement"
    $metricUrl = "http://127.0.0.1:$metricsPort/metrics"
    $traceResults = @()
    $metricSeries = @()
    $prometheusSeriesCount = 0
    $prometheusScrapeHealthy = $false
    $prometheusUpQuery = [System.Uri]::EscapeDataString('up{job="eduplatform-otel"}')
    $prometheusSeriesQuery = [System.Uri]::EscapeDataString('count({service_name="EduPlatform.CourseManagement"})')
    $deadline = [DateTimeOffset]::UtcNow.AddSeconds(60)
    while ([DateTimeOffset]::UtcNow -lt $deadline) {
        try {
            $traceResponse = Invoke-RestMethod -Uri $tempoSearch -TimeoutSec 5
            $traceResults = @($traceResponse.traces | Where-Object { $_.rootServiceName -eq 'EduPlatform.CourseManagement' })
            $metricsResponse = Invoke-WebRequest -Uri $metricUrl -TimeoutSec 5
            $metricSeries = @($metricsResponse.Content -split "`n" | Where-Object {
                $_ -match 'service_name="EduPlatform\.CourseManagement"'
            })
            $prometheusUpResponse = Invoke-RestMethod -Uri "$prometheusUrl/api/v1/query?query=$prometheusUpQuery" -TimeoutSec 5
            $prometheusUpResults = @($prometheusUpResponse.data.result)
            if ($prometheusUpResults.Count -gt 0) {
                $prometheusScrapeHealthy = [int]$prometheusUpResults[0].value[1] -eq 1
            }
            $prometheusSeriesResponse = Invoke-RestMethod -Uri "$prometheusUrl/api/v1/query?query=$prometheusSeriesQuery" -TimeoutSec 5
            $prometheusSeriesResults = @($prometheusSeriesResponse.data.result)
            if ($prometheusSeriesResults.Count -gt 0) {
                $prometheusSeriesCount = [int]$prometheusSeriesResults[0].value[1]
            }
            if ($traceResults.Count -ge 3 -and $metricSeries.Count -gt 0 `
                -and $prometheusScrapeHealthy -and $prometheusSeriesCount -gt 0) { break }
        }
        catch { }
        Start-Sleep -Seconds 1
    }
    if ($traceResults.Count -lt 3) { throw "Tempo returned only $($traceResults.Count) Course Management traces." }
    if ($metricSeries.Count -eq 0) { throw 'The Collector Prometheus exporter did not include Course Management metrics.' }
    if (-not $prometheusScrapeHealthy) { throw 'Prometheus did not report a healthy scrape of the OpenTelemetry Collector.' }
    if ($prometheusSeriesCount -eq 0) { throw 'Prometheus did not return any scraped Course Management metric series.' }

    $collectorLogs = (& docker logs $collectorName 2>&1 | Out-String)
    if ($collectorLogs -match 'Exporting failed') { throw 'The Collector reported an export failure.' }
    Write-Output "PASS: Course Management OTLP reached Tempo ($($traceResults.Count) traces); the Collector exposed $($metricSeries.Count) Prometheus metric series and Prometheus scraped $prometheusSeriesCount Course Management series; no Collector export failure was reported."
}
catch {
    if ($apiStdOut -and (Test-Path -LiteralPath $apiStdOut)) { Get-Content -LiteralPath $apiStdOut -Tail 60 }
    if ($apiStdErr -and (Test-Path -LiteralPath $apiStdErr)) { Get-Content -LiteralPath $apiStdErr -Tail 60 }
    throw
}
finally {
    if ($apiProcess -and -not $apiProcess.HasExited) {
        Stop-Process -Id $apiProcess.Id -Force -ErrorAction SilentlyContinue
    }
    foreach ($container in @($prometheusName, $collectorName, $tempoName)) {
        & docker rm --force $container *> $null
    }
    & docker network rm $networkName *> $null

    $resolvedTempRoot = [System.IO.Path]::GetFullPath([System.IO.Path]::GetTempPath()).TrimEnd([char[]]@(
        [System.IO.Path]::DirectorySeparatorChar, [System.IO.Path]::AltDirectorySeparatorChar)) + [System.IO.Path]::DirectorySeparatorChar
    $resolvedSmokeDataRoot = [System.IO.Path]::GetFullPath($smokeDataRoot)
    if ($resolvedSmokeDataRoot.StartsWith($resolvedTempRoot, [System.StringComparison]::OrdinalIgnoreCase) -and (Test-Path -LiteralPath $resolvedSmokeDataRoot)) {
        Remove-Item -LiteralPath $resolvedSmokeDataRoot -Recurse -Force
    }
}

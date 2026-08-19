#requires -Version 7.0

[CmdletBinding()]
param(
    [string]$BaseUrl = "http://localhost:5000",
    [string]$Path = "/health",
    [ValidateSet("GET", "POST")]
    [string]$Method = "GET",
    [ValidateRange(1, 512)]
    [int]$Concurrency = 16,
    [ValidateRange(1, 86400)]
    [int]$DurationSeconds = 30,
    [ValidateRange(1, 300)]
    [int]$TimeoutSeconds = 10,
    [string]$Body = "",
    [string]$ContentType = "application/json",
    [string[]]$Header = @(),
    [switch]$Json,
    [switch]$FailOnError
)

Set-StrictMode -Version Latest

if ($Method -eq "GET" -and -not [string]::IsNullOrEmpty($Body)) {
    throw "-Body is only valid when -Method POST is selected."
}

$normalizedPath = if ($Path.StartsWith("/")) { $Path } else { "/$Path" }
$targetUri = "{0}{1}" -f $BaseUrl.TrimEnd('/'), $normalizedPath
$parsedUri = $null
if (-not [Uri]::TryCreate($targetUri, [UriKind]::Absolute, [ref]$parsedUri)) {
    throw "Invalid request URI. Use an absolute http(s) URL."
}
if ($parsedUri.Scheme -notin @("http", "https")) {
    throw "Only http and https request URIs are supported."
}
if (-not [string]::IsNullOrEmpty($parsedUri.UserInfo)) {
    throw "User information in the request URI is not allowed; pass credentials through -Header."
}
$displayTarget = $parsedUri.GetLeftPart([UriPartial]::Path)

$headerMap = @{}
foreach ($headerValue in $Header) {
    $separator = $headerValue.IndexOf("=", [StringComparison]::Ordinal)
    if ($separator -le 0) {
        throw "Headers must use the Name=Value format."
    }

    $name = $headerValue.Substring(0, $separator).Trim()
    $value = $headerValue.Substring($separator + 1)
    if ([string]::IsNullOrWhiteSpace($name)) {
        throw "Header name cannot be empty."
    }

    $headerMap[$name] = $value
}

$deadline = [DateTime]::UtcNow.AddSeconds($DurationSeconds)
$runStopwatch = [System.Diagnostics.Stopwatch]::StartNew()
$stopwatchFrequency = [double][System.Diagnostics.Stopwatch]::Frequency
$workers = 1..$Concurrency

$workerResults = @(
    $workers | ForEach-Object -Parallel {
        $workerHeaders = $using:headerMap
        $workerUri = $using:parsedUri.AbsoluteUri
        $workerMethod = $using:Method

        $requests = 0
        $successes = 0
        $failures = 0
        $latencySampleLimit = 10000
        $latencies = @()
        $statusCodes = @{}
        $errors = @()
        $webSession = $null

        while ([DateTime]::UtcNow -lt $using:deadline) {
            $started = [System.Diagnostics.Stopwatch]::GetTimestamp()
            $response = $null

            try {
                $requestParameters = @{
                    Uri                = $workerUri
                    Method             = $workerMethod
                    Headers            = $workerHeaders
                    TimeoutSec         = $using:TimeoutSeconds
                    SkipHttpErrorCheck = $true
                    MaximumRedirection = 0
                }
                if ($workerMethod -eq "POST") {
                    $requestParameters.Body = $using:Body
                    $requestParameters.ContentType = $using:ContentType
                }
                if ($null -eq $webSession) {
                    $requestParameters.SessionVariable = "webSession"
                }
                else {
                    $requestParameters.WebSession = $webSession
                }

                $response = Invoke-WebRequest @requestParameters
                $elapsedMilliseconds = (([System.Diagnostics.Stopwatch]::GetTimestamp() - $started) * 1000.0) / $using:stopwatchFrequency
                $requests++
                if ($latencies.Count -lt $latencySampleLimit) {
                    $latencies += $elapsedMilliseconds
                }
                elseif ((Get-Random -Minimum 0 -Maximum $requests) -lt $latencySampleLimit) {
                    $latencies[(Get-Random -Minimum 0 -Maximum $latencySampleLimit)] = $elapsedMilliseconds
                }

                $statusCode = [int]$response.StatusCode
                if (-not $statusCodes.ContainsKey($statusCode)) {
                    $statusCodes[$statusCode] = 0
                }
                $statusCodes[$statusCode]++

                if ($statusCode -ge 200 -and $statusCode -lt 300) {
                    $successes++
                }
                else {
                    $failures++
                }
            }
            catch {
                $failures++
                $requests++
                $elapsedMilliseconds = (([System.Diagnostics.Stopwatch]::GetTimestamp() - $started) * 1000.0) / $using:stopwatchFrequency
                if ($latencies.Count -lt $latencySampleLimit) {
                    $latencies += $elapsedMilliseconds
                }
                elseif ((Get-Random -Minimum 0 -Maximum $requests) -lt $latencySampleLimit) {
                    $latencies[(Get-Random -Minimum 0 -Maximum $latencySampleLimit)] = $elapsedMilliseconds
                }
                if ($errors.Count -lt 10) {
                    $errors += [string]$_.FullyQualifiedErrorId
                }
            }
        }

        [ordered]@{
            Worker       = $_
            Requests     = $requests
            Successes    = $successes
            Failures     = $failures
            LatenciesMs  = [double[]]$latencies
            StatusCodes  = $statusCodes
            Errors       = [string[]]$errors
        }
    } -ThrottleLimit $Concurrency
)

function Get-Percentile {
    param(
        [double[]]$Values,
        [double]$Percentile
    )

    if ($null -eq $Values -or $Values.Count -eq 0) {
        return $null
    }

    $sorted = @($Values | Sort-Object)
    $rank = ($Percentile / 100) * ($sorted.Count - 1)
    $lower = [int][Math]::Floor($rank)
    $upper = [int][Math]::Ceiling($rank)
    if ($lower -eq $upper) {
        return [Math]::Round([double]$sorted[$lower], 2)
    }

    $weight = $rank - $lower
    $interpolated = ([double]$sorted[$lower] * (1 - $weight)) + ([double]$sorted[$upper] * $weight)
    return [Math]::Round($interpolated, 2)
}

$allLatencies = [System.Collections.Generic.List[double]]::new()
$totalRequests = 0
$totalSuccesses = 0
$totalFailures = 0
$statusCodes = @{}
$errors = [System.Collections.Generic.List[string]]::new()

foreach ($workerResult in $workerResults) {
    $totalRequests += $workerResult.Requests
    $totalSuccesses += $workerResult.Successes
    $totalFailures += $workerResult.Failures

    foreach ($latency in @($workerResult.LatenciesMs)) {
        [void]$allLatencies.Add([double]$latency)
    }

    foreach ($status in $workerResult.StatusCodes.GetEnumerator()) {
        $code = [string]$status.Key
        if (-not $statusCodes.ContainsKey($code)) {
            $statusCodes[$code] = 0
        }
        $statusCodes[$code] += [int]$status.Value
    }

    foreach ($errorType in @($workerResult.Errors)) {
        if ($errors.Count -lt 10) {
            [void]$errors.Add([string]$errorType)
        }
    }
}

$successRate = if ($totalRequests -eq 0) { 0 } else { [Math]::Round(($totalSuccesses / $totalRequests) * 100, 2) }
$elapsedSeconds = [Math]::Max(0.001, $runStopwatch.Elapsed.TotalSeconds)
$summary = [ordered]@{
    Target             = $displayTarget
    Method             = $Method
    Concurrency        = $Concurrency
    DurationSeconds    = $DurationSeconds
    ElapsedSeconds     = [Math]::Round($elapsedSeconds, 2)
    Requests           = $totalRequests
    RequestsPerSecond   = [Math]::Round($totalRequests / $elapsedSeconds, 2)
    Successes          = $totalSuccesses
    Failures           = $totalFailures
    SuccessRatePercent = $successRate
    P50Ms              = Get-Percentile -Values ([double[]]$allLatencies.ToArray()) -Percentile 50
    P95Ms              = Get-Percentile -Values ([double[]]$allLatencies.ToArray()) -Percentile 95
    P99Ms              = Get-Percentile -Values ([double[]]$allLatencies.ToArray()) -Percentile 99
    StatusCodes        = $statusCodes
    SampleErrors       = [string[]]$errors.ToArray()
}

if ($Json) {
    $summary | ConvertTo-Json -Depth 5
}
else {
    $summary | Format-List
}

if ($FailOnError -and $totalFailures -gt 0) {
    exit 1
}

$ErrorActionPreference = 'Stop'
$containerName = 'course-management-clamav-smoke-' + [Guid]::NewGuid().ToString('N')
$previousHost = $env:COURSE_MANAGEMENT_TEST_CLAMAV_HOST
$previousPort = $env:COURSE_MANAGEMENT_TEST_CLAMAV_PORT
$smokeScript = Join-Path $PSScriptRoot 'course-management-migration.smoke.ps1'

try {
    docker run -d --name $containerName --memory 1536m --cpus '1.0' --pids-limit 128 `
        -p '127.0.0.1::3310' `
        -e CLAMD_CONF_MaxScanSize=10M `
        -e CLAMD_CONF_MaxFileSize=10M `
        -e CLAMD_CONF_StreamMaxLength=10M `
        -e CLAMD_CONF_ConcurrentDatabaseReload=no `
        -e CLAMD_CONF_MaxThreads=4 `
        -e CLAMD_CONF_MaxQueue=16 `
        -e CLAMAV_NO_MILTERD=true `
        -e FRESHCLAM_CHECKS=1 `
        clamav/clamav:1.5.4 | Out-Null
    if ($LASTEXITCODE -ne 0) { throw 'Pinned Course Management ClamAV container failed to start.' }

    $ready = $false
    for ($attempt = 0; $attempt -lt 240; $attempt++) {
        docker exec $containerName clamdscan --ping 3 *> $null
        if ($LASTEXITCODE -eq 0) { $ready = $true; break }
        Start-Sleep -Seconds 1
    }
    if (-not $ready) {
        docker logs $containerName
        throw 'Pinned Course Management ClamAV container did not become ready.'
    }

    $binding = docker port $containerName '3310/tcp'
    if ($LASTEXITCODE -ne 0 -or $binding -notmatch '^127\.0\.0\.1:(\d+)$') {
        throw 'Pinned Course Management ClamAV container has an unexpected port binding.'
    }
    $env:COURSE_MANAGEMENT_TEST_CLAMAV_HOST = '127.0.0.1'
    $env:COURSE_MANAGEMENT_TEST_CLAMAV_PORT = $Matches[1]

    & $smokeScript
} finally {
    if ($null -eq $previousHost) {
        Remove-Item Env:COURSE_MANAGEMENT_TEST_CLAMAV_HOST -ErrorAction SilentlyContinue
    } else {
        $env:COURSE_MANAGEMENT_TEST_CLAMAV_HOST = $previousHost
    }
    if ($null -eq $previousPort) {
        Remove-Item Env:COURSE_MANAGEMENT_TEST_CLAMAV_PORT -ErrorAction SilentlyContinue
    } else {
        $env:COURSE_MANAGEMENT_TEST_CLAMAV_PORT = $previousPort
    }
    docker rm -f $containerName *> $null
}

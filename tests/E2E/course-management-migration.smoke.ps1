$ErrorActionPreference = 'Stop'
$containerName = 'course-foundation-test-' + [Guid]::NewGuid().ToString('N')
$previousConnection = $env:COURSE_MANAGEMENT_CONNECTION_STRING
$previousTestConnection = $env:COURSE_MANAGEMENT_TEST_CONNECTION_STRING
$previousTestContainer = $env:COURSE_MANAGEMENT_TEST_DB_CONTAINER
$previousTestAttachmentRoot = $env:COURSE_MANAGEMENT_TEST_ATTACHMENT_ROOT
$previousTestPreserveAttachments = $env:COURSE_MANAGEMENT_TEST_PRESERVE_ATTACHMENTS
$smokeDataRoot = Join-Path ([System.IO.Path]::GetTempPath()) ('course-management-smoke-' + [Guid]::NewGuid().ToString('N'))
$attachmentRoot = Join-Path $smokeDataRoot 'attachments'
$attachmentBackupRoot = Join-Path $smokeDataRoot 'backup'
$attachmentRestoreRoot = Join-Path $smokeDataRoot 'restore'
$repoRoot = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$apiAssemblyPath = Join-Path $repoRoot 'services/course-management-service/CourseManagement.API/bin/Debug/net10.0/CourseManagement.API.dll'
try {
    New-Item -ItemType Directory -Path $attachmentRoot -Force | Out-Null
    $env:COURSE_MANAGEMENT_TEST_ATTACHMENT_ROOT = $attachmentRoot
    $env:COURSE_MANAGEMENT_TEST_PRESERVE_ATTACHMENTS = '1'
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
    if (-not (Test-Path $apiAssemblyPath)) { throw 'Course Management API build output is missing.' }
    dotnet $apiAssemblyPath --migrate-only
    if ($LASTEXITCODE -ne 0) { throw 'Initial migration failed.' }
    dotnet $apiAssemblyPath --migrate-only
    if ($LASTEXITCODE -ne 0) { throw 'Repeated migration failed.' }
    $count = docker exec $containerName psql -U course_test -d course_test -tAc "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema='public' AND table_name IN ('UserProfiles', 'Workspaces');"
    if ($LASTEXITCODE -ne 0 -or $count.Trim() -ne '2') { throw 'Expected product tables are missing.' }
    $subscriptionAuditIndex = docker exec $containerName psql -U course_test -d course_test -tAc "SELECT COUNT(*) FROM pg_indexes WHERE schemaname='public' AND tablename='WorkspaceAuditEntries' AND indexname='IX_WorkspaceAuditEntries_SubscriptionAudit';"
    if ($LASTEXITCODE -ne 0 -or $subscriptionAuditIndex.Trim() -ne '1') { throw 'Subscription audit query index is missing.' }
    $env:COURSE_MANAGEMENT_TEST_CONNECTION_STRING = $env:COURSE_MANAGEMENT_CONNECTION_STRING
    $env:COURSE_MANAGEMENT_TEST_DB_CONTAINER = $containerName
    node --test (Join-Path $PSScriptRoot 'course-management-auth.integration.test.mjs')
    if ($LASTEXITCODE -ne 0) { throw 'PostgreSQL API isolation tests failed.' }
    node --test (Join-Path $PSScriptRoot 'course-management-subscription-access.integration.test.mjs')
    if ($LASTEXITCODE -ne 0) { throw 'PostgreSQL subscription access tests failed.' }
    $sourceAttachmentFiles = @(Get-ChildItem -LiteralPath $attachmentRoot -Recurse -File)
    if ($sourceAttachmentFiles.Count -eq 0) { throw 'Course Management E2E did not leave a test attachment to snapshot.' }
    $attachmentKeysQuery = 'SELECT "StorageKey" FROM "CourseAssignmentAttachments" ORDER BY "StorageKey";'
    $attachmentKeysOutput = $attachmentKeysQuery | docker exec -i $containerName psql -U course_test -d course_test -tA
    if ($LASTEXITCODE -ne 0) { throw 'Attachment metadata could not be read for snapshot verification.' }
    $attachmentKeys = @($attachmentKeysOutput | ForEach-Object { $_.Trim() } | Where-Object { $_ })
    if ($attachmentKeys.Count -ne $sourceAttachmentFiles.Count) {
        $sourcePaths = $sourceAttachmentFiles | ForEach-Object { [System.IO.Path]::GetRelativePath($attachmentRoot, $_.FullName) }
        throw "Attachment metadata and source file counts do not match (metadata=$($attachmentKeys.Count), files=$($sourceAttachmentFiles.Count), keys=$($attachmentKeys -join ';'), files=$($sourcePaths -join ';'))."
    }
    foreach ($attachmentKey in $attachmentKeys) {
        $relativeAttachmentPath = $attachmentKey.Replace('/', [string][System.IO.Path]::DirectorySeparatorChar)
        if (-not (Test-Path -LiteralPath (Join-Path $attachmentRoot $relativeAttachmentPath) -PathType Leaf)) {
            throw 'An attachment metadata record has no source file.'
        }
    }
    New-Item -ItemType Directory -Path $attachmentBackupRoot -Force | Out-Null
    Get-ChildItem -LiteralPath $attachmentRoot -Force | ForEach-Object {
        Copy-Item -LiteralPath $_.FullName -Destination $attachmentBackupRoot -Recurse -Force
    }
    New-Item -ItemType Directory -Path $attachmentRestoreRoot -Force | Out-Null
    Get-ChildItem -LiteralPath $attachmentBackupRoot -Force | ForEach-Object {
        Copy-Item -LiteralPath $_.FullName -Destination $attachmentRestoreRoot -Recurse -Force
    }
    function Get-AttachmentManifest([string]$directory) {
        $resolvedRoot = [System.IO.Path]::GetFullPath($directory).TrimEnd([char[]]@(
            [System.IO.Path]::DirectorySeparatorChar, [System.IO.Path]::AltDirectorySeparatorChar))
        return @(Get-ChildItem -LiteralPath $resolvedRoot -Recurse -File | Sort-Object FullName | ForEach-Object {
            $relativePath = [System.IO.Path]::GetRelativePath($resolvedRoot, $_.FullName)
            $hash = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash
            "$relativePath|$hash"
        })
    }
    $sourceAttachmentManifest = Get-AttachmentManifest $attachmentRoot
    $restoredAttachmentManifest = Get-AttachmentManifest $attachmentRestoreRoot
    if (Compare-Object -ReferenceObject $sourceAttachmentManifest -DifferenceObject $restoredAttachmentManifest) {
        throw 'Restored attachment files differ from the source snapshot.'
    }
    foreach ($attachmentKey in $attachmentKeys) {
        $relativeAttachmentPath = $attachmentKey.Replace('/', [string][System.IO.Path]::DirectorySeparatorChar)
        if (-not (Test-Path -LiteralPath (Join-Path $attachmentRestoreRoot $relativeAttachmentPath) -PathType Leaf)) {
            throw 'An attachment metadata record has no restored file.'
        }
    }
    $archivePath = '/tmp/course-management-smoke.dump'
    docker exec $containerName createdb -U course_test course_restore
    if ($LASTEXITCODE -ne 0) { throw 'Isolated restore database could not be created.' }
    docker exec $containerName pg_dump -U course_test -d course_test -Fc -f $archivePath
    if ($LASTEXITCODE -ne 0) { throw 'Course Management smoke backup failed.' }
    docker exec $containerName pg_restore -U course_test -d course_restore --exit-on-error $archivePath
    if ($LASTEXITCODE -ne 0) { throw 'Isolated restore failed.' }
    $stateQuery = @'
SELECT (SELECT COUNT(*) FROM "__EFMigrationsHistory")::text || ',' ||
       (SELECT COUNT(*) FROM "Workspaces")::text || ',' ||
       (SELECT COUNT(*) FROM "Students")::text || ',' ||
       (SELECT COUNT(*) FROM "CourseManagementReminders")::text || ',' ||
       (SELECT COUNT(*) FROM "CourseAssignmentAttachments")::text;
'@
    $sourceStateOutput = $stateQuery | docker exec -i $containerName psql -U course_test -d course_test -tA
    if ($LASTEXITCODE -ne 0) { throw 'Source backup verification query failed.' }
    $sourceState = ($sourceStateOutput -join '').Trim()
    $restoredStateOutput = $stateQuery | docker exec -i $containerName psql -U course_test -d course_restore -tA
    if ($LASTEXITCODE -ne 0) { throw 'Restored database verification query failed.' }
    $restoredState = ($restoredStateOutput -join '').Trim()
    if ($LASTEXITCODE -ne 0 -or $restoredState -ne $sourceState) { throw "Restored database state differs: $sourceState != $restoredState" }
    Write-Output "PASS: Isolated PostgreSQL backup restored migrations/workspaces/students/reminders/attachments ($restoredState)."
    Write-Output "PASS: Course Management attachment snapshot restored $($sourceAttachmentFiles.Count) metadata-linked files with matching SHA-256 manifests."
    Write-Output 'PASS: Initial migration and repeat application on an isolated PostgreSQL database.'
} finally {
    $env:COURSE_MANAGEMENT_CONNECTION_STRING = $previousConnection
    $env:COURSE_MANAGEMENT_TEST_CONNECTION_STRING = $previousTestConnection
    $env:COURSE_MANAGEMENT_TEST_DB_CONTAINER = $previousTestContainer
    $env:COURSE_MANAGEMENT_TEST_ATTACHMENT_ROOT = $previousTestAttachmentRoot
    $env:COURSE_MANAGEMENT_TEST_PRESERVE_ATTACHMENTS = $previousTestPreserveAttachments
    docker rm -f $containerName | Out-Null
    $resolvedTempRoot = [System.IO.Path]::GetFullPath([System.IO.Path]::GetTempPath()).TrimEnd([char[]]@(
        [System.IO.Path]::DirectorySeparatorChar, [System.IO.Path]::AltDirectorySeparatorChar)) + [System.IO.Path]::DirectorySeparatorChar
    $resolvedSmokeDataRoot = [System.IO.Path]::GetFullPath($smokeDataRoot)
    if ($resolvedSmokeDataRoot.StartsWith($resolvedTempRoot, [System.StringComparison]::OrdinalIgnoreCase)) {
        if (Test-Path -LiteralPath $resolvedSmokeDataRoot) {
            Remove-Item -LiteralPath $resolvedSmokeDataRoot -Recurse -Force
        }
    }
}

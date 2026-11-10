param([string]$Goal, [string]$Only)

$ErrorActionPreference = 'Continue'
$gateCommon = $env:GATE_LIBRARY
if (-not $gateCommon -or -not (Test-Path -LiteralPath $gateCommon -PathType Leaf)) {
    Write-Host 'GATE: FAILED (GATE_LIBRARY names no gate library script; README.md, section Gate, says how to plug one in or run the steps without one)'
    exit 1
}
. $gateCommon
$gateOutput = if (Get-Command Get-GateOutputDirectory -ErrorAction SilentlyContinue) {
    Get-GateOutputDirectory (Split-Path -Leaf $PSScriptRoot)
} else {
    (New-Item -ItemType Directory -Force -Path (Join-Path ([IO.Path]::GetTempPath()) "claude\crgolden-gates\$(Split-Path -Leaf $PSScriptRoot)")).FullName
}
Register-GateSteps @('Install dotnet-coverage', 'Restore local tools', 'Begin Sonar analysis', 'Build with dotnet',
    'jb inspectcode', 'Run unit tests with coverage', 'Install SqlPackage', 'Deploy integration test database schema', 'Run Stryker mutation tests',
    'Run Stryker SQL mutation tests', 'Run Stryker relevance mutation tests', 'Run integration tests with coverage', 'Run full-text tests with coverage', 'End Sonar analysis',
    'Fail on open Sonar issues')
Register-StepInputs @{
    'Install dotnet-coverage'                 = @('*')
    'Restore local tools'                     = @('dotnet-tools.json')
    'Begin Sonar analysis'                    = @('*')
    'Build with dotnet'                       = @('*')
    'jb inspectcode'                          = @('*')
    'Run unit tests with coverage'            = @('*', '!Directory.Tests.Integration/*', '!Directory.Tests.FullText/*')
    'Run Stryker mutation tests'              = @('*')
    'Run Stryker SQL mutation tests'          = @('*')
    'Run Stryker relevance mutation tests'    = @('*')
    'Install SqlPackage'                      = @('*')
    'Deploy integration test database schema' = @('*', '!Directory.Tests.Unit/*')
    'Run integration tests with coverage'     = @('*', '!Directory.Tests.Unit/*')
    'Run full-text tests with coverage'       = @('*', '!Directory.Tests.Unit/*')
    'End Sonar analysis'                      = @('*')
    'Fail on open Sonar issues'               = @('*')
}
$repo = $PSScriptRoot
$sarif = (Join-Path $gateOutput 'directory-inspect.sarif')
$unitTrx = Join-Path $repo 'Directory.Tests.Unit\bin\Release\net10.0\TestResults\unit-tests.trx'
$integrationTrx = Join-Path $repo 'Directory.Tests.Integration\bin\Release\net10.0\TestResults\integration-tests.trx'
$fullTextTrx = Join-Path $repo 'Directory.Tests.FullText\bin\Release\net10.0\TestResults\fulltext-tests.trx'
$testCatalog = $env:SqlConnectionStringBuilder__InitialCatalog ?? 'DirectoryTest'
$developmentSql = (Get-Content -Raw (Join-Path $repo 'Directory\appsettings.Development.json') | ConvertFrom-Json).SqlConnectionStringBuilder
$dataSource = $env:SqlConnectionStringBuilder__DataSource ?? $developmentSql.DataSource
$targetConnection = [Data.Common.DbConnectionStringBuilder]::new()
$targetConnection['Data Source'] = $dataSource
$targetConnection['Initial Catalog'] = $testCatalog
if ($env:SqlConnectionStringBuilder__UserID) {
    $targetConnection['User ID'] = $env:SqlConnectionStringBuilder__UserID
    $targetConnection['Password'] = $env:SqlConnectionStringBuilder__Password
    $targetConnection['Encrypt'] = 'True'
    $targetConnection['TrustServerCertificate'] = 'False'
}
else {
    $targetConnection['Integrated Security'] = [string]$developmentSql.IntegratedSecurity
    $targetConnection['TrustServerCertificate'] = [string]$developmentSql.TrustServerCertificate
}
$sonarBranch = Get-SonarBranchName
$beginSonar = "Begin Sonar analysis (branch $sonarBranch)"
$build = 'Build with dotnet (Release, RestoreLockedMode)'
$endSonar = 'End Sonar analysis (quality gate waited)'
$sonarIssues = 'Fail on open Sonar issues'
$env:TZ = 'UTC'
if ($env:TZ -ne 'UTC') { Write-Host 'GATE: FAILED (TZ pin)'; exit 1 }
Set-Location $repo
$probeProjects = @('Directory.Tests.Unit', 'Directory.Tests.Integration', 'Directory.Tests.FullText')
if ($Only) {
    $probeFile, $probeTest = $Only -split '::', 2
    $probeFile = "$probeFile".Trim().Replace('\', '/')
    $probeTest = "$probeTest".Trim()
    $probeProject = $probeFile.Split('/')[0]
    if (-not $probeTest -or $probeTest.Contains(',') -or $probeProject -notin $probeProjects -or
            -not (Test-Path -LiteralPath (Join-Path $repo $probeFile) -PathType Leaf)) {
        Write-Host "PROBE: FAILED (a probe runs exactly one test, named by the file it lives in: -Only '<a test file under $($probeProjects -join ' | ')>::<test method>')"
        exit 1
    }
    $probeClass = [IO.Path]::GetFileNameWithoutExtension($probeFile)
    $probeResults = Join-Path $gateOutput 'probe-results'
    $probeTrx = Join-Path $probeResults 'probe.trx'
    if (Test-Path -LiteralPath $probeTrx) { Remove-Item -LiteralPath $probeTrx -Force }
    $env:SqlConnectionStringBuilder__InitialCatalog = $testCatalog
    Write-Host "PROBE: running one test, '$probeTest' of $probeClass in $probeProject; no gate record or verdict is written"
    $global:LASTEXITCODE = $null
    dotnet test --project $probeProject --no-build --configuration Release -- --filter-query "/*/*/$probeClass/$probeTest" --report-xunit-trx --report-xunit-trx-filename probe.trx --results-directory=$probeResults
    $probeExit = $global:LASTEXITCODE
    if (-not (Test-Path -LiteralPath $probeTrx)) { Write-Host "PROBE: FAILED (no TRX, exit $probeExit)"; exit 1 }
    [xml]$probeDoc = Get-Content -LiteralPath $probeTrx -Raw
    $counters = $probeDoc.TestRun.ResultSummary.Counters
    if ($probeExit -ne 0 -or [int]$counters.executed -ne 1 -or [int]$counters.failed -ne 0) {
        Write-Host "PROBE: FAILED (a probe runs one test, and '$Only' executed $($counters.executed), failed $($counters.failed), exit $probeExit)"
        exit 1
    }
    Write-Host "PROBE: PASSED for the one test named, against the last build's binaries; no verdict was saved"
    exit 0
}
exit (Invoke-Gate {
Initialize-GateState 'Directory' $repo
Resolve-OwedSteps
Invoke-CatalogSteps

if (-not (Test-StepCarried 'Install dotnet-coverage')) {
    if (Get-Command dotnet-coverage -ErrorAction SilentlyContinue) { Write-Row 'Install dotnet-coverage' 'PASS' 'present on PATH' }
    else { Stop-Gate 'Install dotnet-coverage' 'not on PATH' }
}
$global:LASTEXITCODE = $null
dotnet tool restore
Assert-StepExit 'Restore local tools (dotnet tool restore)'

$sonarCarried = Test-StepCarried $sonarIssues
if ($sonarCarried) {
    $null = Test-StepCarried $beginSonar
    $null = Test-StepCarried $build
    $null = Test-StepCarried $endSonar
}
else {
    $sonarStartedAt = [DateTimeOffset]::UtcNow
    $env:JAVA_HOME = Get-SonarScannerJavaHome $beginSonar
    $global:LASTEXITCODE = $null
    dotnet-sonarscanner begin /k:"crgolden_Directory" /o:"crgolden" /d:sonar.host.url="https://sonarcloud.io" /d:sonar.cs.opencover.reportsPaths="coverage.opencover.xml" /d:sonar.cs.vscoveragexml.reportsPaths="coverage-integration.xml,coverage-fulltext.xml" /d:sonar.exclusions="**/bin/**,**/obj/**" /d:sonar.coverage.exclusions="**/Program.cs,**/gate.ps1" /d:sonar.qualitygate.wait=true /d:sonar.scanner.skipJreProvisioning=true /d:sonar.branch.name="$sonarBranch"
    Assert-StepExit $beginSonar

    $global:LASTEXITCODE = $null
    dotnet build --no-incremental --configuration Release /p:RestoreLockedMode=true -warnaserror
    Assert-StepExit $build
}

if (-not (Test-StepCarried 'jb inspectcode')) {
    if (Test-Path $sarif) { Remove-Item $sarif -Force }
    dotnet jb inspectcode "$repo\Directory.slnx" --no-build -e=WARNING --caches-home="$(New-InspectCodeCaches $gateOutput)" --output="$sarif"
    Test-Sarif $sarif
}

if (-not (Test-StepCarried 'Run unit tests with coverage (Category=Unit)')) {
    if (Test-Path $unitTrx) { Remove-Item $unitTrx -Force }
    $global:LASTEXITCODE = $null
    dotnet coverlet Directory.Tests.Unit\bin\Release\net10.0 `
        --target "dotnet" `
        --targetargs "test --project Directory.Tests.Unit --no-build --configuration Release -- --filter-trait Category=Unit --stop-on-fail on --report-xunit-trx --report-xunit-trx-filename unit-tests.trx --results-directory=Directory.Tests.Unit/bin/Release/net10.0/TestResults" `
        --format opencover --output "coverage.opencover.xml" `
        --skipautoprops --exclude-by-attribute GeneratedCodeAttribute --exclude-by-file "**/obj/**" `
        --exclude-by-file "**/Program.cs" --does-not-return-attribute DoesNotReturnAttribute --include "[Directory]*"
    Test-Trx 'Run unit tests with coverage (Category=Unit)' $unitTrx $global:LASTEXITCODE -floor 1
}

if (-not (Test-StepCarried 'Install SqlPackage')) {
    if (Get-Command sqlpackage -ErrorAction SilentlyContinue) { Write-Row 'Install SqlPackage' 'PASS' 'present on PATH' }
    else { Stop-Gate 'Install SqlPackage' 'not on PATH' }
}
$schemaStep = "Deploy integration test database schema ($testCatalog on $dataSource)"
if (-not (Test-StepCarried $schemaStep)) {
    $global:LASTEXITCODE = $null
    sqlpackage /Action:Publish /SourceFile:Directory.Data/bin/Release/Directory.Data.dacpac /TargetConnectionString:"$($targetConnection.ConnectionString)"
    Assert-StepExit $schemaStep
}
$env:SqlConnectionStringBuilder__InitialCatalog = $testCatalog

$stryker = 'Run Stryker mutation tests (thresholds 100)'
if (-not (Test-StepCarried $stryker)) {
    $strykerOutputDir = Join-Path $repo 'Directory\StrykerOutput\gate'
    if (Test-Path -LiteralPath $strykerOutputDir) { Remove-Item -LiteralPath $strykerOutputDir -Recurse -Force }
    Push-Location (Join-Path $repo 'Directory')
    $global:LASTEXITCODE = $null
    dotnet stryker --config-file ../stryker-config.json --output $strykerOutputDir
    $strykerExit = $global:LASTEXITCODE
    Pop-Location
    if ($null -eq $strykerExit) { Stop-Gate $stryker 'command never ran' }
    if ($strykerExit -ne 0) { Stop-Gate $stryker "exit $strykerExit" }
    Write-Row $stryker 'PASS' 'every mutant killed or excluded with its reason'
}

$sqlStryker = 'Run Stryker SQL mutation tests (thresholds 100)'
if (-not (Test-StepCarried $sqlStryker)) {
    $sqlOutputDir = Join-Path $repo 'Directory\StrykerOutput\gate-sql'
    if (Test-Path -LiteralPath $sqlOutputDir) { Remove-Item -LiteralPath $sqlOutputDir -Recurse -Force }
    Push-Location (Join-Path $repo 'Directory')
    $global:LASTEXITCODE = $null
    dotnet stryker --config-file ../stryker-sql-config.json --output $sqlOutputDir
    $sqlExit = $global:LASTEXITCODE
    Pop-Location
    if ($null -eq $sqlExit) { Stop-Gate $sqlStryker 'command never ran' }
    if ($sqlExit -ne 0) { Stop-Gate $sqlStryker "exit $sqlExit" }
    Write-Row $sqlStryker 'PASS' 'every SQL-statement mutant killed'
}

$relevanceStryker = 'Run Stryker relevance mutation tests (thresholds 100)'
if (-not (Test-StepCarried $relevanceStryker)) {
    $relevanceOutputDir = Join-Path $repo 'Directory\StrykerOutput\gate-relevance'
    if (Test-Path -LiteralPath $relevanceOutputDir) { Remove-Item -LiteralPath $relevanceOutputDir -Recurse -Force }
    Push-Location (Join-Path $repo 'Directory')
    $global:LASTEXITCODE = $null
    dotnet stryker --config-file ../stryker-relevance-config.json --output $relevanceOutputDir
    $relevanceExit = $global:LASTEXITCODE
    Pop-Location
    if ($null -eq $relevanceExit) { Stop-Gate $relevanceStryker 'command never ran' }
    if ($relevanceExit -ne 0) { Stop-Gate $relevanceStryker "exit $relevanceExit" }
    Write-Row $relevanceStryker 'PASS' 'every relevance-ordering mutant killed'
}

if (-not (Test-StepCarried 'Run integration tests with coverage (Category=Integration)')) {
    if (Test-Path $integrationTrx) { Remove-Item $integrationTrx -Force }
    $global:LASTEXITCODE = $null
    dotnet-coverage collect `
        "dotnet test --project Directory.Tests.Integration --no-build --configuration Release -- --filter-trait Category=Integration --stop-on-fail on --report-xunit-trx --report-xunit-trx-filename integration-tests.trx --results-directory=Directory.Tests.Integration/bin/Release/net10.0/TestResults" `
        -f xml -o "coverage-integration.xml" -s "coverage.settings.xml"
    Test-Trx 'Run integration tests with coverage (Category=Integration)' $integrationTrx $global:LASTEXITCODE -floor 1
}

if (-not (Test-StepCarried 'Run full-text tests with coverage (Category=Integration)')) {
    if (Test-Path $fullTextTrx) { Remove-Item $fullTextTrx -Force }
    $global:LASTEXITCODE = $null
    dotnet-coverage collect `
        "dotnet test --project Directory.Tests.FullText --no-build --configuration Release -- --filter-trait Category=Integration --stop-on-fail on --report-xunit-trx --report-xunit-trx-filename fulltext-tests.trx --results-directory=Directory.Tests.FullText/bin/Release/net10.0/TestResults" `
        -f xml -o "coverage-fulltext.xml" -s "coverage.settings.xml"
    Test-Trx 'Run full-text tests with coverage (Category=Integration)' $fullTextTrx $global:LASTEXITCODE -floor 1
}

if (-not $sonarCarried) {
    $global:LASTEXITCODE = $null
    dotnet-sonarscanner end
    Assert-StepExit $endSonar
    Test-SonarIssues $sonarIssues 'crgolden_Directory' $sonarBranch $sonarStartedAt
}

Write-Row 'Upload test results / dotnet publish / uploads / deploy' 'NOT RUN' 'delivery steps, not checks'
Complete-Gate
})

param([string]$Goal)

$ErrorActionPreference = 'Continue'
$gateCommon = $env:GATE_LIBRARY
if (-not $gateCommon -or -not (Test-Path -LiteralPath $gateCommon -PathType Leaf)) {
    Write-Host 'GATE: FAILED (GATE_LIBRARY names no gate library script; README.md, section Gate, says how to plug one in or run the steps without one)'
    exit 1
}
. $gateCommon
$gateOutput = Join-Path ([IO.Path]::GetTempPath()) "crgolden-gates\$(Split-Path -Leaf $PSScriptRoot)"
New-Item -ItemType Directory -Force -Path $gateOutput | Out-Null
Register-GateSteps @('Install dotnet-coverage', 'Restore local tools', 'Begin Sonar analysis', 'Build with dotnet',
    'jb inspectcode', 'Run unit tests with coverage', 'Install SqlPackage', 'Deploy integration test database schema', 'Run Stryker mutation tests',
    'Run Stryker relevance mutation tests', 'Run integration tests with coverage', 'Run full-text tests with coverage', 'End Sonar analysis',
    'Fail on open Sonar issues')
Register-StepInputs @{
    'Install dotnet-coverage'                 = @('*')
    'Restore local tools'                     = @('dotnet-tools.json')
    'Begin Sonar analysis'                    = @('*')
    'Build with dotnet'                       = @('*')
    'jb inspectcode'                          = @('*')
    'Run unit tests with coverage'            = @('*')
    'Run Stryker mutation tests'              = @('*')
    'Run Stryker relevance mutation tests'    = @('*')
    'Install SqlPackage'                      = @('*')
    'Deploy integration test database schema' = @('*')
    'Run integration tests with coverage'     = @('*')
    'Run full-text tests with coverage'       = @('*')
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
Initialize-GateState 'Directory' $repo
Resolve-OwedSteps
Invoke-CatalogSteps

if (-not (Test-StepCarried 'Install dotnet-coverage')) {
    if (Get-Command dotnet-coverage -ErrorAction SilentlyContinue) { Write-Row 'Install dotnet-coverage' 'PASS' 'present on PATH' }
    else { Stop-Gate 'Install dotnet-coverage' 'not on PATH' }
}
$global:LASTEXITCODE = $null
dotnet tool restore
$null = Test-Exit 'Restore local tools (dotnet tool restore)'

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
    $null = Test-Exit $beginSonar

    $global:LASTEXITCODE = $null
    dotnet build --no-incremental --configuration Release /p:RestoreLockedMode=true -warnaserror
    $null = Test-Exit $build
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
    $null = Test-Exit $schemaStep
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
    $null = Test-Exit $endSonar
    Test-SonarIssues $sonarIssues 'crgolden_Directory' $sonarBranch $sonarStartedAt
}

Write-Row 'Upload test results / dotnet publish / uploads / deploy' 'NOT RUN' 'delivery steps, not checks'
Complete-Gate

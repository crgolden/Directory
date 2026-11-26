@{
    Schema      = 1
    Repo        = 'Directory'
    Workflow    = '.github/workflows/main_crgolden-directory.yml'
    Sonar       = @{ ProjectKey = 'crgolden_Directory' }
    Probe       = @{ Kind = 'dotnet'; Projects = @('Directory.Tests.Unit', 'Directory.Tests.Integration', 'Directory.Tests.FullText') }
    Environment = @{ SqlConnectionStringBuilder__InitialCatalog = 'DirectoryTest' }
    Steps       = @(
        @{ Name = 'Regenerate lock files and push'; Delivery = $true }
        @{ Name = 'Install SonarQube Cloud scanner'; Tool = 'dotnet-sonarscanner'; Inputs = @('*') }
        @{ Name = 'Install dotnet-coverage'; Tool = 'dotnet-coverage'; Inputs = @('*') }
        @{ Name = 'Restore local tools'; Run = @('dotnet', 'tool', 'restore'); Inputs = @('dotnet-tools.json') }
        @{
            Name   = 'Begin Sonar analysis'
            Sonar  = 'begin'
            Needs  = @('Install SonarQube Cloud scanner', 'Restore local tools')
            Inputs = @('*')
            Run    = @(
                'dotnet-sonarscanner', 'begin', '/k:{sonarProject}', '/o:crgolden', '/d:sonar.host.url=https://sonarcloud.io',
                '/d:sonar.cs.opencover.reportsPaths=coverage.opencover.xml',
                '/d:sonar.cs.vscoveragexml.reportsPaths=coverage-integration.xml,coverage-fulltext.xml',
                '/d:sonar.exclusions=**/bin/**,**/obj/**', '/d:sonar.coverage.exclusions=**/Program.cs,**/gate.ps1,**/gate.steps.psd1,scripts/gate/**',
                '/d:sonar.qualitygate.wait=true', '/d:sonar.scanner.skipJreProvisioning=true',
                '/d:sonar.scm.use.blame.algorithm=GIT_NATIVE_BLAME', '/d:sonar.branch.name={sonarBranch}'
            )
        }
        @{
            Name   = 'Build with dotnet'
            Sonar  = 'build'
            Needs  = @('Restore local tools')
            Inputs = @('*')
            Run    = @('dotnet', 'build', '--no-incremental', '--configuration', 'Release', '/p:RestoreLockedMode=true', '-warnaserror')
        }
        @{
            Name   = 'ReSharper inspection (jb inspectcode)'
            Needs  = @('Build with dotnet')
            Inputs = @('*')
            Remove = @('{output}\directory-inspect.sarif')
            Run    = @('dotnet', 'jb', 'inspectcode', 'Directory.slnx', '--no-build', '-e=WARNING', '--caches-home={inspectCache}', '--output={output}\directory-inspect.sarif')
            Check  = @{ Sarif = '{output}\directory-inspect.sarif' }
        }
        @{
            Name   = 'Run unit tests with coverage'
            Needs  = @('Build with dotnet')
            Inputs = @('*', '!Directory.Tests.Integration/*', '!Directory.Tests.FullText/*')
            Remove = @('Directory.Tests.Unit\bin\Release\net11.0\TestResults\unit-tests.trx')
            Run    = @(
                'dotnet', 'coverlet', 'Directory.Tests.Unit/bin/Release/net11.0', '--target', 'dotnet',
                '--targetargs', 'test --project Directory.Tests.Unit --no-build --configuration Release -- --filter-trait Category=Unit --report-xunit-trx --report-xunit-trx-filename unit-tests.trx --results-directory=Directory.Tests.Unit/bin/Release/net11.0/TestResults',
                '--format', 'opencover', '--output', 'coverage.opencover.xml', '--skipautoprops',
                '--exclude-by-attribute', 'GeneratedCodeAttribute', '--exclude-by-file', '**/obj/**', '--exclude-by-file', '**/Program.cs',
                '--does-not-return-attribute', 'DoesNotReturnAttribute', '--include', '[Directory]*'
            )
            Check  = @{ Trx = 'Directory.Tests.Unit\bin\Release\net11.0\TestResults\unit-tests.trx'; Floor = 1 }
        }
        @{ Name = 'Install SqlPackage'; Tool = 'sqlpackage'; Inputs = @('*') }
        @{
            Name   = 'Build integration test database schema'
            Needs  = @('Build with dotnet')
            Inputs = @('*', '!Directory.Tests.Unit/*')
            Run    = @('dotnet', 'build', 'Directory.Data/Directory.Data.sqlproj', '--configuration', 'Release')
        }
        @{
            Name   = 'Deploy integration test database schema'
            Needs  = @('Build with dotnet', 'Install SqlPackage')
            Inputs = @('*', '!Directory.Tests.Unit/*')
            Run    = @('pwsh', '-NoProfile', '-File', 'scripts/gate/Publish-TestSchema.ps1')
        }
        @{
            Name             = 'Run Stryker mutation tests'
            Needs            = @('Build with dotnet')
            Inputs           = @('*')
            WorkingDirectory = 'Directory'
            Remove           = @('Directory\StrykerOutput\gate')
            Run              = @('dotnet', 'stryker', '--config-file', '../stryker-config.json', '--output', '{repo}\Directory\StrykerOutput\gate')
        }
        @{
            Name             = 'Run Stryker SQL mutation tests'
            Needs            = @('Build with dotnet', 'Deploy integration test database schema')
            Inputs           = @('*')
            WorkingDirectory = 'Directory'
            Remove           = @('Directory\StrykerOutput\gate-sql')
            Run              = @('dotnet', 'stryker', '--config-file', '../stryker-sql-config.json', '--output', '{repo}\Directory\StrykerOutput\gate-sql')
        }
        @{
            Name             = 'Run Stryker relevance mutation tests'
            Needs            = @('Build with dotnet', 'Deploy integration test database schema')
            Inputs           = @('*')
            WorkingDirectory = 'Directory'
            Remove           = @('Directory\StrykerOutput\gate-relevance')
            Run              = @('dotnet', 'stryker', '--config-file', '../stryker-relevance-config.json', '--output', '{repo}\Directory\StrykerOutput\gate-relevance')
        }
        @{
            Name   = 'Run integration tests with coverage'
            Needs  = @('Build with dotnet', 'Deploy integration test database schema', 'Install dotnet-coverage')
            Inputs = @('*', '!Directory.Tests.Unit/*')
            Remove = @('Directory.Tests.Integration\bin\Release\net11.0\TestResults\integration-tests.trx')
            Run    = @(
                'dotnet-coverage', 'collect',
                'dotnet test --project Directory.Tests.Integration --no-build --configuration Release -- --filter-trait Category=Integration --report-xunit-trx --report-xunit-trx-filename integration-tests.trx --results-directory=Directory.Tests.Integration/bin/Release/net11.0/TestResults',
                '-f', 'xml', '-o', 'coverage-integration.xml', '-s', 'coverage.settings.xml'
            )
            Check  = @{ Trx = 'Directory.Tests.Integration\bin\Release\net11.0\TestResults\integration-tests.trx'; Floor = 1 }
        }
        @{
            Name   = 'Run full-text tests with coverage'
            Needs  = @('Build with dotnet', 'Deploy integration test database schema', 'Install dotnet-coverage')
            Inputs = @('*', '!Directory.Tests.Unit/*')
            Remove = @('Directory.Tests.FullText\bin\Release\net11.0\TestResults\fulltext-tests.trx')
            Run    = @(
                'dotnet-coverage', 'collect',
                'dotnet test --project Directory.Tests.FullText --no-build --configuration Release -- --filter-trait Category=Integration --report-xunit-trx --report-xunit-trx-filename fulltext-tests.trx --results-directory=Directory.Tests.FullText/bin/Release/net11.0/TestResults',
                '-f', 'xml', '-o', 'coverage-fulltext.xml', '-s', 'coverage.settings.xml'
            )
            Check  = @{ Trx = 'Directory.Tests.FullText\bin\Release\net11.0\TestResults\fulltext-tests.trx'; Floor = 1 }
        }
        @{ Name = 'dotnet publish'; Delivery = $true }
        @{
            Name   = 'End Sonar analysis'
            Sonar  = 'end'
            Needs  = @('Begin Sonar analysis', 'Build with dotnet')
            Inputs = @('*')
            Run    = @('dotnet-sonarscanner', 'end')
        }
        @{ Name = 'Fail on open Sonar issues'; Sonar = 'issues'; Needs = @('End Sonar analysis'); Inputs = @('*') }
        @{ Name = 'Deploy database'; Delivery = $true }
        @{ Name = 'Run the app on .NET 11'; Delivery = $true }
    )
}

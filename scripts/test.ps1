$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$compiler = Join-Path $env:windir 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$testDir = Join-Path $root 'artifacts\tests'
[IO.Directory]::CreateDirectory($testDir) | Out-Null
$exe = Join-Path $testDir 'ControllerSelectionTests.exe'
$mapping = Join-Path $root 'src\XInputMapping.cs'
$tests = Join-Path $root 'tests\ControllerSelectionTests.cs'

& $compiler /nologo /warnaserror /platform:x64 /target:exe "/out:$exe" $mapping $tests
if ($LASTEXITCODE -ne 0) { throw "C# test compilation failed with exit code $LASTEXITCODE." }
& $exe
if ($LASTEXITCODE -ne 0) { throw "Controller selection tests failed with exit code $LASTEXITCODE." }

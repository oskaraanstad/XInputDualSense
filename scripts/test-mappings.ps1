$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$compiler = Join-Path $env:windir 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$testDir = Join-Path $root 'artifacts\tests'
[IO.Directory]::CreateDirectory($testDir) | Out-Null
$exe = Join-Path $testDir 'ButtonMappingTests.exe'
$mapping = Join-Path $root 'src\XInputMapping.cs'
$profile = Join-Path $root 'src\ButtonProfile.cs'
$tests = Join-Path $root 'tests\ButtonMappingTests.cs'

& $compiler /nologo /warnaserror /platform:x64 /target:exe "/out:$exe" $mapping $profile $tests
if ($LASTEXITCODE -ne 0) { throw "C# test compilation failed with exit code $LASTEXITCODE." }
& $exe
if ($LASTEXITCODE -ne 0) { throw "Button mapping tests failed with exit code $LASTEXITCODE." }

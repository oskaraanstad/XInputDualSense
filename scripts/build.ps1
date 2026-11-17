$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$source = Join-Path $root 'src'
$vendor = Join-Path $root 'vendor\libviiper'
$artifacts = Join-Path $root 'artifacts'
$bin = Join-Path $artifacts 'bin'
$compiler = Join-Path $env:windir 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$dll = Join-Path $vendor 'libVIIPER.dll'
$dllHash = '29D7D8CCF76F0216D14CDD253827A3F3E5F751F834DBA8A2EACDA8186FE89457'
if ((Get-FileHash -LiteralPath $dll -Algorithm SHA256).Hash -ne $dllHash) {
    throw 'The bundled libVIIPER DLL does not match the pinned SHA-256.'
}

[IO.Directory]::CreateDirectory($bin) | Out-Null
$exe = Join-Path $bin 'XInputDualSense.exe'
$icon = Join-Path $root 'assets\app.ico'
$manifest = Join-Path $source 'app.manifest'
$sources = (Get-ChildItem -LiteralPath $source -Filter '*.cs' -File).FullName
& $compiler /nologo /warnaserror /optimize+ /codepage:65001 /target:winexe /platform:x64 /r:System.Windows.Forms.dll /r:System.Drawing.dll "/out:$exe" "/win32icon:$icon" "/win32manifest:$manifest" $sources
if ($LASTEXITCODE -ne 0) { throw "C# compiler exited with code $LASTEXITCODE." }
Copy-Item -LiteralPath $dll -Destination (Join-Path $bin 'libVIIPER.dll') -Force -ErrorAction Stop

Add-Type -AssemblyName System.IO.Compression, System.IO.Compression.FileSystem
function Write-Zip([string]$destination, [string]$base, [string[]]$paths) {
    if (Test-Path -LiteralPath $destination) { Remove-Item -LiteralPath $destination }
    $archive = [IO.Compression.ZipFile]::Open($destination, [IO.Compression.ZipArchiveMode]::Create)
    try {
        foreach ($path in $paths) {
            $item = Get-Item -LiteralPath (Join-Path $base $path) -Force
            $entries = @($item)
            if ($item.PSIsContainer) {
                $entries = Get-ChildItem -LiteralPath $item.FullName -File -Recurse -Force
            }
            foreach ($file in $entries) {
                $name = $file.FullName.Substring($base.Length + 1).Replace('\','/')
                [IO.Compression.ZipFileExtensions]::CreateEntryFromFile(
                    $archive, $file.FullName, $name, [IO.Compression.CompressionLevel]::Optimal) | Out-Null
            }
        }
    } finally { $archive.Dispose() }
}

$sourceZip = Join-Path $bin 'XInputDualSense-source.zip'
Write-Zip $sourceZip $root @('src','assets','scripts','tests','vendor','.github','.gitattributes','.gitignore','README.md','LICENSE','THIRD_PARTY_NOTICES.md')
foreach ($name in 'README.md','LICENSE','THIRD_PARTY_NOTICES.md') {
    Copy-Item -LiteralPath (Join-Path $root $name) -Destination (Join-Path $bin $name) -Force
}
Copy-Item -LiteralPath (Join-Path $vendor 'licenses.txt') -Destination (Join-Path $bin 'libVIIPER-licenses.txt') -Force
[IO.Directory]::CreateDirectory((Join-Path $bin 'assets')) | Out-Null
Copy-Item -LiteralPath (Join-Path $root 'assets\icon.png') -Destination (Join-Path $bin 'assets\icon.png') -Force
$releaseZip = Join-Path $artifacts 'XInputDualSense-win-x64.zip'
Write-Zip $releaseZip $bin @('XInputDualSense.exe','libVIIPER.dll','XInputDualSense-source.zip','README.md','LICENSE','THIRD_PARTY_NOTICES.md','libVIIPER-licenses.txt','assets')
Write-Output $releaseZip

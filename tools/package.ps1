# Builds the mod in Release and creates dist/StellarDrive-demo-TF-advanced.zip in the layout
# StellarModManager expects: mod.json at the root, the DLL under Mods/.
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$project = Join-Path $root 'src\StellarDriveDemoTFAdvanced\StellarDriveDemoTFAdvanced.csproj'
$dist = Join-Path $root 'dist'
$stage = Join-Path $dist 'stage'

dotnet build $project -c Release -p:DeployToGame=false
if ($LASTEXITCODE -ne 0) { throw 'Build failed' }

Remove-Item $dist -Recurse -Force -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Force (Join-Path $stage 'Mods') | Out-Null
Copy-Item (Join-Path $root 'Mods\mod.json') $stage
Copy-Item (Join-Path $root 'src\StellarDriveDemoTFAdvanced\bin\Release\StellarDriveDemoTFAdvanced.dll') (Join-Path $stage 'Mods')

$version = (Get-Content (Join-Path $root 'Mods\mod.json') -Raw | ConvertFrom-Json).version
$zip = Join-Path $dist 'StellarDrive-demo-TF-advanced.zip'
# Entry names with '/' (Compress-Archive on Windows PowerShell writes '\', which some unzippers misread)
Add-Type -AssemblyName System.IO.Compression, System.IO.Compression.FileSystem
$archive = [System.IO.Compression.ZipFile]::Open($zip, 'Create')
try {
    foreach ($file in Get-ChildItem $stage -Recurse -File) {
        $entry = $file.FullName.Substring($stage.Length + 1).Replace('\', '/')
        [System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile($archive, $file.FullName, $entry) | Out-Null
    }
} finally {
    $archive.Dispose()
}
Remove-Item $stage -Recurse -Force
Write-Host "Packaged $zip (upload it to a GitHub release tagged v$version)"

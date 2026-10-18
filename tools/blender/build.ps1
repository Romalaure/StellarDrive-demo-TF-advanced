# Builds the mod's Blender models: runs every model script of this folder (except common.py)
# headless and writes src/StellarDriveDemoTFAdvanced/Models/<name>.obj, embedded in the DLL.
$ErrorActionPreference = 'Continue'
$blender = 'C:\Program Files\Blender Foundation\Blender 3.3\blender.exe'
$here = $PSScriptRoot
$out = Join-Path (Split-Path -Parent (Split-Path -Parent $here)) 'src\StellarDriveDemoTFAdvanced\Models'
New-Item -ItemType Directory -Force $out | Out-Null
foreach ($script in Get-ChildItem $here -Filter *.py | Where-Object { $_.Name -ne 'common.py' }) {
    $target = Join-Path $out ($script.BaseName + '.obj')
    & $blender --background --factory-startup --python $script.FullName -- $target 2>&1 | Select-String 'exported|Error|Traceback'
    Remove-Item ([System.IO.Path]::ChangeExtension($target, '.mtl')) -ErrorAction SilentlyContinue
}

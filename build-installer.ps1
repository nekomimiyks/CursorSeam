param(
    [string]$Compiler = $env:ISCC,
    [string]$OutputDirectory = (Join-Path $PSScriptRoot 'dist')
)

$ErrorActionPreference = 'Stop'
if (-not $Compiler) { $Compiler = 'ISCC.exe' }
$Compiler = (Get-Command $Compiler -CommandType Application -ErrorAction Stop).Source
$output = (New-Item -ItemType Directory -Path $OutputDirectory -Force).FullName
$executable = Join-Path $output 'CursorSeam.exe'
& (Join-Path $PSScriptRoot 'build.ps1') -OutputPath $executable | Out-Host
if ($LASTEXITCODE -ne 0) { throw 'CursorSeam could not be built.' }
$version = (Get-Item -LiteralPath $executable).VersionInfo.ProductVersion
& $Compiler "/DAppVersion=$version" "/DApplication=$executable" "/DOutputDirectory=$output" (Join-Path $PSScriptRoot 'installer\CursorSeam.iss') | Out-Host
if ($LASTEXITCODE -ne 0) { throw 'CursorSeam Setup could not be built.' }
Write-Output (Join-Path $output "CursorSeam-$version-Setup.exe")

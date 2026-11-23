param([switch]$Test, [string]$PreviewPath, [string]$OutputPath, [switch]$Store)

$ErrorActionPreference = 'Stop'
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path -LiteralPath $compiler)) {
    $compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe'
}
if (-not (Test-Path -LiteralPath $compiler)) {
    throw 'The .NET Framework 4.x C# compiler was not found.'
}
$sources = @(Get-ChildItem -LiteralPath $PSScriptRoot -Filter '*.cs' -File | ForEach-Object FullName)
$iconPath = Join-Path $PSScriptRoot 'CursorSeam.ico'
$executable = if ($OutputPath) { $OutputPath } else { Join-Path $PSScriptRoot 'CursorSeam.exe' }
$tempPrefix = Join-Path ([System.IO.Path]::GetTempPath()) ('CursorSeam-' + [Guid]::NewGuid())
$resourcePath = $tempPrefix + '.resources'
$verification = $tempPrefix + '.exe'
$temporaryFiles = @($verification, $resourcePath)
try {
    [xml]$catalog = Get-Content -LiteralPath (Join-Path $PSScriptRoot 'UiStrings.resx') -Raw -Encoding UTF8
    $japanese = @{}
    $resourceWriter = [System.Resources.ResourceWriter]::new($resourcePath)
    try {
        foreach ($entry in $catalog.root.data) {
            $key = [string]$entry.name
            $japanese.Add($key, [string]$entry.value)
            $resourceWriter.AddResource($key, $japanese[$key])
        }
    } finally { $resourceWriter.Dispose() }

    $options = @('/nologo', '/warnaserror+', '/codepage:65001', "/resource:$resourcePath,CursorSeam.UiStrings.resources", "/resource:$iconPath,CursorSeam.AppIcon.ico", "/win32icon:$iconPath", '/r:System.Drawing.dll', '/r:System.Windows.Forms.dll', '/r:System.Xml.Linq.dll')
    if ($Store) {
        $metadata = Join-Path $env:WINDIR 'System32\WinMetadata'
        $options += @('/define:STORE_BUILD', '/platform:x64', '/r:System.Runtime.dll', '/r:System.Runtime.WindowsRuntime.dll',
            "/r:$metadata\Windows.ApplicationModel.winmd", "/r:$metadata\Windows.Foundation.winmd")
    } else {
        $options += @('/r:System.Net.Http.dll', '/r:System.Runtime.Serialization.dll')
    }
    $catalogLines = @(Get-Content -LiteralPath (Join-Path $PSScriptRoot 'UiTranslations.txt') -Encoding UTF8)
    $headers = $catalogLines[0].Split('|')
    $translations = @{}
    foreach ($line in ($catalogLines | Select-Object -Skip 1)) {
        if (-not $line.Trim()) { continue }
        $columns = $line.Split('|')
        if ($columns.Count -ne $headers.Count -or -not $japanese.ContainsKey($columns[0])) { throw "Invalid translation row: $($columns[0])" }
        $translations.Add($columns[0], $columns)
    }
    foreach ($key in $japanese.Keys) {
        if ($key -notin @('DisplayCaption', 'RulerCentimeters') -and -not $translations.ContainsKey($key)) {
            throw "Untranslated UI string: $key"
        }
    }
    for ($column = 1; $column -lt $headers.Count; $column++) {
        $language = $headers[$column]
        $localizedResource = "$tempPrefix.$language.resources"
        $temporaryFiles += $localizedResource
        $resourceWriter = [System.Resources.ResourceWriter]::new($localizedResource)
        try {
            foreach ($key in $translations.Keys) {
                $value = $translations[$key][$column]
                $expectedFields = @([regex]::Matches($japanese[$key], '\{\d+\}').Value | Sort-Object) -join ' '
                $fields = @([regex]::Matches($value, '\{\d+\}').Value | Sort-Object) -join ' '
                if (-not $value.Trim() -or $fields -ne $expectedFields) { throw "Invalid translation: $language / $key" }
                $resourceWriter.AddResource($key, $value)
            }
        } finally { $resourceWriter.Dispose() }
        $options += "/resource:$localizedResource,CursorSeam.UiStrings.$language.resources"
    }
    & $compiler @options /target:winexe /optimize+ "/out:$executable" $sources
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
    Write-Output $executable

    if ($Test -or $PreviewPath) {
        $verificationSource = Join-Path $PSScriptRoot 'tests\Verification.cs'
        & $compiler @options /main:Verification "/out:$verification" $sources $verificationSource
        if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
        if ($PreviewPath) { & $verification $PreviewPath $executable } else { & $verification }
        if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
    }
} finally {
    foreach ($temporary in $temporaryFiles) {
        if (Test-Path -LiteralPath $temporary) { Remove-Item -LiteralPath $temporary }
    }
}

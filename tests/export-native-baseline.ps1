[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)] [string] $ModelPath,
    [Parameter(Mandatory = $true)] [string] $DiagramCode,
    [Parameter(Mandatory = $true)] [string] $OutputPath
)

$ErrorActionPreference = 'Stop'
$modelFile = [IO.Path]::GetFullPath($ModelPath)
$imageFile = [IO.Path]::GetFullPath($OutputPath)
if (-not (Test-Path -LiteralPath $modelFile -PathType Leaf)) {
    throw "PDM file does not exist: $modelFile"
}
if ([IO.Path]::GetExtension($imageFile).ToLowerInvariant() -notin @('.png', '.svg')) {
    throw 'The native baseline must be exported as PNG or SVG.'
}
if (Test-Path -LiteralPath $imageFile) {
    throw "Refusing to overwrite an existing baseline: $imageFile"
}

$designer = New-Object -ComObject PowerDesigner.Application
$model = $null
try {
    if ([string]$designer.Version -ne '16.7.4.6866') {
        throw "PowerDesigner 16.7.4.6866 is required; found $($designer.Version)."
    }
    $model = $designer.OpenModel($modelFile)
    if ($null -eq $model) { throw "PowerDesigner could not open: $modelFile" }
    $matches = @()
    for ($index = 0; $index -lt $model.PhysicalDiagrams.Count; $index++) {
        $diagram = $model.PhysicalDiagrams.Item($index)
        if ([string]$diagram.Code -eq $DiagramCode) { $matches += $diagram }
    }
    if ($matches.Count -ne 1) {
        throw "Expected one physical diagram with code '$DiagramCode'; found $($matches.Count)."
    }
    $directory = [IO.Path]::GetDirectoryName($imageFile)
    [IO.Directory]::CreateDirectory($directory) | Out-Null
    $matches[0].ExportImage($imageFile)
    if (-not (Test-Path -LiteralPath $imageFile -PathType Leaf)) {
        throw "PowerDesigner did not export the image: $imageFile"
    }
    Write-Output "Exported native diagram: $imageFile"
}
finally {
    if ($null -ne $model) { $model.Close() }
    [Runtime.InteropServices.Marshal]::ReleaseComObject($designer) | Out-Null
}

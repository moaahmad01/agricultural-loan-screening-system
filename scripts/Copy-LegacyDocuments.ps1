param(
    [Parameter(Mandatory = $true)][string]$ApplicationRoot,
    [switch]$Apply
)
$ErrorActionPreference = 'Stop'
$appRoot = (Resolve-Path -LiteralPath $ApplicationRoot).Path
if (-not (Test-Path -LiteralPath (Join-Path $appRoot 'AgriculturalLoanSystem.csproj'))) {
    throw 'ApplicationRoot must be the application project folder.'
}
$sourceRoot = Join-Path $appRoot 'wwwroot/uploads'
$destinationRoot = Join-Path $appRoot 'App_Data/Documents'
$count = 0
foreach ($category in @('identity_docs', 'screening_docs')) {
    $source = Join-Path $sourceRoot $category
    if (-not (Test-Path -LiteralPath $source)) { continue }
    foreach ($directory in @($sourceRoot, $source, (Join-Path $appRoot 'App_Data'), $destinationRoot)) {
        if ((Test-Path -LiteralPath $directory) -and
            ((Get-Item -LiteralPath $directory).Attributes -band [IO.FileAttributes]::ReparsePoint)) {
            throw 'Symlinks and junctions require manual review.'
        }
    }
    $destination = Join-Path $destinationRoot $category
    if ((Test-Path -LiteralPath $destination) -and
        ((Get-Item -LiteralPath $destination).Attributes -band [IO.FileAttributes]::ReparsePoint)) {
        throw 'Destination cannot be a symlink or junction.'
    }
    foreach ($file in Get-ChildItem -LiteralPath $source -Force) {
        if ($file.PSIsContainer -or ($file.Attributes -band [IO.FileAttributes]::ReparsePoint) -or
            $file.Name.IndexOfAny([char[]]'\:%') -ge 0) {
            throw 'Unsupported legacy entry; inspect manually before copying.'
        }
        $target = Join-Path $destination $file.Name
        if (Test-Path -LiteralPath $target) {
            if ((Get-Item -LiteralPath $target).Attributes -band [IO.FileAttributes]::ReparsePoint) {
                throw 'Destination file cannot be a symlink.'
            }
            if ((Get-FileHash -LiteralPath $file.FullName).Hash -ne (Get-FileHash -LiteralPath $target).Hash) {
                throw 'Conflicting destination file; nothing was overwritten.'
            }
        } elseif ($Apply) {
            New-Item -ItemType Directory -Path $destination -Force | Out-Null
            Copy-Item -LiteralPath $file.FullName -Destination $target
            if ((Get-FileHash -LiteralPath $file.FullName).Hash -ne (Get-FileHash -LiteralPath $target).Hash) {
                throw 'Copy verification failed. Preserve the original and investigate.'
            }
        }
        $count++
    }
}
Write-Output "Checked $count legacy documents. Apply=$Apply. Originals were preserved."
Write-Output 'Keep the app offline until public originals have been securely moved outside every web-server/CDN root. See docs/SECURITY.md.'

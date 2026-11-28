param(
    [string]$Configuration = "Release",
    [string]$Runtime = "win-x64",
    [string]$Version = "3.23.3.0"
)

$ErrorActionPreference = "Stop"
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$publishDir = Join-Path $repoRoot "publish"
$portableDir = Join-Path $publishDir "portable"
$portableZip = Join-Path $publishDir "FPM-portable-$($Version.Replace('.0$', '')).zip"
$checksums = Join-Path $publishDir "SHA256SUMS.txt"

Get-Process FPM -ErrorAction SilentlyContinue | ForEach-Object {
    Stop-Process -Id $_.Id -Force
}

dotnet publish (Join-Path $repoRoot "app\src\PCManager\PCManager.csproj") `
    -c $Configuration -r $Runtime --self-contained true `
    -p:PublishReadyToRun=true -o $publishDir `
    -p:Version=$Version -p:FileVersion=$Version -p:AssemblyVersion=$Version

dotnet publish (Join-Path $repoRoot "app\src\Helper\PCManager.Helper.csproj") `
    -c $Configuration -r $Runtime --self-contained true `
    -p:PublishReadyToRun=false -o $publishDir `
    -p:Version=$Version -p:FileVersion=$Version -p:AssemblyVersion=$Version

if (-not (Test-Path (Join-Path $publishDir "FPM.Helper.exe"))) {
    throw "The elevated helper was not published beside FPM.exe."
}

New-Item -ItemType Directory -Force $portableDir | Out-Null
Get-ChildItem -LiteralPath $publishDir -Force |
    Where-Object {
        $_.FullName -ne $portableDir -and
        $_.FullName -notlike (Join-Path $publishDir "installer") -and
        $_.Name -ne "SHA256SUMS.txt" -and
        $_.Name -notlike "FPM-portable-*.zip"
    } |
    Copy-Item -Destination $portableDir -Recurse -Force
Set-Content -Path (Join-Path $portableDir "portable.flag") -Value "FPM portable build" -NoNewline

if (Test-Path $portableZip) {
    Remove-Item $portableZip -Force
}
Compress-Archive -Path (Join-Path $portableDir "*") -DestinationPath $portableZip -CompressionLevel Optimal

$iscc = Get-Command iscc -ErrorAction SilentlyContinue
if (-not $iscc) {
    foreach ($candidate in @(
        "$env:ProgramFiles(x86)\Inno Setup 6\ISCC.exe",
        "$env:ProgramFiles\Inno Setup 6\ISCC.exe",
        "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe"
    )) {
        if (Test-Path $candidate) {
            $iscc = Get-Item $candidate
            break
        }
    }
}
if ($iscc) {
    $isccCommand = if ($iscc.PSObject.Properties.Name -contains "Source") { $iscc.Source } else { $iscc.FullName }
    & $isccCommand (Join-Path $repoRoot "installer\FPM.iss")
    if ($LASTEXITCODE -ne 0) {
        throw "Inno Setup failed with exit code $LASTEXITCODE."
    }
} else {
    Write-Warning "iscc was not found. Portable package was produced, but the installer was not built."
}

$files = @($portableZip)
$installer = Join-Path (Join-Path $publishDir "installer") "FPM-Setup-$($Version.Replace('.0$', '')).exe"
if (Test-Path $installer) {
    $files += $installer
}

$lines = foreach ($file in $files) {
    $hash = (Get-FileHash -Algorithm SHA256 -LiteralPath $file).Hash.ToLowerInvariant()
    "{0}  {1}" -f $hash, [IO.Path]::GetFileName($file)
}
Set-Content -Path $checksums -Value $lines -Encoding ASCII

Write-Output "Portable: $portableZip"
Write-Output "Checksums: $checksums"

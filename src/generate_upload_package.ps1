# This script builds and packages the OutSystems ODC External Library.

$projectName = "UnicodeSpoofGuard"
$configuration = "Release"
$outputDir = "..\.out"

if (Test-Path $outputDir) {
    Remove-Item -Recurse -Force $outputDir
}
New-Item -ItemType Directory -Force -Path $outputDir

dotnet publish -c $configuration --force "$projectName.csproj"

$publishDir = "bin\$configuration\net8.0\publish\"
$zipFileName = "$outputDir\ExternalLibrary.zip"
Compress-Archive -Path "$publishDir\*" -DestinationPath $zipFileName

Write-Host "External library package created at $zipFileName"
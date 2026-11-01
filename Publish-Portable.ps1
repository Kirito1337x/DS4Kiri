$ErrorActionPreference = "Stop"

$projectPath = Join-Path $PSScriptRoot "DS4Kiri.csproj"
dotnet publish $projectPath -p:PublishProfile=Portable
if ($LASTEXITCODE -ne 0) {
    throw "Portable publish failed with exit code $LASTEXITCODE."
}

Write-Host "Portable app created in: $(Join-Path $PSScriptRoot 'portable')"

param(
  [switch]$NoDeploy
)

$ErrorActionPreference = "Stop"

$proj = Join-Path $PSScriptRoot "Mutations.csproj"
$deployFlag = if ($NoDeploy) { "/p:MutationsNoDeploy=true" } else { "" }

dotnet build $proj -c Release $deployFlag
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

Write-Host "Build complete."

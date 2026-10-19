$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
Push-Location $projectRoot
try {
    Push-Location frontend
    try {
        if (-not (Test-Path node_modules)) { npm.cmd ci; if ($LASTEXITCODE -ne 0) { throw 'npm ci failed' } }
        npm.cmd run build
        if ($LASTEXITCODE -ne 0) { throw 'Frontend build failed' }
    } finally { Pop-Location }
    dotnet run --project src/TaskFlow.Web
    if ($LASTEXITCODE -ne 0) { throw 'Application failed' }
} finally { Pop-Location }

$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
Push-Location $projectRoot
try {
    Push-Location frontend
    try {
        if (-not (Test-Path node_modules)) { npm.cmd ci; if ($LASTEXITCODE -ne 0) { throw 'npm ci failed' } }
        npm.cmd test
        if ($LASTEXITCODE -ne 0) { throw 'Frontend tests failed' }
        npm.cmd run build
        if ($LASTEXITCODE -ne 0) { throw 'Frontend build failed' }
    } finally { Pop-Location }
    dotnet test -m:1 --logger 'trx;LogFileName=results.trx'
    if ($LASTEXITCODE -ne 0) { throw 'Backend tests failed' }
} finally { Pop-Location }

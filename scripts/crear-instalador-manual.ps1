$ErrorActionPreference = 'Stop'
Push-Location (Split-Path $PSScriptRoot -Parent)
try {
    & "$PSScriptRoot/validar-publicado.ps1"
    & "$PSScriptRoot/preparar-vcredist.ps1"
    $iscc = & "$PSScriptRoot/preparar-inno.ps1"
    & $iscc /Q /DExigirVcRedist instalador/GeneradorAnexos.iss
    if ($LASTEXITCODE -ne 0) { throw 'No se pudo compilar el instalador.' }
} finally { Pop-Location }

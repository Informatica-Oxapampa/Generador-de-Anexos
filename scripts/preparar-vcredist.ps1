# Microsoft mantiene este enlace para el runtime x64 compatible.
# La firma Authenticode se valida antes de incluirlo; nunca se ejecuta al compilar.
$ErrorActionPreference = 'Stop'
$ruta = Join-Path $PWD 'instalador/redist/VC_redist.x64.exe'
New-Item (Split-Path $ruta) -ItemType Directory -Force | Out-Null
Invoke-WebRequest 'https://aka.ms/vs/17/release/vc_redist.x64.exe' -OutFile $ruta
$firma = Get-AuthenticodeSignature $ruta
if ($firma.Status -ne 'Valid' -or $firma.SignerCertificate.Subject -notmatch 'O=Microsoft Corporation(?:,|$)') {
    Remove-Item $ruta -Force
    throw 'El redistribuible no tiene una firma válida de Microsoft.'
}
Write-Host "Visual C++ x64 verificado. SHA-256: $((Get-FileHash $ruta -Algorithm SHA256).Hash)"

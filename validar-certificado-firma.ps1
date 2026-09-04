param()

$ErrorActionPreference = 'Stop'
$pfx = $env:GENERADOR_ANEXOS_SIGN_PFX
$clave = $env:GENERADOR_ANEXOS_SIGN_PASSWORD

if ([string]::IsNullOrWhiteSpace($pfx) -or -not (Test-Path -LiteralPath $pfx -PathType Leaf)) {
    throw 'GENERADOR_ANEXOS_SIGN_PFX no apunta a un archivo PFX existente.'
}

$certificado = [Security.Cryptography.X509Certificates.X509Certificate2]::new(
    $pfx,
    $clave,
    [Security.Cryptography.X509Certificates.X509KeyStorageFlags]::EphemeralKeySet)

if (-not $certificado.HasPrivateKey) {
    throw 'El PFX no contiene la clave privada.'
}

$ahora = (Get-Date).ToUniversalTime()
if ($certificado.NotBefore.ToUniversalTime() -gt $ahora -or
    $certificado.NotAfter.ToUniversalTime() -le $ahora) {
    throw 'El certificado aún no es válido o ya venció.'
}

$eku = $certificado.Extensions |
    Where-Object { $_.Oid.Value -eq '2.5.29.37' } |
    Select-Object -First 1
if (-not $eku -or
    -not ($eku.EnhancedKeyUsages | Where-Object { $_.Value -eq '1.3.6.1.5.5.7.3.3' })) {
    throw 'El certificado no permite firma de código.'
}

$huella = $certificado.GetCertHashString(
    [Security.Cryptography.HashAlgorithmName]::SHA256)
$configuracion = Get-Content -LiteralPath (
    Join-Path $PSScriptRoot 'src\GeneradorAnexos.WinUI\Services\Actualizaciones\ConfiguracionActualizaciones.cs'
) -Raw
if ($configuracion -notmatch [regex]::Escape($huella)) {
    throw "La huella SHA-256 $huella no está fijada en ConfiguracionActualizaciones.cs."
}

Write-Host "Certificado institucional válido y fijado: $huella"

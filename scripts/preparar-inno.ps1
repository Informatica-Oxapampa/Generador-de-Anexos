# Distribución oficial fijada por versión y SHA-256.
# https://github.com/jrsoftware/issrc/releases/tag/is-6_4_3
$ErrorActionPreference = 'Stop'
$version = '6.4.3'
$sha256 = 'f3c42116542c4cc57263c5ba6c4feabfc49fe771f2f98a79d2f7628b8762723b'
$carpeta = Join-Path ([IO.Path]::GetTempPath()) "generador-inno-$version"
$descarga = Join-Path ([IO.Path]::GetTempPath()) ("innosetup-" + [guid]::NewGuid() + '.exe')
try {
    Invoke-WebRequest "https://github.com/jrsoftware/issrc/releases/download/is-6_4_3/innosetup-$version.exe" -OutFile $descarga
    if ((Get-FileHash $descarga -Algorithm SHA256).Hash -ne $sha256) {
        throw 'La descarga de Inno Setup no coincide con el SHA-256 oficial fijado.'
    }
    $proceso = Start-Process -FilePath $descarga -Wait -PassThru -ArgumentList @(
        '/VERYSILENT', '/SUPPRESSMSGBOXES', '/NORESTART', '/SP-', "/DIR=`"$carpeta`"")
    if ($proceso.ExitCode -ne 0) { throw "Inno Setup no pudo instalarse: $($proceso.ExitCode)." }
    $iscc = Join-Path $carpeta 'ISCC.exe'
    if (!(Test-Path $iscc)) { throw 'La instalación no generó ISCC.exe.' }
    # El SHA-256 fija la versión del paquete completo. ISCC.exe es un lanzador
    # y su recurso FileVersion declara 0.0.0.0, no la versión del compilador.
    Write-Host "Inno Setup $version preparado y verificado."
    Write-Output $iscc
} finally {
    Remove-Item $descarga -Force -ErrorAction SilentlyContinue
}

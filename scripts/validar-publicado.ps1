param([string]$Carpeta = 'publicado')
$ErrorActionPreference = 'Stop'
$proyecto = [xml](Get-Content 'src/GeneradorAnexos.WinUI/GeneradorAnexos.WinUI.csproj' -Raw)
$version = @($proyecto.Project.PropertyGroup.Version | Where-Object { $_ })[0]
foreach ($archivo in @('GeneradorAnexos.exe', 'GeneradorAnexos.dll', 'GeneradorAnexos.pri',
    'GeneradorAnexos.runtimeconfig.json', 'coreclr.dll', 'hostfxr.dll', 'Microsoft.UI.Xaml.dll',
    'plantillas/plantilla_tdr.docx', 'plantillas/plantilla_anexos.docx', 'plantillas/catalogos.json', 'plantillas/version.txt')) {
    $ruta = Join-Path $Carpeta $archivo
    if (!(Test-Path $ruta) -or (Get-Item $ruta).Length -eq 0) { throw "Publicación incompleta: $archivo" }
}
$real = [Diagnostics.FileVersionInfo]::GetVersionInfo((Resolve-Path "$Carpeta/GeneradorAnexos.exe")).FileVersion
if ($real -ne "$version.0") { throw "Ejecutable $real; proyecto $version" }
$instalador = Get-Content instalador/GeneradorAnexos.iss -Raw
if ($instalador -notmatch ('#define\s+MiVersion\s+"' + [regex]::Escape($version) + '"')) { throw 'Versión del instalador incoherente.' }
$prohibidos = @(Get-ChildItem $Carpeta -Recurse -File | Where-Object { $_.Extension -in '.pfx','.p12','.key','.db','.log' })
if ($prohibidos.Count) { throw 'La publicación contiene archivos privados o datos de ejecución.' }
Write-Host "Publicación $version validada: ejecutable, recursos, runtimes y plantillas."

"""Prepara los archivos de una Release desde el instalador generado por CI."""
import argparse
import datetime as dt
import hashlib
import json
import re
from pathlib import Path
import xml.etree.ElementTree as ET
import zipfile


def preparar(repo, salida, raiz=Path(".")):
    if not re.fullmatch(r"[A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+", repo):
        raise ValueError("Repositorio inválido")
    version = ET.parse(raiz / "src/GeneradorAnexos.WinUI/GeneradorAnexos.WinUI.csproj").findtext(".//Version")
    plant_ver = (raiz / "plantillas/version.txt").read_text().strip()
    for v in (version, plant_ver):
        if not re.fullmatch(r"[0-9]+\.[0-9]+\.[0-9]+", v or ""):
            raise ValueError("Versión inválida")
    setup = salida / f"GeneradorAnexos-{version}-Setup.exe"
    if not setup.is_file() or setup.stat().st_size == 0:
        raise ValueError("Falta el instalador generado por CI")
    documentos = ["plantilla_anexos.docx", "plantilla_tdr.docx"]
    plantilla = salida / f"plantillas-{plant_ver}.zip"
    with zipfile.ZipFile(plantilla, "w", zipfile.ZIP_DEFLATED) as z:
        for name in documentos + ["version.txt"]:
            z.write(raiz / "plantillas" / name, name)
    ahora = dt.datetime.now(dt.timezone.utc)
    base = f"https://github.com/{repo}/releases/download/v{version}"
    def paquete(path, ver):
        return dict(version=ver, fecha=ahora.date().isoformat(), url=f"{base}/{path.name}",
                    sha256=hashlib.file_digest(path.open("rb"), "sha256").hexdigest(),
                    tamano=path.stat().st_size, obligatoria=False, versionMinima="", notas=[])
    manifiesto = dict(manifiesto=2, publicado=ahora.isoformat(), release=f"v{version}",
                     app=paquete(setup, version), plantillas=paquete(plantilla, plant_ver))
    manifiesto["plantillas"]["archivos"] = documentos
    (salida / "update.json").write_text(json.dumps(manifiesto, ensure_ascii=False, indent=2), encoding="utf-8")
    paths = [setup, plantilla, salida / "update.json"]
    (salida / "SHA256SUMS.txt").write_text("".join(
        f"{hashlib.file_digest(p.open('rb'), 'sha256').hexdigest()}  {p.name}\n" for p in paths), encoding="ascii")
    return manifiesto


if __name__ == "__main__":
    parser = argparse.ArgumentParser()
    parser.add_argument("--repo", required=True)
    parser.add_argument("--salida", type=Path, default=Path("salida"))
    args = parser.parse_args()
    preparar(args.repo, args.salida)

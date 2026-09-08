"""Prepara una publicación del programa o solo de sus plantillas."""
import argparse
import copy
import datetime as dt
import hashlib
import json
import re
from pathlib import Path
import xml.etree.ElementTree as ET
import zipfile


def numero(valor):
    if not re.fullmatch(r"[0-9]+\.[0-9]+\.[0-9]+", valor or ""):
        raise ValueError(f"Versión inválida: {valor!r}. Use mayor.menor.parche.")
    return tuple(map(int, valor.split('.')))


def planificar(raiz, anterior=None):
    version = ET.parse(raiz / "src/GeneradorAnexos.WinUI/GeneradorAnexos.WinUI.csproj").findtext(".//Version")
    plant_ver = (raiz / "plantillas/version.txt").read_text().strip()
    app, plant = numero(version), numero(plant_ver)
    modo = "app"
    if anterior is not None:
        if anterior.get("manifiesto") != 2:
            raise ValueError("La Release Latest debe contener un update.json de formato 2.")
        app_previa = numero(anterior["app"]["version"])
        plant_previa = numero(anterior["plantillas"]["version"])
        if app < app_previa or plant < plant_previa:
            raise ValueError("Las versiones del repositorio son anteriores a Latest. No se publicará una regresión.")
        if app == app_previa:
            modo = "plantillas" if plant > plant_previa else "sin-cambios"
    tag = f"plantillas-{plant_ver}" if modo == "plantillas" else f"v{version}"
    return dict(modo=modo, tag=tag, app=version, plantillas=plant_ver)


def sha256(path):
    with path.open("rb") as archivo:
        return hashlib.file_digest(archivo, "sha256").hexdigest()


def preparar(repo, salida, raiz=Path("."), anterior=None):
    if not re.fullmatch(r"[A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+", repo):
        raise ValueError("Repositorio inválido")
    plan = planificar(raiz, anterior)
    if plan["modo"] == "sin-cambios":
        raise ValueError("No hay versiones nuevas que preparar.")
    salida.mkdir(parents=True, exist_ok=True)
    ahora = dt.datetime.now(dt.timezone.utc)
    base = f"https://github.com/{repo}/releases/download/{plan['tag']}"

    def paquete(path, ver):
        return dict(version=ver, fecha=ahora.date().isoformat(), url=f"{base}/{path.name}",
                    sha256=sha256(path), tamano=path.stat().st_size,
                    obligatoria=False, versionMinima="", notas=[])

    paths = []
    if plan["modo"] == "plantillas":
        # Mantiene exactamente el instalador, URL y hash ya publicados.
        app = copy.deepcopy(anterior["app"])
        prefijo = f"https://github.com/{repo}/releases/download/"
        if not app.get("url", "").startswith(prefijo) or not re.fullmatch(r"[0-9a-fA-F]{64}", app.get("sha256", "")) or app.get("tamano", 0) <= 0:
            raise ValueError("El instalador de Latest no tiene URL, hash o tamaño válidos.")
    else:
        setup = salida / f"GeneradorAnexos-{plan['app']}-Setup.exe"
        if not setup.is_file() or setup.stat().st_size == 0:
            raise ValueError("Falta el instalador generado por CI")
        app = paquete(setup, plan["app"])
        paths.append(setup)
    documentos = ["plantilla_anexos.docx", "plantilla_tdr.docx"]
    plantilla = salida / f"plantillas-{plan['plantillas']}.zip"
    with zipfile.ZipFile(plantilla, "w", zipfile.ZIP_DEFLATED) as z:
        for name in documentos + ["version.txt"]:
            z.write(raiz / "plantillas" / name, name)
    manifiesto = dict(manifiesto=2, publicado=ahora.isoformat(), release=plan["tag"],
                     app=app, plantillas=paquete(plantilla, plan["plantillas"]))
    manifiesto["plantillas"]["archivos"] = documentos
    (salida / "update.json").write_text(json.dumps(manifiesto, ensure_ascii=False, indent=2), encoding="utf-8")
    paths += [plantilla, salida / "update.json"]
    (salida / "SHA256SUMS.txt").write_text("".join(
        f"{sha256(p)}  {p.name}\n" for p in paths), encoding="ascii")
    return manifiesto


if __name__ == "__main__":
    parser = argparse.ArgumentParser()
    parser.add_argument("--repo", required=True)
    parser.add_argument("--salida", type=Path, default=Path("salida"))
    parser.add_argument("--anterior", type=Path)
    args = parser.parse_args()
    previo = json.loads(args.anterior.read_text(encoding="utf-8-sig")) if args.anterior else None
    preparar(args.repo, args.salida, anterior=previo)

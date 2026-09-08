"""Orquesta GitHub Releases; solo prepara borradores, nunca sobrescribe publicaciones."""
import importlib.util
import json
import os
from pathlib import Path
import subprocess
import tempfile
import sys
from urllib.parse import quote

spec = importlib.util.spec_from_file_location("preparar", Path(__file__).with_name("preparar-release.py"))
preparacion = importlib.util.module_from_spec(spec)
spec.loader.exec_module(preparacion)


def gh(*args, permitir_404=False):
    resultado = subprocess.run(["gh", *args], text=True, capture_output=True)
    if resultado.returncode:
        if permitir_404 and "(HTTP 404)" in resultado.stderr:
            return None
        raise RuntimeError(resultado.stderr.strip() or "GitHub no completó la operación.")
    return resultado.stdout


def api(repo, ruta):
    respuesta = gh("api", f"repos/{repo}/releases/{ruta}", permitir_404=True)
    return json.loads(respuesta) if respuesta is not None else None


def comprobar_destino(existente, sha):
    if existente is None:
        return
    if not existente.get("draft"):
        raise ValueError("Esta etiqueta ya está publicada. Aumente la versión correspondiente; no se reemplazará una Release publicada.")
    if existente.get("target_commitish") != sha:
        raise ValueError("Existe un borrador de esa versión preparado desde otro commit. Elimine ese borrador pendiente o aumente la versión antes de repetir.")


def ejecutar(raiz=Path(".")):
    repo, sha, run = (os.environ[k] for k in ("GH_REPO", "GITHUB_SHA", "GITHUB_RUN_ID"))
    latest = api(repo, "latest")
    with tempfile.TemporaryDirectory() as temporal:
        carpeta = Path(temporal)
        anterior = None
        if latest:
            gh("release", "download", latest["tag_name"], "--repo", repo,
               "--pattern", "update.json", "--dir", str(carpeta))
            anterior = json.loads((carpeta / "update.json").read_text(encoding="utf-8-sig"))
        plan = preparacion.planificar(raiz, anterior)
        if plan["modo"] == "sin-cambios":
            mensaje = "Las versiones del aplicativo y las plantillas ya están publicadas. Para actualizar las plantillas, aumente plantillas/version.txt."
            print(mensaje)
            return mensaje
        existente = api(repo, "tags/" + quote(plan["tag"], safe=""))
        comprobar_destino(existente, sha)
        salida = carpeta / "salida"
        salida.mkdir()
        if plan["modo"] == "app":
            gh("run", "download", run, "--repo", repo,
               "--name", f"instalador-pruebas-sin-firma-{sha}", "--dir", str(salida))
        preparacion.preparar(repo, salida, raiz, anterior)
        titulo = f"Plantillas {plan['plantillas']}" if plan["modo"] == "plantillas" else f"v{plan['app']}"
        notas = carpeta / "notas.txt"
        notas.write_text(
            f"Aplicativo: {plan['app']}. Plantillas: {plan['plantillas']}.\n"
            + ("Actualización exclusiva de plantillas. Conserva el instalador del programa ya publicado.\n"
               if plan["modo"] == "plantillas" else "Incluye instalador para Windows 10/11 x64 y plantillas.\n")
            + "Publique este borrador como estable y marque Set as the latest release para que la aplicación detecte la actualización.\n",
            encoding="utf-8")
        archivos = [str(p) for p in sorted(salida.iterdir()) if p.is_file()]
        if existente:
            gh("release", "upload", plan["tag"], *archivos, "--repo", repo, "--clobber")
            gh("release", "edit", plan["tag"], "--repo", repo, "--title", titulo, "--notes-file", str(notas))
        else:
            gh("release", "create", plan["tag"], *archivos, "--repo", repo,
               "--target", sha, "--title", titulo, "--draft", "--notes-file", str(notas))
        enlace = json.loads(gh("release", "view", plan["tag"], "--repo", repo, "--json", "url"))["url"]
        mensaje = f"Borrador listo: {enlace}\nAplicativo {plan['app']} / plantillas {plan['plantillas']}.\nPublique el borrador como estable y Latest."
        print(mensaje)
        return mensaje


if __name__ == "__main__":
    try:
        mensaje = ejecutar()
        if os.environ.get("GITHUB_STEP_SUMMARY"):
            with open(os.environ["GITHUB_STEP_SUMMARY"], "a", encoding="utf-8") as resumen:
                resumen.write(mensaje + "\n")
    except (ValueError, KeyError, OSError, RuntimeError) as error:
        print(f"No se pudo preparar la Release: {error}", file=sys.stderr)
        sys.exit(1)

import copy
import importlib.util
import json
import os
from pathlib import Path
import tempfile
import unittest
from unittest.mock import patch
import zipfile

spec = importlib.util.spec_from_file_location('publicar', Path(__file__).with_name('publicar-release.py'))
publicar = importlib.util.module_from_spec(spec)
spec.loader.exec_module(publicar)
p = publicar.preparacion
REPO = 'Informatica-Oxapampa/Generador-de-Anexos'


class Releases(unittest.TestCase):
    def setUp(self):
        self.tmp = tempfile.TemporaryDirectory()
        self.addCleanup(self.tmp.cleanup)
        self.raiz = Path(self.tmp.name)
        (self.raiz / 'src/GeneradorAnexos.WinUI').mkdir(parents=True)
        (self.raiz / 'plantillas').mkdir()
        self.versiones('1.0.0', '1.0.0')
        for name in ['plantilla_anexos.docx', 'plantilla_tdr.docx']:
            (self.raiz / 'plantillas' / name).write_bytes(b'contenido de prueba')
        inicial = self.raiz / 'inicial'
        inicial.mkdir()
        (inicial / 'GeneradorAnexos-1.0.0-Setup.exe').write_bytes(b'instalador de prueba')
        self.anterior = p.preparar(REPO, inicial, self.raiz)

    def versiones(self, app, plantillas):
        (self.raiz / 'src/GeneradorAnexos.WinUI/GeneradorAnexos.WinUI.csproj').write_text(f'<Project><PropertyGroup><Version>{app}</Version></PropertyGroup></Project>')
        (self.raiz / 'plantillas/version.txt').write_text(plantillas + '\n')

    def test_primera_release(self):
        self.assertEqual(self.anterior['release'], 'v1.0.0')
        self.assertEqual(self.anterior['app']['version'], '1.0.0')

    def test_solo_plantillas_conserva_instalador_y_contenido(self):
        self.versiones('1.0.0', '1.0.1')
        salida = self.raiz / 'nueva'
        m = p.preparar(REPO, salida, self.raiz, self.anterior)
        self.assertEqual(m['release'], 'plantillas-1.0.1')
        self.assertEqual(m['app'], self.anterior['app'])
        self.assertIn('/plantillas-1.0.1/plantillas-1.0.1.zip', m['plantillas']['url'])
        self.assertFalse(list(salida.glob('*.exe')))
        paquete = salida / 'plantillas-1.0.1.zip'
        self.assertEqual(m['plantillas']['sha256'], p.sha256(paquete))
        with zipfile.ZipFile(paquete) as z:
            self.assertEqual(z.read('version.txt'), b'1.0.1\n')
            self.assertEqual(z.read('plantilla_anexos.docx'), b'contenido de prueba')
        for linea in (salida / 'SHA256SUMS.txt').read_text().splitlines():
            sha, name = linea.split('  ')
            self.assertEqual(sha, p.sha256(salida / name))

    def test_plantillas_sucesivas(self):
        self.versiones('1.0.0', '1.0.1')
        m = p.preparar(REPO, self.raiz / 'uno', self.raiz, self.anterior)
        self.versiones('1.0.0', '1.0.2')
        m2 = p.preparar(REPO, self.raiz / 'dos', self.raiz, m)
        self.assertEqual(m2['release'], 'plantillas-1.0.2')
        self.assertEqual(m2['app'], self.anterior['app'])

    def test_sin_cambios(self):
        self.assertEqual(p.planificar(self.raiz, self.anterior)['modo'], 'sin-cambios')

    def test_no_regresion(self):
        for app, plant in [('0.9.0', '1.0.1'), ('1.0.1', '0.9.0')]:
            self.versiones(app, plant)
            with self.assertRaises(ValueError):
                p.planificar(self.raiz, self.anterior)

    def test_programa_nuevo_despues_de_plantillas(self):
        self.versiones('1.0.0', '1.0.1')
        m = p.preparar(REPO, self.raiz / 'plant', self.raiz, self.anterior)
        self.versiones('1.0.1', '1.0.1')
        salida = self.raiz / 'app'
        salida.mkdir()
        (salida / 'GeneradorAnexos-1.0.1-Setup.exe').write_bytes(b'nuevo instalador')
        n = p.preparar(REPO, salida, self.raiz, m)
        self.assertEqual(n['release'], 'v1.0.1')
        self.assertEqual(n['app']['version'], '1.0.1')

    def test_no_sobrescribe_publicada_o_borrador_ajeno(self):
        for draft, sha in [(False, 'abc'), (True, 'otro')]:
            with self.assertRaises(ValueError):
                publicar.comprobar_destino({'draft': draft, 'target_commitish': sha}, 'abc')
        publicar.comprobar_destino({'draft': True, 'target_commitish': 'abc'}, 'abc')

    def test_instalador_ajeno_rechazado(self):
        self.versiones('1.0.0', '1.0.1')
        anterior = copy.deepcopy(self.anterior)
        anterior['app']['url'] = 'https://example.com/setup.exe'
        with self.assertRaises(ValueError):
            p.preparar(REPO, self.raiz / 'mal', self.raiz, anterior)

    def test_workflow_plantillas_no_descarga_instalador(self):
        self.versiones('1.0.0', '1.0.1')
        llamadas = []
        def gh(*args, **kwargs):
            llamadas.append(args)
            if args[0] == 'api':
                if args[1].endswith('/latest'):
                    return json.dumps({'tag_name': 'v1.0.0'})
                return None
            if args[:2] == ('release', 'download'):
                (Path(args[args.index('--dir') + 1]) / 'update.json').write_text(json.dumps(self.anterior))
            if args[:2] == ('release', 'view'):
                return json.dumps({'url': 'https://github.com/' + REPO + '/releases/tag/plantillas-1.0.1'})
            return ''
        with patch.object(publicar, 'gh', side_effect=gh), patch.dict(os.environ, GH_REPO=REPO, GITHUB_SHA='abc', GITHUB_RUN_ID='123'):
            publicar.ejecutar(self.raiz)
        self.assertFalse(any(x[0] == 'run' for x in llamadas))
        crear = next(x for x in llamadas if x[:2] == ('release', 'create'))
        self.assertEqual(crear[2], 'plantillas-1.0.1')
        self.assertIn('--draft', crear)

    def test_error_de_permisos_no_se_interpreta_como_primera_release(self):
        resultado = type('Resultado', (), {'returncode': 1, 'stdout': '', 'stderr': 'HTTP 403 forbidden'})()
        with patch.object(publicar.subprocess, 'run', return_value=resultado), self.assertRaises(RuntimeError):
            publicar.api(REPO, 'latest')


if __name__ == '__main__':
    unittest.main()

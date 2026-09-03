# Historial de versiones

El formato sigue [Semantic Versioning](https://semver.org/lang/es/):
`MAYOR.MENOR.PARCHE`.

- **PARCHE** — correcciones que no cambian el comportamiento esperado.
- **MENOR** — funciones nuevas compatibles con lo anterior.
- **MAYOR** — cambios importantes o incompatibles.

---

## [1.0.1]

Parche sobre la 1.0.0: correcciones de publicación, pruebas, instancia única y
documentación. Las plantillas Word no cambian (siguen en 1.0.0).

### Corregido

- **Rutas de documentación.** Los manuales y los scripts hablaban de `winui\compilar.cmd`
  y `winui\publicado`. El repositorio tiene esos archivos en la raíz.
- **Consulta DNI en el manual de compilación.** Seguía documentando ApiPeru.dev;
  el código ya usa el servicio desactivado a la espera de RENIEC.
- **Instancia única.** El mutex se publicaba pero no se reclamaba, así que se
  podían abrir dos ventanas y picar el autoguardado. Ahora la segunda instancia
  avisa y no arranca.
- **Comentarios XML duplicados** en `ServiciosApp` y en el catálogo de áreas.
- **Pruebas fuera de la solución.** Domain, pagos, sincronización y el lector
  PDF entran en la `.sln` y corren en GitHub Actions en cada push.
- **Paquetes NuGet con versión flotante.** Sqlite, OpenXML y PdfPig quedan
  fijados para builds reproducibles.
- **Firma opcional en `crear-instalador.cmd`.** El `SET` del Setup iba dentro
  de un `if (...)` de CMD y no se veía la variable.
- **MessageBoxW del aviso de instancia única** ahora busca `user32` solo en
  System32, igual que el resto de P/Invoke.
- **Un solo texto de forma de pago único**, el de dominio; Constantes ya no
  duplica una redacción distinta.

### Cambiado

- **Estado compartido y sincronización TDR→Anexos** salen de WinUI y viven en
  `GeneradorAnexos.Application.Sync`, para que las pruebas no arrastren la UI.

### Añadido

- [LICENSE.md](LICENSE.md) con reserva de derechos de la Municipalidad.

---

## [1.0.0]

Primera versión (etiqueta en GitHub sin instalador). Migración completa del
aplicativo original en Python/PySide6 a .NET 8 con WinUI 3.

### Funcionalidades

- Generación de Términos de Referencia y Anexos N.° 06 al 09 en Word.
- Importación del Pedido de Servicio del SIGA en PDF con autocompletado.
- Sincronización de datos comunes entre TDR y Anexos.
- Cálculo automático del plan de pagos según los entregables.
- Registros guardados con cifrado, autoguardado y copias de seguridad
  rotativas.
- Vista previa de los documentos antes de generarlos.

### Interfaz

- Navegación con `NavigationView`, barra de título integrada y material Mica
  en Windows 11.
- Tema claro, oscuro y alto contraste, siguiendo los recursos de tema del
  propio Windows.
- Sección de Configuración: apariencia, actualizaciones, datos y diagnóstico.

### Distribución

- Instalador con asistente, que instala en `C:\Program Files`.
- Actualización automática del programa desde GitHub Releases, con
  verificación de tamaño y SHA-256.
- Canal independiente de actualización de plantillas, sin permisos de
  administrador.

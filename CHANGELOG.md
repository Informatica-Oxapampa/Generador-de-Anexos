# Historial de versiones

## [No publicado]

### Corregido

- **Los botones TDR y Anexo de la lista ya no generan documentos.** Cargaban el
  registro y generaban de inmediato, así que al pulsarlos aparecía sin aviso el
  cuadro «Guardar como» de Windows: el usuario pedía abrir un registro y se
  encontraba guardando un archivo. Ahora solo abren el registro en la sección
  correspondiente; generar sigue siendo una decisión explícita.
- **El botón «Anexo» se habilitaba en registros que solo tenían TDR.** La
  comprobación miraba, entre otros, el número de pedido, el plazo y la
  descripción del servicio, que la sincronización copia automáticamente desde
  el TDR. Ahora solo se consideran los campos exclusivos del Anexo: los datos
  del proveedor y su propuesta económica.

### Cambiado

- **Al abrir un registro guardado, el documento toma la fecha de hoy.** Antes
  heredaba la fecha con la que se guardó y había que corregirla a mano en el
  caso más frecuente, que es reutilizar los datos para emitir algo nuevo. La
  fecha del registro sigue disponible en un enlace de la cabecera, para cuando
  haga falta reimprimir un documento tal como se emitió.
- Al recuperar el autoguardado se conserva la fecha que había, porque ahí se
  está retomando el mismo trabajo y no empezando otro.

El formato sigue [Semantic Versioning](https://semver.org/lang/es/):
`MAYOR.MENOR.PARCHE`.

- **PARCHE** — correcciones que no cambian el comportamiento esperado.
- **MENOR** — funciones nuevas compatibles con lo anterior.
- **MAYOR** — cambios importantes o incompatibles.

---

## [1.0.3]

Corrección de compilación sobre la versión 1.0.2.

### Corregido

- Se agregó el espacio de nombres de `ISecurityEventSink` en la raíz de
  composición WinUI.
- Al poder compilarse nuevamente el ensamblado local, el compilador XAML puede
  resolver los convertidores y controles propios (`Icono`, `CampoTexto` y
  `FilaLista`) que antes aparecían erróneamente como tipos desconocidos.

## [1.0.2]

Corrección de compilación sobre la versión 1.0.3.

### Corregido

- Se retiró la propiedad `MaxLength` no compatible con `AutoSuggestBox` en
  WinUI 3; el límite del buscador se aplica ahora desde código.
- Se corrigieron las advertencias CA1305 y CA1001 del servicio de respaldos.
- Los semáforos de operaciones de interfaz tienen duración de proceso y no
  generan advertencias de recursos descartables.
- El README y la guía de compilación distinguen claramente entre ejecutar la
  aplicación autocontenida y compilarla con el SDK de .NET 8.
- La guía incluye el comando oficial de instalación del SDK mediante `winget`.

## [1.0.3]

Actualización de estabilidad y seguridad sobre la 1.0.2. Las plantillas
incluidas conservan su numeración independiente.

### Corregido

- Los borradores recuperados permanecen marcados como cambios pendientes hasta
  que el usuario los guarda o descarta explícitamente.
- Cargar otro registro, crear uno nuevo, actualizar o cerrar ya no sustituye
  cambios sin confirmar.
- El autoguardado y la generación de documentos no se ejecutan en paralelo.
- La creación de un registro es atómica; actualizar, renombrar y eliminar
  comprueban que la fila aún exista.
- La lista de registros evita una consulta completa por cada elemento y tolera
  filas dañadas sin impedir el acceso a las demás.
- Las copias de seguridad usan la API de respaldo de SQLite, verificación de
  integridad y nombres únicos.
- La migración de la base heredada conserva el original y migra nombres y datos
  sin dejar información antigua en texto plano.
- El desinstalador ya no puede borrar recursivamente una carpeta elegida ni
  elimina datos de otro perfil bajo elevación.

### Seguridad

- Los registros y borradores cifrados fallan de forma cerrada si su envoltura
  fue manipulada; el texto plano heredado se cifra al migrarse.
- El manifiesto `update.json` requiere una firma CMS separada y fijada al
  certificado institucional.
- El instalador requiere hash, tamaño, firma Authenticode válida y la misma
  huella institucional antes de ejecutarse.
- Las redirecciones, tamaños y dominios de descarga están limitados.
- Los paquetes de plantillas rechazan macros, ActiveX, archivos embebidos,
  relaciones externas no autorizadas y bombas ZIP.
- Los registros de diagnóstico ya no escriben rutas ni mensajes internos de
  las excepciones.

### Cambiado

- Versión del programa: **1.0.3**.
- Windows App SDK 2.4.0, Microsoft.Data.Sqlite 8.0.30 y Open XML SDK 3.5.1.
- Compatibilidad mínima coherente: Windows 10 2004 (19041), x64.
- Los flujos de publicación ejecutan pruebas, fijan las acciones por hash,
  exigen certificado, firman binarios y manifiestos y crean solo borradores.
- Los generadores detienen la operación si faltan marcadores o tablas y
  validan el DOCX final antes de entregarlo.

## [1.0.2]

Parche sobre la 1.0.1. Las plantillas Word no cambian y conservan su versión
independiente.

### Corregido

- **Flujo de registros.** «Nuevo registro» solicita el nombre y crea el registro
  en blanco; «Guardar» actualiza únicamente el registro activo y ya no vuelve a
  mostrar el diálogo de nombre.
- **Estado del registro.** Cuando no existe un registro activo, la interfaz lo
  indica claramente y orienta al usuario a utilizar «Nuevo registro».

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

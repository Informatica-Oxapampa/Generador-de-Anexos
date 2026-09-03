# Historial de versiones

El formato sigue [Semantic Versioning](https://semver.org/lang/es/):
`MAYOR.MENOR.PARCHE`.

- **PARCHE** — correcciones que no cambian el comportamiento esperado.
- **MENOR** — funciones nuevas compatibles con lo anterior.
- **MAYOR** — cambios importantes o incompatibles.

---

## [No publicado]

### Cambiado

- **Consulta de DNI retirada.** La versión anterior obtenía el nombre leyendo
  los formularios públicos de un sitio privado, lo que implicaba enviar el DNI
  de un ciudadano fuera de la entidad sin convenio ni base legal, y dependía de
  que ese sitio no cambiara su maquetación. El botón «Validar» sigue derivando
  el RUC de forma local con el algoritmo de SUNAT y avisa de que la consulta del
  nombre se habilitará al integrar el servicio oficial de RENIEC.
- **Áreas usuarias y entidades bancarias en `catalogos.json`.** Estaban fijas en
  el código, de modo que un cambio en el ROF obligaba a recompilar y reinstalar
  en todos los equipos. Ahora viajan en el paquete de plantillas: se actualizan
  sin permisos de administrador y sin reiniciar el programa.
- **Nombres de archivo con número de pedido y fecha.** Antes dos documentos de
  la misma área o del mismo proveedor proponían el mismo nombre y el segundo
  sobrescribía al primero.

### Añadido

- **Impresión directa con selección de impresora.** Tras generar un documento,
  el diálogo ofrece «Imprimir». Se muestran las impresoras instaladas —locales y
  de red—, el usuario elige una y el documento se envía a esa. Queda
  preseleccionada la última utilizada o la predeterminada de Windows. La
  impresión la ejecuta Word, de modo que el papel conserva exactamente los
  márgenes, tablas, encabezados y saltos de página de la plantilla.
- Comprobación de integridad de la base de registros en Configuración › Datos y
  diagnóstico.

### Corregido

- **Generación atómica de documentos.** La plantilla se copiaba sobre el archivo
  de destino y se editaba ahí: si la generación fallaba a medias quedaba un
  documento corrupto y, al sobrescribir uno anterior válido, se perdía. Ahora se
  compone en un archivo temporal y solo se coloca en su sitio al terminar.
- **Errores al abrir documentos que pasaban desapercibidos.** Abrir el documento
  generado no controlaba excepciones dentro de una tarea sin observar: si el
  archivo se había movido o el equipo no tenía Word, al pulsar el botón no
  ocurría nada. Ahora se informa con un mensaje que indica qué hacer.
- **Mensajes técnicos en la interfaz.** Varios errores mostraban el texto de la
  excepción, que podía incluir rutas internas del equipo o mensajes de SQLite.
  El detalle pasa al registro de diagnóstico y al usuario se le explica el
  problema en términos comprensibles.

---

## [1.0.0] — pendiente de publicación

Primera versión oficial. Migración completa del aplicativo original en
Python/PySide6 a .NET 8 con WinUI 3, conservando todas las funcionalidades.

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

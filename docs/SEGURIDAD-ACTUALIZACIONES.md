# Análisis de seguridad del sistema de actualización

> **Cómo está configurado ahora mismo (modo predeterminado)**
>
> Todo funciona **solo con GitHub**, sin ningún paso adicional:
>
> 1. El programa pide a GitHub el manifiesto de la última versión publicada.
> 2. Compara la versión instalada con la publicada.
> 3. Si hay una superior, descarga el instalador desde GitHub.
> 4. Comprueba el SHA-256 y el tamaño antes de ejecutarlo.
> 5. Instala y se reinicia.
>
> No hace falta generar ninguna clave ni ejecutar ningún script para publicar
> versiones.

Este documento cuestiona la propuesta inicial, señala dónde fallaba y explica
la arquitectura que se implementó en su lugar.

---

## 1. El problema central de la propuesta inicial

La propuesta era: publicar el instalador y su hash SHA-256 en la Release, y que
el programa comprobara el hash antes de instalar.

**Ese diseño no aporta autenticidad.** El hash y el instalador se publican en el
mismo sitio y viajan por el mismo canal. Quien pueda modificar el origen —una
cuenta de GitHub comprometida, un token con permiso de escritura filtrado, un
colaborador con acceso— puede subir un instalador manipulado **y** el hash que
le corresponde. La verificación pasaría sin objeciones.

Un hash publicado junto al archivo protege contra:

- Descargas corruptas o incompletas.
- Alteración en tránsito.

Y **no** protege contra:

- Compromiso de la cuenta o del repositorio.
- Un token de Actions filtrado.
- Publicación por error o por un tercero con acceso.

Usted lo formuló exactamente bien: *«no quiero un hash que pueda obtenerse desde
la misma fuente que el archivo descargado»*. Es el punto correcto.

---

## 2. Qué se decidió hacer en su lugar

El sistema verifica **tamaño y SHA-256** del instalador contra el manifiesto
antes de ejecutarlo, y solo acepta descargas por HTTPS desde dominios de
GitHub. Con eso quedan cubiertos los escenarios realistas: descarga corrupta,
descarga incompleta y alteración en tránsito.

El escenario que **no** cubre es que alguien entre en la cuenta de GitHub y
publique una versión falsa. Para ese caso, la defensa es la seguridad de la
propia cuenta: **verificación en dos pasos obligatoria** en la organización y
control de quién tiene permiso de escritura.

Existe una protección adicional posible —firmar el manifiesto con una clave
privada que no esté en GitHub— pero se descartó por ahora: obliga a un paso
manual antes de cada publicación y su beneficio solo aparece si la cuenta
institucional se ve comprometida. Si en el futuro el aplicativo se despliega en
muchas más entidades, conviene revisar esa decisión.

## 3. Riesgos analizados etapa por etapa

| Etapa | Riesgo | Cómo se aborda |
|---|---|---|
| Consulta de versión | Interceptación (MITM) | Solo HTTPS; la validación de certificado la hace Windows. No se fija el certificado a propósito: GitHub los rota y el fijado provocaría cortes en cuanto rotara |
| Consulta de versión | Manifiesto sustituido en el origen (cuenta de GitHub comprometida) | La defensa es la seguridad de la cuenta: verificación en dos pasos obligatoria y control de los permisos de escritura |
| Consulta de versión | **Congelación**: servir siempre un manifiesto antiguo y auténtico para que el equipo nunca reciba una corrección | Se guarda la versión más alta vista y se rechaza cualquier manifiesto que anuncie una inferior |
| Comparación | Comparación de versiones como texto | Comparación numérica campo por campo; `1.10.0` es posterior a `1.9.0` |
| Comparación | **Retroceso** a una versión anterior vulnerable | Misma defensa que la congelación, más el rechazo de versiones no superiores a la instalada |
| Descarga | Redirección a otro servidor | Lista blanca de dominios de GitHub y exigencia de HTTPS. Un manifiesto manipulado no puede apuntar a otro sitio |
| Descarga | Ruta de escritura elegida por el atacante | El nombre y la carpeta del archivo los decide la aplicación, nunca el manifiesto. Se sanea el nombre |
| Descarga | Archivo incompleto o corrupto | Se comprueban tamaño exacto y SHA-256. Cualquier fallo borra el archivo |
| Espera | **Condición de carrera**: sustituir el archivo entre la verificación y la ejecución | Se vuelve a comprobar el hash inmediatamente antes de lanzar el instalador; si cambió, se descarta |
| Instalación | Escalada de privilegios por ACL floja | La carpeta del programa hereda la ACL de Archivos de programa. **No** se afloja con `users-modify` |
| Instalación | Servicio actualizador con privilegios permanentes | No existe. La elevación es puntual, visible y la autoriza el usuario en UAC |
| Instalación | Secuestro de DLL | Las llamadas nativas de la aplicación usan `DefaultDllImportSearchPaths(System32)`. El instalador se ejecuta con `/SP-` desde su propia carpeta temporal |
| Instalación | UAC rechazado | Se detecta y se informa; no queda nada a medias |
| Datos | Pérdida de información del usuario | El instalador nunca toca `%LOCALAPPDATA%\GeneradorAnexos` |

---

## 4. Modelo de privilegios

| Componente | Privilegios | Motivo |
|---|---|---|
| Aplicación en uso normal | Usuario estándar | Nunca necesita más |
| Datos, registros, respaldos, preferencias | Usuario estándar | Viven en `%LOCALAPPDATA%` |
| **Actualización de plantillas** | **Usuario estándar** | Se instalan en `%LOCALAPPDATA%`; no hay UAC |
| Actualización del programa | Administrador, una vez | Escribe en Archivos de programa |
| Instalación y desinstalación | Administrador | Ídem |

La consecuencia práctica es buena: las correcciones de plantillas —lo que más se
va a corregir— llegan a los equipos **sin pedir ninguna contraseña de
administrador**. Solo las versiones del programa piden elevación.

---

## 5. Recuperación ante fallos

Lo que sí está garantizado:

- **Nada se ejecuta ni se extrae sin verificar antes.** Es la protección real
  contra dejar el equipo a medias, porque el escenario habitual de instalación
  rota es ejecutar un paquete corrupto.
- **Cancelar es seguro en cualquier momento.** Se borra el archivo parcial y no
  se ha tocado nada de lo instalado.
- **Los datos del usuario nunca se pierden.** Ninguna actualización los toca.
- **Las plantillas tienen vuelta atrás inmediata**, sin red: Configuración ›
  Datos y diagnóstico › Restaurar plantillas incluidas.
- **Vuelta atrás del programa:** reinstalar la versión anterior desde GitHub.
  Como el `AppId` es el mismo, Windows lo trata como la misma aplicación.

Lo que **no** está implementado, y por qué lo digo en vez de disimularlo:

**No hay rollback binario automático.** Restaurar la versión anterior sin
intervención exigiría copiar la carpeta del programa antes de actualizar y que
algo la restaurara si la aplicación no arranca. Pero si no arranca, no puede
restaurarse a sí misma: haría falta un proceso vigilante, y un vigilante con
permiso de escritura en Archivos de programa es exactamente el componente
privilegiado y permanente que esta arquitectura evita a propósito. El remedio
sería peor que la enfermedad.

---

## 6. Qué protege GitHub por sí solo, y qué no

Con la configuración predeterminada, lo que garantiza el sistema es:

- **Que nadie intercepte ni altere la descarga** (HTTPS y lista blanca de
  dominios de GitHub).
- **Que el archivo llegue íntegro y completo** (SHA-256 y tamaño exacto).
- **Que solo se instalen versiones publicadas**, nunca commits de desarrollo.
- **Que no se instale una versión anterior a la que ya se tiene.**

Lo que **no** cubre: que alguien entre en la cuenta de GitHub y publique una
versión falsa. Para ese caso concreto está la verificación en dos pasos de la
cuenta institucional.

Para un aplicativo interno de la municipalidad, con la cuenta protegida con
verificación en dos pasos, esta configuración es razonable y es la que usa la
inmensa mayoría de aplicaciones que se distribuyen por GitHub.

## 7. Lo que falta para cerrar el modelo: firma de código

La firma del manifiesto protege contra un origen comprometido. Queda una pieza:
**Authenticode**, la firma del propio `.exe`.

Con un certificado de firma de código:

- Windows verifica la firma al elevar, y UAC muestra el nombre del publicador en
  lugar de «Editor desconocido».
- Desaparece la advertencia de SmartScreen.
- Se añade una segunda verificación independiente de la nuestra.

Es de pago (unos 200–400 USD al año en certificados OV). Mi recomendación es
adquirirlo antes de desplegar a decenas de equipos: es la mejora de seguridad
con mayor efecto real por lo que cuesta.

Si lo consiguen, firme **antes** de calcular el SHA-256 —firmar modifica el
archivo— y añada la verificación de la firma Authenticode del descargado como
comprobación adicional.

---

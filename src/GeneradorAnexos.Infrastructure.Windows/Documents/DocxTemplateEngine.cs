using System.Text;
using System.Text.RegularExpressions;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Validation;
using DocumentFormat.OpenXml.Wordprocessing;

namespace GeneradorAnexos.Infrastructure.Windows.Documents;

/// <summary>Error controlado durante la generación de un documento.</summary>
public sealed class DocumentoException : Exception
{
    public DocumentoException(string mensaje) : base(mensaje)
    {
    }

    public DocumentoException(string mensaje, Exception interna) : base(mensaje, interna)
    {
    }
}

/// <summary>
/// Motor de plantillas .docx. Sustituye a <c>docxtpl</c> (Jinja sobre
/// python-docx) del aplicativo Python.
/// </summary>
/// <remarks>
/// Las plantillas institucionales usan marcadores <c>{{VARIABLE}}</c>. Word
/// suele partir un marcador en varios <c>Run</c> (por revisiones, corrector
/// ortográfico o cambios de formato), así que la sustitución se hace sobre el
/// texto completo del párrafo y luego se reescribe conservando el formato del
/// primer run. Es el mismo resultado que producía docxtpl.
/// </remarks>
public static class DocxTemplateEngine
{
    private const long TamanoMaximoPlantilla = 15L * 1024 * 1024;
    private static readonly Regex Marcador = new(@"\{\{\s*([A-Za-z0-9_]+)\s*\}\}", RegexOptions.Compiled);

    /// <summary>Copia la plantilla al destino y aplica el contexto.</summary>
    /// <exception cref="DocumentoException">Si la plantilla falta o no se puede escribir.</exception>
    public static void Renderizar(
        string rutaPlantilla,
        string rutaDestino,
        IReadOnlyDictionary<string, string> contexto,
        Action<WordprocessingDocument>? postProceso = null)
    {
        if (!File.Exists(rutaPlantilla))
        {
            throw new DocumentoException(
                $"No se encontró la plantilla necesaria: {Path.GetFileName(rutaPlantilla)}. " +
                "Reinstale la aplicación o restaure las plantillas.");
        }

        if (new FileInfo(rutaPlantilla).Length > TamanoMaximoPlantilla)
        {
            throw new DocumentoException("La plantilla supera el límite permitido de 15 MB.");
        }

        var noResueltos = new HashSet<string>(StringComparer.Ordinal);

        var carpeta = Path.GetDirectoryName(rutaDestino);
        if (!string.IsNullOrEmpty(carpeta))
        {
            Directory.CreateDirectory(carpeta);
        }

        // Se compone sobre un archivo temporal y solo al final se coloca en su
        // sitio. Antes se copiaba la plantilla directamente sobre el destino y
        // se editaba ahí: si la generación fallaba a medias, el usuario se
        // quedaba con un documento corrupto y, si estaba sobrescribiendo uno
        // anterior que sí era válido, lo perdía. Con el temporal, un fallo deja
        // intacto lo que ya había.
        var temporal = rutaDestino + ".generando-" + Guid.NewGuid().ToString("N") + ".tmp";

        try
        {
            Descartar(temporal);
            File.Copy(rutaPlantilla, temporal, overwrite: true);

            using (var documento = WordprocessingDocument.Open(temporal, isEditable: true))
            {
                var cuerpo = documento.MainDocumentPart?.Document?.Body
                    ?? throw new DocumentoException("La plantilla no tiene un cuerpo de documento válido.");

                SustituirEnParte(cuerpo, contexto, noResueltos);

                // Encabezados y pies también pueden contener marcadores.
                var parte = documento.MainDocumentPart!;

                foreach (var encabezado in parte.HeaderParts)
                {
                    if (encabezado.Header is { } cabecera)
                    {
                        SustituirEnParte(cabecera, contexto, noResueltos);
                    }
                }

                foreach (var pie in parte.FooterParts)
                {
                    if (pie.Footer is { } piePagina)
                    {
                        SustituirEnParte(piePagina, contexto, noResueltos);
                    }
                }

                postProceso?.Invoke(documento);

                if (noResueltos.Count > 0)
                {
                    throw new DocumentoException(
                        "La plantilla contiene campos sin correspondencia: " +
                        string.Join(", ", noResueltos.OrderBy(x => x)) + ".");
                }

                parte.Document.Save();

                if (new OpenXmlValidator(FileFormatVersions.Office2019)
                    .Validate(documento).Take(1).Any())
                {
                    throw new DocumentoException(
                        "El documento generado no superó la validación estructural OpenXML.");
                }
            }

            // Reemplazo en una sola operación: o queda el documento anterior o
            // queda el nuevo, nunca uno a medio escribir.
            File.Move(temporal, rutaDestino, overwrite: true);
        }
        catch (DocumentoException)
        {
            Descartar(temporal);
            throw;
        }
        catch (UnauthorizedAccessException excepcion)
        {
            Descartar(temporal);
            throw new DocumentoException(
                "No hay permisos para escribir en la carpeta elegida. "
                + "Guarde el documento en otra ubicación, por ejemplo en Documentos.",
                excepcion);
        }
        catch (IOException excepcion)
        {
            Descartar(temporal);
            throw new DocumentoException(
                "No se pudo escribir el documento. Compruebe que no esté abierto en "
                + "Word y que haya espacio libre en el disco.",
                excepcion);
        }
        catch (Exception excepcion)
        {
            Descartar(temporal);

            // El mensaje técnico va al registro, no al usuario: puede contener
            // rutas internas y no le dice nada a quien está rellenando un TDR.
            throw new DocumentoException(
                "No se pudo generar el documento a partir de la plantilla.",
                excepcion);
        }
    }

    /// <summary>Borra el archivo temporal sin propagar errores.</summary>
    private static void Descartar(string ruta)
    {
        try
        {
            if (File.Exists(ruta))
            {
                File.Delete(ruta);
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private static void SustituirEnParte(
        OpenXmlElement raiz,
        IReadOnlyDictionary<string, string> contexto,
        ISet<string> noResueltos)
    {
        foreach (var parrafo in raiz.Descendants<Paragraph>().ToList())
        {
            SustituirEnParrafo(parrafo, contexto, noResueltos);
        }
    }

    /// <summary>
    /// Sustituye los marcadores de un párrafo.
    /// </summary>
    /// <remarks>
    /// Se intenta primero la sustitución <b>run a run</b>: cada marcador se
    /// reemplaza dentro de su propio run y los demás runs del párrafo no se
    /// tocan. Es el comportamiento que tenía docxtpl en el aplicativo Python y
    /// el único que respeta el formato que el redactor dio a la plantilla.
    ///
    /// Solo si un marcador quedó partido entre varios runs —cosa que Word hace
    /// a veces por revisiones o corrector ortográfico— se recurre a reconstruir
    /// el párrafo completo, que es una operación más destructiva.
    /// </remarks>
    internal static void SustituirEnParrafo(
        Paragraph parrafo,
        IReadOnlyDictionary<string, string> contexto,
        ISet<string> noResueltos)
    {
        var runs = parrafo.Elements<Run>().ToList();
        if (runs.Count == 0)
        {
            return;
        }

        var textos = runs.Select(TextoDe).ToList();
        var original = string.Concat(textos);
        if (!original.Contains("{{", StringComparison.Ordinal))
        {
            return;
        }

        if (SustituirRunARun(runs, textos, contexto, noResueltos))
        {
            return;
        }

        var sustituido = Marcador.Replace(
            original,
            coincidencia => Resolver(coincidencia, contexto, noResueltos));

        if (sustituido == original)
        {
            return;
        }

        EscribirTexto(parrafo, runs, sustituido);
    }

    /// <summary>Texto visible de un run.</summary>
    private static string TextoDe(Run run)
        => string.Concat(run.Elements<Text>().Select(t => t.Text));

    /// <summary>
    /// Resuelve un marcador contra el contexto. Igual que Jinja en el original:
    /// sin valor queda vacío, nunca se imprime el marcador crudo en un
    /// documento oficial.
    /// </summary>
    private static string Resolver(
        System.Text.RegularExpressions.Match coincidencia,
        IReadOnlyDictionary<string, string> contexto,
        ISet<string> noResueltos)
    {
        var clave = coincidencia.Groups[1].Value;
        if (contexto.TryGetValue(clave, out var valor))
        {
            return valor ?? string.Empty;
        }

        noResueltos.Add(clave);
        return string.Empty;
    }

    /// <summary>
    /// Sustituye cada marcador dentro de su propio run, sin alterar el resto
    /// del párrafo.
    /// </summary>
    /// <returns>
    /// <c>false</c> si algún marcador está repartido entre varios runs, en
    /// cuyo caso el llamador debe reconstruir el párrafo completo.
    /// </returns>
    private static bool SustituirRunARun(
        List<Run> runs,
        List<string> textos,
        IReadOnlyDictionary<string, string> contexto,
        ISet<string> noResueltos)
    {
        var completos = Marcador.Matches(string.Concat(textos)).Count;
        var porRun = textos.Sum(texto => Marcador.Matches(texto).Count);

        // Si el total del párrafo no coincide con la suma por run, hay al menos
        // un marcador partido: no se puede resolver run a run.
        if (completos == 0 || completos != porRun)
        {
            return false;
        }

        for (var i = 0; i < runs.Count; i++)
        {
            if (!textos[i].Contains("{{", StringComparison.Ordinal))
            {
                continue;
            }

            var sustituido = Marcador.Replace(
                textos[i],
                c => Resolver(c, contexto, noResueltos));
            if (sustituido != textos[i])
            {
                ReescribirRun(runs[i], sustituido);
            }
        }

        return true;
    }

    /// <summary>Reescribe el texto de un run conservando intacto su formato.</summary>
    private static void ReescribirRun(Run run, string texto)
    {
        run.RemoveAllChildren<Text>();
        run.RemoveAllChildren<Break>();
        AgregarLineas(run, texto);
    }

    /// <summary>
    /// Reconstruye el párrafo en un solo run. Es el camino de respaldo cuando
    /// un marcador está partido entre varios runs.
    /// </summary>
    /// <remarks>
    /// El formato se toma del primer run <b>con texto real</b>, no del primer
    /// run a secas. Las plantillas institucionales suelen empezar la celda con
    /// un run que contiene solo un espacio y que arrastra un formato distinto
    /// —por ejemplo negrita—; tomar ese run como referencia teñía de negrita
    /// todo el párrafo resultante.
    /// </remarks>
    internal static void EscribirTexto(Paragraph parrafo, List<Run> runs, string texto)
    {
        var donante = runs.FirstOrDefault(run => !string.IsNullOrWhiteSpace(TextoDe(run))) ?? runs[0];
        var propiedades = donante.RunProperties?.CloneNode(true) as RunProperties;

        foreach (var run in runs.Skip(1))
        {
            run.Remove();
        }

        var primero = runs[0];
        primero.RemoveAllChildren<Text>();
        primero.RemoveAllChildren<Break>();
        primero.RemoveAllChildren<RunProperties>();

        if (propiedades is not null)
        {
            primero.PrependChild(propiedades);
        }

        AgregarLineas(primero, texto);
    }

    /// <summary>Añade el texto respetando los saltos de línea del usuario.</summary>
    private static void AgregarLineas(Run run, string texto)
    {
        var lineas = texto.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        for (var i = 0; i < lineas.Length; i++)
        {
            if (i > 0)
            {
                run.AppendChild(new Break());
            }

            run.AppendChild(new Text(lineas[i]) { Space = SpaceProcessingModeValues.Preserve });
        }
    }

    // ═══════════════════════ Utilidades de tabla ═══════════════════════

    /// <summary>Texto normalizado de una celda: sin tildes, en mayúsculas y sin espacios extra.</summary>
    internal static string Normalizar(string? texto)
    {
        var descompuesto = (texto ?? string.Empty).Normalize(NormalizationForm.FormD);
        var limpio = new StringBuilder(descompuesto.Length);

        foreach (var caracter in descompuesto)
        {
            if (System.Globalization.CharUnicodeInfo.GetUnicodeCategory(caracter)
                != System.Globalization.UnicodeCategory.NonSpacingMark)
            {
                limpio.Append(caracter);
            }
        }

        return string.Join(' ', limpio.ToString().ToUpperInvariant()
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
    }

    /// <summary>Busca la primera tabla que contenga todas las claves indicadas.</summary>
    internal static Table? BuscarTabla(Body cuerpo, params string[] claves)
    {
        foreach (var tabla in cuerpo.Descendants<Table>())
        {
            var texto = Normalizar(tabla.InnerText);
            if (claves.All(clave => texto.Contains(Normalizar(clave), StringComparison.Ordinal)))
            {
                return tabla;
            }
        }

        return null;
    }

    /// <summary>Escribe el contenido de una celda conservando el formato existente.</summary>
    internal static void EscribirCelda(TableCell celda, string texto, bool? negrita = null)
    {
        var parrafo = celda.Elements<Paragraph>().FirstOrDefault();
        if (parrafo is null)
        {
            parrafo = new Paragraph();
            celda.AppendChild(parrafo);
        }

        // Elimina los párrafos sobrantes: la celda queda con uno solo.
        foreach (var extra in celda.Elements<Paragraph>().Skip(1).ToList())
        {
            extra.Remove();
        }

        var runs = parrafo.Elements<Run>().ToList();
        if (runs.Count == 0)
        {
            var run = new Run();
            parrafo.AppendChild(run);
            runs.Add(run);
        }

        EscribirTexto(parrafo, runs, texto);

        if (negrita is not null)
        {
            var propiedades = runs[0].RunProperties ??= new RunProperties();
            propiedades.Bold = negrita.Value ? new Bold() : null;
        }
    }

    /// <summary>Clona una fila modelo y la inserta después de ella.</summary>
    internal static TableRow ClonarFila(TableRow modelo)
    {
        var clon = (TableRow)modelo.CloneNode(true);
        modelo.Parent!.InsertAfter(clon, modelo);
        return clon;
    }

    /// <summary>
    /// Reemplaza un token por una lista de viñetas, o elimina el párrafo si la
    /// lista está vacía. Equivale a <c>_aplicar_vinetas</c> del original.
    /// </summary>
    internal static void AplicarVinetas(Body cuerpo, string token, IReadOnlyList<string> elementos)
    {
        var parrafo = cuerpo.Descendants<Paragraph>()
            .FirstOrDefault(p => p.InnerText.Contains(token, StringComparison.Ordinal));

        if (parrafo is null)
        {
            throw new DocumentoException(
                $"La plantilla no contiene la sección requerida {token}.");
        }

        if (elementos.Count == 0)
        {
            parrafo.Remove();
            return;
        }

        var padre = parrafo.Parent!;
        OpenXmlElement anterior = parrafo;

        for (var i = 0; i < elementos.Count; i++)
        {
            var nuevo = (Paragraph)parrafo.CloneNode(true);
            var runs = nuevo.Elements<Run>().ToList();
            if (runs.Count == 0)
            {
                var run = new Run();
                nuevo.AppendChild(run);
                runs.Add(run);
            }

            EscribirTexto(nuevo, runs, elementos[i]);
            padre.InsertAfter(nuevo, anterior);
            anterior = nuevo;
        }

        parrafo.Remove();
    }
}

using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using GeneradorAnexos.Application.Abstractions.Integrations;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;

namespace GeneradorAnexos.Infrastructure.Windows.Integrations;

/// <summary>
/// Lee la primera página del Pedido SIGA por posiciones, como lector_pedido.py.
/// Los importes y clasificadores se buscan únicamente en sus columnas, nunca
/// en el texto global (que también contiene versiones, fechas y otros códigos).
/// No requiere Python ni OCR en el equipo del usuario.
/// </summary>
public sealed class OrderPdfReader : IOrderPdfReader
{
    private static readonly TimeSpan RegexTimeout = TimeSpan.FromSeconds(1);
    private const RegexOptions Options = RegexOptions.IgnoreCase | RegexOptions.CultureInvariant;
    private const string OrderToken = @"[A-ZÁÉÍÓÚÜÑ0-9][A-ZÁÉÍÓÚÜÑ0-9./-]{0,19}";
    private const string OrderLabel = @"(?:PEDIDO\s+DE\s+SERVICIO\s*N\s*[°ºo.]?|N(?:[°º]|RO\.?|[UÚ]M(?:ERO)?\.?)\s*(?:DE\s+)?PEDIDO)";
    private static readonly string[] SectionLabels =
    [
        "DIRECCION SOLICITANTE", "DEPENDENCIA SOLICITANTE", "AREA USUARIA", "OFICINA",
        "ENTREGAR A", "FECHA", "ACTIVIDAD OPERATIVA", "MOTIVO", "DENOMINACION",
        "TIPO USO", "FF/RB", "FF/R", "META", "MNEMONICO", "FUNCION", "CODIGO",
        "CLASIFICADOR", "DESCRIPCION", "VALOR", "UNIDAD MEDIDA", "UNIDAD DE MEDIDA",
        "UNIDAD EJECUTORA", "NRO. IDENTIFICACION", "PEDIDO DE SERVICIO",
        "FIRMA DEL", "FIRMA AUTORIZADA"
    ];

    public Task<OrderData> ReadFirstPageAsync(
        string pdfPath, CancellationToken cancellationToken = default)
        => Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!File.Exists(pdfPath))
            {
                throw new FileNotFoundException(
                    "No se encontró el archivo del Pedido de Servicio.", pdfPath);
            }

            using var document = PdfDocument.Open(pdfPath);
            if (document.NumberOfPages == 0)
            {
                throw new InvalidOperationException("El PDF no contiene páginas.");
            }

            var page = document.GetPage(1);
            var words = page.GetWords().Where(w => !string.IsNullOrWhiteSpace(w.Text))
                .Select(w => new PdfWord(w.Text, w.BoundingBox.Left, w.BoundingBox.Right,
                    w.BoundingBox.Bottom)).ToList();
            cancellationToken.ThrowIfCancellationRequested();
            if (words.Count == 0)
            {
                throw new InvalidOperationException(
                    "El PDF no contiene texto extraíble. Verifique que sea el Pedido " +
                    "original del SIGA y no una imagen escaneada.");
            }

            // Márgenes relativos al ancho A4 de los formatos SIGA originales.
            var scale = page.Width / 595.32;
            var rows = GroupRows(words, 3 * scale);
            var sections = SectionLabels.Select(label => FindLabel(rows, label))
                .OfType<Label>().Distinct().ToList();
            var office = ReadBlock(words, rows, sections, scale,
                "DIRECCION SOLICITANTE", "DEPENDENCIA SOLICITANTE", "AREA USUARIA", "OFICINA");
            var reason = ReadBlock(words, rows, sections, scale,
                "MOTIVO", "DENOMINACION DEL SERVICIO", "DENOMINACION");
            var meta = ReadMeta(words, rows, scale);
            var item = ReadItem(words, rows, scale);
            var result = new OrderData(ReadOrderNumber(rows), reason, office, meta,
                item.Classifier, item.Amount, item.Unit, item.Description);
            cancellationToken.ThrowIfCancellationRequested();

            if (new[] { result.Number, reason, office, meta, item.Classifier,
                    item.Amount, item.Unit, item.Description }.All(string.IsNullOrWhiteSpace))
            {
                throw new InvalidOperationException(
                    "No se identificaron campos del Pedido de Servicio SIGA. " +
                    "Verifique el PDF o complete los datos manualmente.");
            }
            return result;
        }, cancellationToken);

    private sealed record PdfWord(string Text, double Left, double Right, double Y)
    {
        public double Center => (Left + Right) / 2;
    }

    private sealed record Row(double Y, List<PdfWord> Words)
    {
        public string Text => string.Join(" ", Words.Select(w => w.Text));
    }

    private sealed record Label(double Y, double Left, double Right);
    private sealed record Item(string Classifier, string Amount, string Unit, string Description);

    // La tolerancia absorbe diferencias de fuente/línea base. Redondear Y a
    // enteros separaba el rótulo, los dos puntos y el valor de una misma fila.
    private static List<Row> GroupRows(IEnumerable<PdfWord> words, double tolerance)
    {
        var rows = new List<Row>();
        foreach (var word in words.OrderByDescending(w => w.Y).ThenBy(w => w.Left))
        {
            if (rows.Count == 0 || rows[^1].Y - word.Y > tolerance)
            {
                rows.Add(new Row(word.Y, new List<PdfWord>()));
            }
            rows[^1].Words.Add(word);
        }
        foreach (var row in rows)
        {
            row.Words.Sort((a, b) => a.Left.CompareTo(b.Left));
        }
        return rows;
    }

    private static string Normalize(string text)
    {
        var result = new StringBuilder();
        foreach (var c in text.Normalize(NormalizationForm.FormD))
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
            {
                result.Append(char.ToUpperInvariant(c));
            }
        }
        return result.ToString().Normalize(NormalizationForm.FormC);
    }

    private static Match Match(string text, string pattern) => Regex.Match(text, pattern, Options, RegexTimeout);

    // Solo rótulos al principio de la fila: «OFICINA» dentro del valor no es
    // otra etiqueta. Los índices normalizados nunca se usan para cortar texto.
    private static Label? FindLabel(List<Row> rows, string label)
    {
        var tokens = label.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        foreach (var row in rows)
        {
            if (row.Words.Count < tokens.Length) continue;
            if (tokens.Where((token, i) => Normalize(row.Words[i].Text).TrimEnd(':') != token).Any()) continue;
            return new Label(row.Words[0].Y, row.Words[0].Left, row.Words[tokens.Length - 1].Right);
        }
        return null;
    }

    private static string ReadBlock(List<PdfWord> words, List<Row> rows,
        List<Label> sections, double scale, params string[] labels)
    {
        var label = labels.Select(name => FindLabel(rows, name)).OfType<Label>().FirstOrDefault();
        if (label is null) return string.Empty;

        // El SIGA centra «Motivo» respecto de varias líneas: la primera línea
        // del valor puede estar ENCIMA del rótulo. El límite es la sección previa.
        var previous = sections.Where(s => s.Y > label.Y + 4 * scale)
            .OrderBy(s => s.Y).FirstOrDefault();
        var top = previous is null ? label.Y + 6 * scale : previous.Y - 4 * scale;
        var next = sections.Where(s => s.Y < label.Y - 4 * scale).OrderByDescending(s => s.Y).FirstOrDefault();
        var bottom = next is null ? 0 : next.Y + 4 * scale;
        var colon = words.Where(w => w.Text == ":" && w.Left >= label.Right
                && Math.Abs(w.Y - label.Y) <= 4 * scale)
            .OrderBy(w => w.Left).FirstOrDefault();
        var left = colon?.Right ?? label.Right;
        var block = GroupRows(words.Where(w => w.Left >= left && w.Y <= top && w.Y > bottom
            && w.Text != ":"), 3 * scale);
        var lines = new List<string>();
        double? lastY = null;
        foreach (var row in block)
        {
            // No saltar grandes espacios vacíos para capturar pies de página.
            if (lastY.HasValue && lastY.Value - row.Y > 24 * scale) break;
            lines.Add(row.Text);
            lastY = row.Y;
        }
        return Regex.Replace(string.Join(" ", lines), @"\s+", " ", RegexOptions.None, RegexTimeout).Trim();
    }

    private static string ReadMeta(List<PdfWord> words, List<Row> rows, double scale)
    {
        var header = rows.FirstOrDefault(r => r.Words.Any(w => Normalize(w.Text) == "MNEMONICO")
            && r.Words.Any(w => Normalize(w.Text) == "FUNCION"));
        if (header is null) return string.Empty;
        var mnemonic = header.Words.First(w => Normalize(w.Text) == "MNEMONICO");
        var function = header.Words.First(w => Normalize(w.Text) == "FUNCION");
        var values = GroupRows(words.Where(w => w.Y < header.Y - 4 * scale
            && w.Y > header.Y - 35 * scale && w.Center >= mnemonic.Left - 30 * scale
            && w.Center < function.Left - 6 * scale), 3 * scale);
        var text = values.FirstOrDefault()?.Text ?? string.Empty;
        return Match(text, @"^\d{1,6}$").Success ? text : string.Empty;
    }

    private static Item ReadItem(List<PdfWord> words, List<Row> rows, double scale)
    {
        var header = rows.FirstOrDefault(r => r.Words.Any(w => Normalize(w.Text) == "CLASIFICADOR")
            && r.Words.Any(w => Normalize(w.Text) == "VALOR")
            && r.Words.Any(w => Normalize(w.Text) == "UNIDAD"));
        if (header is null) return new Item("", "", "", "");
        var classifierHeader = header.Words.First(w => Normalize(w.Text) == "CLASIFICADOR");
        var amountHeader = header.Words.First(w => Normalize(w.Text) == "VALOR");
        var unitHeader = header.Words.First(w => Normalize(w.Text) == "UNIDAD");
        if (!(classifierHeader.Left < amountHeader.Left && amountHeader.Left < unitHeader.Left))
            return new Item("", "", "", "");

        var classLeft = classifierHeader.Left - 12 * scale;
        var amountLeft = amountHeader.Left - 6 * scale;
        var unitLeft = unitHeader.Left - 8 * scale;
        var candidates = GroupRows(words.Where(w => w.Y < header.Y - 4 * scale
            && w.Y > header.Y - 40 * scale), 3 * scale);
        // El formulario Python representa el PRIMER ítem, no suma otras filas.
        var row = candidates.FirstOrDefault();
        if (row is null) return new Item("", "", "", "");
        string InBand(double start, double end) => string.Join(" ", row.Words
            .Where(w => w.Center >= start && w.Center < end).Select(w => w.Text));
        var classifier = NormalizeClassifier(InBand(classLeft, amountLeft));
        var amount = NormalizeAmount(InBand(amountLeft, unitLeft));
        var unit = InBand(unitLeft, double.MaxValue).Trim();
        if (unit.Length > 60 || !unit.Any(char.IsLetter) || Match(unit, @"\bFirma\b").Success) unit = "";

        // Respaldo de denominación: descripción íntegra del primer ítem,
        // restringida a su columna y hasta la siguiente fila con código/firma.
        var codeHeader = header.Words.FirstOrDefault(w => Normalize(w.Text) == "CODIGO");
        var description = "";
        if (codeHeader is not null)
        {
            var code = row.Words.FirstOrDefault(w => w.Center < classLeft && Match(w.Text, @"^\d{8,}$").Success);
            if (code is not null)
            {
                var descriptionLeft = code.Right + 4 * scale;
                var end = rows.Where(r => r.Y < row.Y - 4 * scale &&
                    (r.Words.Any(w => w.Center < descriptionLeft && Match(w.Text, @"^\d{8,}$").Success)
                     || Match(Normalize(r.Text), @"^FIRMA\b").Success))
                    .Select(r => r.Y).DefaultIfEmpty(0).Max();
                var descriptionRows = GroupRows(words.Where(w => w.Y <= row.Y + 3 * scale && w.Y > end + 3 * scale
                    && w.Center >= descriptionLeft && w.Center < classLeft), 3 * scale);
                var lines = new List<string>();
                double last = row.Y;
                foreach (var line in descriptionRows)
                {
                    if (last - line.Y > 24 * scale) break;
                    lines.Add(line.Text);
                    last = line.Y;
                }
                description = string.Join(" ", lines).Trim();
            }
        }
        return new Item(classifier, amount, unit, description);
    }

    private static string NormalizeClassifier(string text)
    {
        if (!Match(text.Trim(), @"^\d[\d.\s]*$").Success) return string.Empty;
        var parts = Regex.Split(text.Trim(), @"[.\s]+", RegexOptions.None, RegexTimeout)
            .Where(p => p.Length > 0).ToArray();
        // Clasificador presupuestal SIGA: seis segmentos; una versión no es gasto.
        return parts.Length == 6 && parts[0] == "2" && parts.All(p => p.Length <= 2)
            ? string.Join(".", parts) : string.Empty;
    }

    private static string NormalizeAmount(string text)
    {
        var clean = Regex.Replace(text, @"\s+|S/\.?", "", Options, RegexTimeout);
        // Admite 3,000.00 y 3.000,00, además de 500.00 y 500,00.
        if (!Match(clean, @"^(?:\d+|\d{1,3}(?:,\d{3})+)\.\d{2}$").Success
            && !Match(clean, @"^(?:\d+|\d{1,3}(?:\.\d{3})+),\d{2}$").Success)
            return string.Empty;
        var decimalIndex = Math.Max(clean.LastIndexOf('.'), clean.LastIndexOf(','));
        var normalized = clean[..decimalIndex].Replace(",", "").Replace(".", "") + "." + clean[(decimalIndex + 1)..];
        return decimal.TryParse(normalized, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var amount)
            ? amount.ToString("0.00", CultureInfo.InvariantCulture) : string.Empty;
    }

    private static bool IsReliableOrderNumber(string text)
    {
        return text.Length is >= 2 and <= 20 && Match(text, "^" + OrderToken + "$").Success
            && text.Any(char.IsDigit)
            && !Match(text, @"^\d{1,3}(?:[.,]\d{3})*[.,]\d{2}$").Success
            && !(text.Count(c => c == '.') >= 2 && Match(text, @"^[\d.]+$").Success)
            && !Match(text, @"^\d{1,2}[/-]\d{1,2}[/-]\d{2,4}$").Success;
    }

    private static string ReadOrderNumber(List<Row> rows)
    {
        for (var i = 0; i < rows.Count; i++)
        {
            var match = Match(rows[i].Text, OrderLabel);
            if (!match.Success) continue;
            var after = rows[i].Text[(match.Index + match.Length)..].TrimStart(' ', ':');
            if (IsReliableOrderNumber(after)) return after;
            var before = rows[i].Text[..match.Index].Trim();
            if (IsReliableOrderNumber(before)) return before;
            // Número en una línea vecina: no saltar hacia fechas, unidad
            // ejecutora u otros números fuera del encabezado del pedido.
            foreach (var neighbor in new[] { i - 1, i + 1 })
            {
                if (neighbor >= 0 && neighbor < rows.Count
                    && Math.Abs(rows[neighbor].Y - rows[i].Y) <= 24
                    && IsReliableOrderNumber(rows[neighbor].Text)) return rows[neighbor].Text;
            }
        }
        return string.Empty;
    }
}

using System.Text.Json;
using GeneradorAnexos.Infrastructure.Windows.Integrations;
using UglyToad.PdfPig.Writer;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Fonts.Standard14Fonts;

var reader = new OrderPdfReader();
var fixturePath = args.Length > 0 ? args[0] : Path.Combine(AppContext.BaseDirectory, "Fixtures");

// Los pedidos reales del SIGA no se publican en el repositorio porque llevan el
// nombre de la persona a la que se entrega el servicio. Ver Fixtures/LEEME.md.
// Sin ellos se ejecuta el resto de la bateria, que usa PDF sinteticos generados
// aqui mismo con la misma biblioteca que produccion.
var expectedPath = Path.Combine(fixturePath, "python_expected.json");
var expected = File.Exists(expectedPath)
    ? JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, string>>>(
        File.ReadAllText(expectedPath))!
    : new Dictionary<string, Dictionary<string, string>>();

if (expected.Count == 0)
{
    Console.WriteLine("AVISO: sin pedidos reales en Fixtures; se omiten esas comprobaciones.");
}

var checks = 0;
void Equal(string name, string actual, string wanted)
{
    if (actual != wanted) throw new Exception($"{name}: esperado [{wanted}], obtenido [{actual}]");
    checks++;
    Console.WriteLine("OK " + name);
}
async Task Throws<T>(string name, Func<Task> action) where T : Exception
{
    try { await action(); }
    catch (T) { checks++; Console.WriteLine("OK " + name); return; }
    throw new Exception(name + ": no se rechazó la entrada.");
}
foreach (var (file, values) in expected)
{
    var realPath = Path.Combine(fixturePath, file);
    if (!File.Exists(realPath))
    {
        Console.WriteLine("OMITIDO " + file + " (no disponible en este equipo)");
        continue;
    }

    var data = await reader.ReadFirstPageAsync(realPath);
    foreach (var (key, actual) in new[] { ("numero", data.Number), ("motivo", data.Reason),
        ("direccion_solicitante", data.RequestingOffice), ("meta", data.Meta),
        ("clasificador", data.Classifier), ("valor", data.Amount), ("unidad", data.Unit) })
        Equal(file + "/" + key, actual, values[key]);
    Equal(file + "/descripcion completa de respaldo", data.Description, file == "pedido.pdf"
        ? "SERVICIO DE DIGITALIZACIÓN Y DIGITACIÓN DE DOCUMENTOS"
        : "SERVICIO DE INSTALACION DE GABINETE Y CABLEADO ESTRUCTURADO DE RED");
}

// PDF sinteticos temporales: mismo lector y misma biblioteca que produccion.
var temp = Path.Combine(Path.GetTempPath(), "OrderPdfRegression-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(temp);
try
{
    string Make(string name, string amount = "3,000.00", string classifier = "2.3. 2 7.11 99",
        string order = "000123-A", string numberLabel = "PEDIDO DE SERVICIO N.",
        double numberY = 770, bool reason = true, bool twoItems = false, bool table = true,
        bool empty = false, bool unrelated = false, double scale = 1, bool secondPage = false)
    {
        using var builder = new PdfDocumentBuilder();
        var font = builder.AddStandard14Font(Standard14Font.Helvetica);
        var page = builder.AddPage(595.32 * scale, 842 * scale);
        void Text(string value, double x, double y, double size = 8)
        {
            if (value.Length > 0) page.AddText(value, size * scale, new PdfPoint(x * scale, y * scale), font);
        }
        if (!empty)
        {
            Text("Version 26.01.00.U1", 20, 810);
            Text("Fecha : 25/08/2026", 450, 810);
            if (!unrelated)
            {
                Text(numberLabel, 170, 770, 11); Text(order, 336, numberY);
                Text("Tipo Uso : Consumo", 360, 713);
                Text("Direccion Solicitante", 44, 701); Text(":", 124, 701);
                Text("OFICINA DE TECNOLOGIA DE LA INFORMACION", 132, 701);
                Text("Entregar a Sr(a)", 44, 689); Text(": PERSONAL DE PRUEBA", 124, 689);
                Text("Actividad Operativa", 44, 665); Text(": C0507 REDES", 124, 665);
                if (reason)
                {
                    Text("Motivo", 44, 651); Text(":", 124, 651);
                    Text("PRIMERA LINEA DEL SERVICIO", 132, 654, 6);
                    Text("SEGUNDA LINEA SIN RECORTAR", 132, 647, 6);
                    Text("TERCERA LINEA COMPLETA", 132, 640, 6);
                }
                Text("FF/Rb", 47, 614); Text("META / MNEMONICO", 74, 614);
                Text("Funcion", 156, 614); Text("5-08", 49, 599); Text("0054", 99, 599); Text("03", 164, 599);
                if (table)
                {
                    Text("Codigo", 45, 573); Text("Descripcion / Terminos de Referencia", 138, 573);
                    Text("Clasificador", 346, 573); Text("Valor S/.", 402, 573); Text("Unidad Medida", 475, 573);
                    Text("526000130349", 29, 557); Text("DESCRIPCION DEL PRIMER ITEM", 90, 558, 7);
                    Text(classifier, 340, 557, 7); Text(amount, 431, 557, 7); Text("SERVICIO", 481, 557, 7);
                    Text("CONTINUACION DEL PRIMER ITEM", 90, 550, 7);
                    if (twoItems)
                    {
                        Text("526000130350", 29, 530); Text("SEGUNDO ITEM NO IMPORTAR", 90, 530, 7);
                        Text("2.3. 2 7. 4 99", 340, 530, 7); Text("999.00", 431, 530, 7);
                        Text("OTRA", 481, 530, 7);
                    }
                }
                Text("Firma del Solicitante", 128, 425); Text("Firma Autorizada", 384, 425);
            }
            else Text("DOCUMENTO SIN CAMPOS DE PEDIDO", 40, 750);
        }
        if (secondPage)
        {
            var other = builder.AddPage(595.32, 842);
            other.AddText("PEDIDO DE SERVICIO N. 009999", 11, new PdfPoint(170, 770), font);
        }
        var path = Path.Combine(temp, name + ".pdf");
        File.WriteAllBytes(path, builder.Build());
        return path;
    }

    var complete = await reader.ReadFirstPageAsync(Make("completo", twoItems: true, secondPage: true));
    Equal("motivo de tres lineas con primera encima del rotulo", complete.Reason,
        "PRIMERA LINEA DEL SERVICIO SEGUNDA LINEA SIN RECORTAR TERCERA LINEA COMPLETA".ToUpperInvariant());
    Equal("area sin Tipo Uso ni Entregar", complete.RequestingOffice, "OFICINA DE TECNOLOGIA DE LA INFORMACION");
    Equal("numero literal de primera pagina", complete.Number, "000123-A");
    Equal("clasificador del primer item", complete.Classifier, "2.3.2.7.11.99");
    Equal("importe del primer item", complete.Amount, "3000.00");
    Equal("unidad del primer item", complete.Unit, "SERVICIO");
    Equal("descripcion sin segundo item ni firma", complete.Description,
        "DESCRIPCION DEL PRIMER ITEM CONTINUACION DEL PRIMER ITEM");

    foreach (var (input, output) in new[] { ("500.00", "500.00"), ("500,00", "500.00"),
        ("3,000.00", "3000.00"), ("3.000,00", "3000.00"), ("3000.00", "3000.00"), ("0.00", "0.00") })
    {
        var data = await reader.ReadFirstPageAsync(Make("monto", amount: input));
        Equal("monto " + input, data.Amount, output);
    }

    foreach (var number in new[] { "", "25/08/2026", "500.00", "2.3.2.7.4.99" })
    {
        var data = await reader.ReadFirstPageAsync(Make("numero_invalido", order: number));
        Equal("no inventar pedido desde " + number, data.Number, "");
    }
    foreach (var label in new[] { "NRO. PEDIDO", "NUMERO DE PEDIDO", "NRO DE PEDIDO" })
    {
        var data = await reader.ReadFirstPageAsync(Make("rotulo", numberLabel: label));
        Equal("rotulo " + label, data.Number, "000123-A");
    }
    foreach (var y in new[] { 784.0, 756.0 })
    {
        var data = await reader.ReadFirstPageAsync(Make("numero_vecino", numberY: y));
        Equal("numero vecino " + y, data.Number, "000123-A");
    }

    var missing = await reader.ReadFirstPageAsync(Make("sin_monto", amount: ""));
    Equal("monto ausente no toma version", missing.Amount, "");
    missing = await reader.ReadFirstPageAsync(Make("sin_clasificador", classifier: ""));
    Equal("clasificador ausente no toma version", missing.Classifier, "");
    missing = await reader.ReadFirstPageAsync(Make("sin_tabla", table: false));
    Equal("sin tabla no inventar monto", missing.Amount, "");
    Equal("sin tabla no inventar clasificador", missing.Classifier, "");
    missing = await reader.ReadFirstPageAsync(Make("sin_motivo", reason: false));
    Equal("motivo ausente", missing.Reason, "");
    Equal("respaldo sin motivo", missing.Description,
        "DESCRIPCION DEL PRIMER ITEM CONTINUACION DEL PRIMER ITEM");
    var scaled = await reader.ReadFirstPageAsync(Make("escala", scale: 1.5));
    Equal("monto en pagina escalada", scaled.Amount, "3000.00");
    Equal("meta en pagina escalada", scaled.Meta, "0054");
    await Throws<FileNotFoundException>("archivo inexistente", () => reader.ReadFirstPageAsync(Path.Combine(temp, "missing.pdf")));
    await Throws<InvalidOperationException>("PDF sin texto", () => reader.ReadFirstPageAsync(Make("vacio", empty: true)));
    await Throws<InvalidOperationException>("documento ajeno", () => reader.ReadFirstPageAsync(Make("ajeno", unrelated: true)));
    await Throws<OperationCanceledException>("cancelacion", () => reader.ReadFirstPageAsync(
        Make("cancelacion"), new CancellationToken(true)));
}
finally
{
    Directory.Delete(temp, recursive: true);
}

Console.WriteLine($"TOTAL: {checks} comprobaciones correctas.");

using System.Text.RegularExpressions;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using GeneradorAnexos.Infrastructure.Windows.Documents;
using GeneradorAnexos.Domain.Payments;
using GeneradorAnexos.Domain.Models;


internal static class PruebasCuadroPagos
{
public static async Task EjecutarAsync(string carpeta)
{
Dictionary<string,string> Context(string path)
{
    using var doc = WordprocessingDocument.Open(path, false);
    return Regex.Matches(doc.MainDocumentPart!.Document!.InnerText, @"\{\{\s*([A-Za-z0-9_]+)\s*\}\}")
        .Select(m=>m.Groups[1].Value).Distinct().ToDictionary(k=>k,k=> k=="OFICINA" ? "Oficina de Tecnología de la Información" : "Dato de prueba");
}
foreach(var cantidad in new[]{1,2,3,8})
{
    var cuotas = Enumerable.Range(1,cantidad).Select(i=>new CuotaPago(i,"Informe de actividades","25 días",
        $"A la presentación del entregable {i}, previo informe de conformidad del área usuaria.",100/cantidad+(i==cantidad?100%cantidad:0),1000m/cantidad)).ToArray();
    var plan = cantidad==1 ? ConstructorPlanPagos.Construir(null,"1000") : new PlanPagos("multiple",cuotas,1000m);
    var anexo=Path.Combine(carpeta,$"pagos-anexo-{cantidad}.docx");
    var tdr=Path.Combine(carpeta,$"pagos-tdr-{cantidad}.docx");
    await new AnnexDocumentGenerator().GenerateAsync(Context(RutasPlantillas.Anexos()),anexo,plan);
    await new TdrDocumentGenerator().GenerateAsync(Context(RutasPlantillas.Tdr()),tdr,new TdrPayload(),plan);
    using var a=WordprocessingDocument.Open(anexo,false);
    using var t=WordprocessingDocument.Open(tdr,false);
    Table Pago(WordprocessingDocument d)=>d.MainDocumentPart!.Document!.Descendants<Table>().First(x=> x.Elements<TableRow>().FirstOrDefault()?.InnerText.Contains("PORCENTAJE")==true);
    var pa=Pago(a);var pt=Pago(t);
    if(pa.InnerText!=pt.InnerText || pa.Elements<TableRow>().Count()!=cantidad+2 || !pa.InnerText.EndsWith("100 %")) throw new InvalidOperationException("Cuadro diferente");
    var celda = pa.Parent as TableCell ?? throw new InvalidOperationException("Cuadro fuera de la celda de pago");
    if (cantidad == 1)
    {
        if (celda.Elements<Paragraph>().Any(p => !string.IsNullOrWhiteSpace(p.InnerText)) ||
            celda.FirstChild is not TableCellProperties || celda.Elements<Table>().Count() != 1 ||
            !pa.InnerText.Contains("ÚNICO PAGO"))
            throw new InvalidOperationException("Único entregable debe contener únicamente el cuadro de pago");
    }
    else if (!celda.Elements<Paragraph>().Any(p => p.InnerText.Contains("Pagos Periódicos")))
        throw new InvalidOperationException("Falta texto editable en pagos múltiples");
    Console.WriteLine($"OK cuadros TDR/Anexos idénticos con {cantidad} pagos, total 100 %, documentos OpenXML válidos.");
}

}
}

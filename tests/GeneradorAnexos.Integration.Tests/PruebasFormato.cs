using DocumentFormat.OpenXml.Wordprocessing;
using GeneradorAnexos.Infrastructure.Windows.Documents;

internal static class PruebasFormato
{
    public static void Ejecutar(Action<string, bool> check)
    {
        var contexto = new Dictionary<string, string> { ["OFICINA"] = "Tecnología", ["OTRA"] = "Soporte\nMunicipal" };
        void Sustituir(Paragraph p) => DocxTemplateEngine.SustituirEnParrafo(p, contexto, new HashSet<string>());
        Run Negrita(string texto) => new(new RunProperties(new Bold(), new Italic(), new Color { Val = "123456" }), new Text(texto));
        const string token = "{{OFICINA}}";
        for (var corte = 1; corte < token.Length; corte++)
        {
            var antes = new Run(new Text("La "));
            var despues = new Run(new Text(" de la Municipalidad."));
            var p = new Paragraph(antes, Negrita(token[..corte]), Negrita(token[corte..]), despues);
            Sustituir(p);
            var valor = p.Descendants<Text>().Single(t => t.Text == "Tecnología");
            var formato = ((Run)valor.Parent!).RunProperties!;
            check("formato de variable partida " + corte, p.InnerText == "La Tecnología de la Municipalidad."
                && formato.Bold is not null && formato.Italic is not null && formato.Color?.Val == "123456"
                && antes.RunProperties is null && despues.RunProperties is null);
        }
        var mixto = new Paragraph(new Run(new Text("{{")), Negrita("OFICINA"), new Run(new Text("}}")));
        Sustituir(mixto);
        check("formato del nombre aunque llaves sean normales", mixto.InnerText == "Tecnología"
            && ((Run)mixto.Descendants<Text>().Single(t => t.Text == "Tecnología").Parent!).RunProperties?.Bold is not null);
        var multiples = new Paragraph(Negrita("{{OFICINA}} y {{OTRA}}"), new Run(new TabChar(), new Text("fin"), new Break()));
        Sustituir(multiples);
        check("múltiples variables y saltos conservados", multiples.InnerText == "Tecnología y SoporteMunicipalfin"
            && multiples.Descendants<Break>().Count() == 2 && multiples.Descendants<TabChar>().Count() == 1);
        var fragmentos = new Paragraph(new Run(new RunProperties(new Bold()), new Text("{{OF"), new Text("ICINA}}")));
        Sustituir(fragmentos);
        check("varios nodos de texto en un run", fragmentos.InnerText == "Tecnología" && fragmentos.Elements<Run>().Single().RunProperties?.Bold is not null);
        var enlace = new Paragraph(new Hyperlink(Negrita("{{OFICINA}}")) { Anchor = "destino" });
        Sustituir(enlace);
        check("variable en enlace", enlace.InnerText == "Tecnología" && enlace.Elements<Hyperlink>().Single().Anchor == "destino");
        var separado = new Paragraph(new Run(new Text("{{OF"), new TabChar(), new Text("ICINA}}")));
        Sustituir(separado);
        check("no une marcador a través de tabulación", separado.InnerText.Contains("{{OF"));
    }
}

using System.Collections.Generic;
using System.Linq;
using GeneradorAnexos.Domain.Models;

namespace GeneradorAnexos.Domain.Documents;

/// <summary>
/// Equivalente de <c>ui/tab_usuarios.py: usuario_tiene_tdr / usuario_tiene_anexo</c>.
/// </summary>
/// <remarks>
/// Decide si un registro guardado contiene realmente un TDR o un Anexo que
/// pueda volver a cargarse. Se ignora el esqueleto que el formulario crea por
/// su cuenta (cantidad «1», unidad por defecto, texto de carta predeterminado y
/// los porcentajes autodistribuidos), porque su sola presencia no significa que
/// el usuario haya llenado esa sección.
/// </remarks>
public static class ContenidoRegistro
{
    /// <summary>True si hay información de Anexo registrada, aunque esté parcial.</summary>
    /// <remarks>
    /// Solo se miran los campos <b>exclusivos</b> del Anexo, es decir, los datos
    /// del proveedor y su propuesta económica.
    ///
    /// Quedan fuera a propósito <c>NumeroPedido</c>, <c>DiasPlazo</c> y
    /// <c>DescripcionServicio</c>: la sincronización los copia automáticamente
    /// desde el TDR, de modo que un registro que solo tiene TDR los lleva
    /// rellenos. Incluyéndolos, cualquier registro de TDR aparecía como si
    /// tuviera Anexo y el botón se habilitaba sin haber ni un dato del
    /// proveedor.
    ///
    /// El criterio real es el que impone el documento: un Anexo no se puede
    /// emitir sin proveedor.
    /// </remarks>
    public static bool TieneAnexo(BorradorPayloadV1? datos)
    {
        var anexos = datos?.Anexos;
        if (anexos is null)
        {
            return false;
        }

        return new[]
        {
            anexos.NombreProveedor, anexos.Dni, anexos.RucProveedor,
            anexos.DireccionProveedor, anexos.CelularProveedor, anexos.EmailProveedor,
            anexos.CuentaProveedor, anexos.CciProveedor, anexos.Monto,
        }.Any(TieneTexto);
    }

    /// <summary>True si el registro contiene datos significativos de un TDR.</summary>
    public static bool TieneTdr(BorradorPayloadV1? datos)
    {
        var tdr = datos?.Tdr;
        if (tdr is null)
        {
            return false;
        }

        var generales = tdr.Generales;
        if (generales is not null && new[]
            {
                generales.Oficina, generales.NumeroPedido, generales.ActividadPoi,
                generales.FuenteFinanciamiento, generales.Meta, generales.Clasificador,
                generales.DenominacionServicio, generales.ObjetivoContratacion,
                generales.DescripcionFinalidadPublica, generales.ActividadesDesarrollar,
                generales.DiasPlazo,
            }.Any(TieneTexto))
        {
            return true;
        }

        var objeto = tdr.Objeto;
        if (objeto is not null)
        {
            if (TieneTexto(objeto.Descripcion))
            {
                return true;
            }

            // Cantidad «1» y la unidad por defecto son el esqueleto inicial.
            if (TieneTexto(objeto.Cantidad) && objeto.Cantidad!.Trim() != "1")
            {
                return true;
            }

            if (TieneTexto(objeto.Unidad) &&
                !objeto.Unidad!.Trim().Equals("SERVICIO", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        if (new[] { tdr.Requisitos, tdr.Formacion, tdr.Experiencia, tdr.Capacitaciones }
            .Any(lista => lista?.Any(TieneTexto) == true))
        {
            return true;
        }

        if (tdr.Unico is not null && EntregableEditado(tdr.Unico))
        {
            return true;
        }

        if (tdr.Entregables?.Any(e => e is not null && EntregableEditado(e)) == true)
        {
            return true;
        }

        return PagosEditados(tdr.Pagos);
    }

    private static bool TieneTexto(string? valor) => !string.IsNullOrWhiteSpace(valor);

    private static bool EntregableEditado(EntregablePayload entregable)
    {
        if (TieneTexto(entregable.Descripcion) &&
            entregable.Descripcion!.Trim() != TdrLabels.DescripcionCartaDefecto.Trim())
        {
            return true;
        }

        return TieneTexto(entregable.Plazo);
    }

    /// <summary>Detecta pagos editados, excluyendo el contenido autogenerado.</summary>
    private static bool PagosEditados(List<PagoPayload?>? pagos)
    {
        if (pagos is null || pagos.Count == 0)
        {
            return false;
        }

        var porDefecto = TdrLabels.DistribuirPorcentajes(pagos.Count);

        for (var i = 0; i < pagos.Count; i++)
        {
            var pago = pagos[i];
            if (pago is null)
            {
                continue;
            }

            if (TieneTexto(pago.Condicion) &&
                pago.Condicion!.Trim() != TdrLabels.CondicionPagoDefecto(i).Trim())
            {
                return true;
            }

            if (pago.Porcentaje is { } porcentaje && porcentaje != porDefecto[i])
            {
                return true;
            }
        }

        return false;
    }
}

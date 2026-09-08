using GeneradorAnexos.Application.Abstractions.Integrations;

namespace GeneradorAnexos.Infrastructure.Windows.Integrations;

/// <summary>
/// Consulta de DNI desactivada.
/// </summary>
/// <remarks>
/// La consulta automática del nombre permanece apagada hasta integrar el
/// servicio oficial de RENIEC. La versión anterior la resolvía leyendo los
/// formularios públicos de un sitio privado, lo que suponía enviar el DNI de un
/// ciudadano fuera de la entidad sin convenio ni base legal.
///
/// El botón «Validar» sigue siendo útil: deriva el RUC a partir del propio DNI
/// con el algoritmo de SUNAT, sin ninguna conexión, y avisa de que la consulta
/// del nombre llegará en una próxima actualización.
/// </remarks>
public sealed class DisabledDniLookupService : IDniLookupService
{
    public bool IsEnabled => false;

    public Task<DniLookupResult> LookupAsync(string dni, CancellationToken cancellationToken = default)
        => throw new InvalidOperationException(
            "La consulta automática de DNI está desactivada en esta instalación.");
}

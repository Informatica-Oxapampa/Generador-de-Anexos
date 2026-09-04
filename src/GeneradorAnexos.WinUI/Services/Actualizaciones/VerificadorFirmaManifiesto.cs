using System;
using System.Linq;
using System.Security.Cryptography;
using System.Security.Cryptography.Pkcs;
using System.Security.Cryptography.X509Certificates;

namespace GeneradorAnexos.WinUI.Services.Actualizaciones;

/// <summary>Comprueba la firma CMS separada de <c>update.json</c>.</summary>
internal static class VerificadorFirmaManifiesto
{
    private const string OidFirmaCodigo = "1.3.6.1.5.5.7.3.3";

    public static bool EsConfiable(byte[] contenido, byte[] firma)
    {
        if (!ConfiguracionActualizaciones.FirmaInstitucionalConfigurada ||
            contenido.Length == 0 || firma.Length == 0)
        {
            return false;
        }

        try
        {
            var cms = new SignedCms(new ContentInfo(contenido), detached: true);
            cms.Decode(firma);
            if (cms.SignerInfos.Count != 1)
            {
                return false;
            }

            // La coincidencia exacta con la huella fijada es la raíz de
            // confianza. CheckSignature(true) comprueba la firma matemática
            // sin depender de la conectividad a servidores de revocación.
            cms.CheckSignature(verifySignatureOnly: true);
            var certificado = cms.SignerInfos[0].Certificate;
            if (certificado is null ||
                DateTime.UtcNow < certificado.NotBefore.ToUniversalTime() ||
                DateTime.UtcNow > certificado.NotAfter.ToUniversalTime() ||
                !PermiteFirmaDeCodigo(certificado))
            {
                return false;
            }

            var huella = certificado.GetCertHashString(HashAlgorithmName.SHA256);
            return ConfiguracionActualizaciones.FirmantesPermitidosSha256.Any(
                permitida => string.Equals(
                    NormalizarHuella(permitida), huella,
                    StringComparison.OrdinalIgnoreCase));
        }
        catch (CryptographicException)
        {
            return false;
        }
    }

    private static bool PermiteFirmaDeCodigo(X509Certificate2 certificado)
    {
        var extensiones = certificado.Extensions
            .OfType<X509EnhancedKeyUsageExtension>()
            .ToList();

        return extensiones.Count == 1 &&
               extensiones[0].EnhancedKeyUsages
                   .OfType<Oid>()
                   .Any(oid => string.Equals(oid.Value, OidFirmaCodigo, StringComparison.Ordinal));
    }

    private static string NormalizarHuella(string value)
        => value.Replace(" ", string.Empty, StringComparison.Ordinal)
            .Replace(":", string.Empty, StringComparison.Ordinal)
            .Trim();
}

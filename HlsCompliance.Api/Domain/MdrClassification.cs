using System;

namespace HlsCompliance.Api.Domain;

public class MdrClassificationState
{
    public Guid AssessmentId { get; set; }

    /// <summary>
    /// Medisch doel (Excel A2): "Ja", "Nee" of null.
    /// Wordt automatisch afgeleid uit de DPIA-quickscan.
    /// </summary>
    public string? MedischDoel { get; set; }

    /// <summary>
    /// Alleen administratief/generieke communicatie (Excel B2).
    /// In Excel is dit altijd het tegenovergestelde van MedischDoel.
    /// </summary>
    public string? AlleenAdministratiefOfGeneriek
    {
        get
        {
            if (string.Equals(MedischDoel, "Ja", StringComparison.OrdinalIgnoreCase))
            {
                return "Nee";
            }

            if (string.Equals(MedischDoel, "Nee", StringComparison.OrdinalIgnoreCase))
            {
                return "Ja";
            }

            return null;
        }
    }

    /// <summary>
    /// Klinische interpretatie (Excel C2): "Ja", "Nee" of null.
    /// Ook afgeleid uit de DPIA-quickscan.
    /// </summary>
    public string? KlinischeInterpretatie { get; set; }

    /// <summary>
    /// Ondersteunt klinische beslissing (Excel D2): "Ja", "Nee" of null.
    /// Zelfde bron als KlinischeInterpretatie (DPIA).
    /// </summary>
    public string? OndersteuntKlinischeBeslissing { get; set; }

    /// <summary>
    /// Ernst van schade bij fout (Excel E2):
    /// "dodelijk_of_onherstelbaar", "ernstig" of "niet_ernstig".
    /// </summary>
    public string? ErnstSchadeBijFout { get; set; }

    /// <summary>
    /// Uitkomst van de classificatie (Excel F2):
    /// "Onbekend", "Geen medisch hulpmiddel", "Klasse I", "Klasse IIa", "Klasse IIb", "Klasse III".
    /// </summary>
    public string MdrClass { get; set; } = "Onbekend";

    /// <summary>
    /// Korte toelichting bij de uitkomst.
    /// </summary>
    public string Explanation { get; set; } = string.Empty;

    /// <summary>
    /// True als alle criteria zijn ingevuld (incl. DPIA-afleiding en ernst) en er een definitieve klasse is.
    /// </summary>
    public bool IsComplete { get; set; }

    public DateTimeOffset LastUpdated { get; set; } = DateTimeOffset.UtcNow;
}

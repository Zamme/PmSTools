using System.Globalization;
using System.Text;

namespace PmSTools;

internal static class GeocodeScoring
{
    public static string NormalizePostalCodeCandidate(string? token)
    {
        if (string.IsNullOrWhiteSpace(token))
            return string.Empty;

        var normalized = token.Trim().ToUpperInvariant()
            .Replace('O', '0')
            .Replace('Q', '0')
            .Replace('I', '1')
            .Replace('L', '1')
            .Replace('Z', '2')
            .Replace('S', '5')
            .Replace('B', '8');

        normalized = System.Text.RegularExpressions.Regex.Replace(normalized, @"[^0-9]", string.Empty);

        if (normalized.Length > 5)
            normalized = normalized.Substring(0, 5);

        return normalized;
    }

    public static string NormalizeHouseNumberForCompare(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        var normalized = value.Trim().ToUpperInvariant();
        normalized = System.Text.RegularExpressions.Regex.Replace(normalized, @"\s+", string.Empty);
        normalized = System.Text.RegularExpressions.Regex.Replace(normalized, @"[^0-9A-Z]", string.Empty);
        normalized = System.Text.RegularExpressions.Regex.Replace(normalized, @"^0+(\d)", "$1");
        return normalized;
    }

    public static string NormalizeCityForCompare(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        var normalized = RemoveDiacritics(value).ToUpperInvariant();
        normalized = System.Text.RegularExpressions.Regex.Replace(normalized, @"[^A-Z0-9\s]", " ");
        normalized = System.Text.RegularExpressions.Regex.Replace(normalized, @"\s+", " ").Trim();
        return normalized;
    }

    public static bool CandidateMatchesHouseNumber(string? candidateHouseNumber, string? desiredHouseNumber)
    {
        var desired = NormalizeHouseNumberForCompare(desiredHouseNumber);
        if (string.IsNullOrWhiteSpace(desired))
            return false;

        var candidate = NormalizeHouseNumberForCompare(candidateHouseNumber);
        return !string.IsNullOrWhiteSpace(candidate) && string.Equals(candidate, desired, StringComparison.OrdinalIgnoreCase);
    }

    public static bool CandidateHasHouseNumber(string? candidateHouseNumber)
    {
        return !string.IsNullOrWhiteSpace(NormalizeHouseNumberForCompare(candidateHouseNumber));
    }

    public static int ComputeCandidateScore(
        string? candidateType,
        string? candidateHouseNumber,
        string? candidatePostalCode,
        string? candidateCity,
        string? desiredHouseNumber,
        string? desiredPostalCode,
        string? desiredCity,
        int sourceOrder)
    {
        var score = 0;

        var desiredPostal = NormalizePostalCodeCandidate(desiredPostalCode);
        var candidatePostal = NormalizePostalCodeCandidate(candidatePostalCode);
        var desiredCityNormalized = NormalizeCityForCompare(desiredCity);
        var candidateCityNormalized = NormalizeCityForCompare(candidateCity);

        var postalExactMatch = desiredPostal.Length == 5 && candidatePostal.Length == 5 && string.Equals(candidatePostal, desiredPostal, StringComparison.OrdinalIgnoreCase);
        var postalExplicitMismatch = desiredPostal.Length == 5 && candidatePostal.Length == 5 && !postalExactMatch;

        if (postalExactMatch)
            score += 480;
        else if (postalExplicitMismatch)
            score -= 520;

        if (!string.IsNullOrWhiteSpace(desiredCityNormalized) && !string.IsNullOrWhiteSpace(candidateCityNormalized))
        {
            var cityExactMatch = string.Equals(candidateCityNormalized, desiredCityNormalized, StringComparison.OrdinalIgnoreCase);
            var citySoftMatch = candidateCityNormalized.Contains(desiredCityNormalized, StringComparison.OrdinalIgnoreCase)
                || desiredCityNormalized.Contains(candidateCityNormalized, StringComparison.OrdinalIgnoreCase);

            if (cityExactMatch)
                score += 220;
            else if (citySoftMatch)
                score += 90;
            else
                score -= 260;
        }

        if (CandidateMatchesHouseNumber(candidateHouseNumber, desiredHouseNumber))
            score += 1000;

        if (!string.IsNullOrWhiteSpace(candidateType) &&
            (candidateType.Equals("house", StringComparison.OrdinalIgnoreCase) ||
             candidateType.Equals("residential", StringComparison.OrdinalIgnoreCase) ||
             candidateType.Equals("building", StringComparison.OrdinalIgnoreCase)))
        {
            score += 40;
        }

        if (!string.IsNullOrWhiteSpace(desiredHouseNumber))
        {
            var desired = NormalizeHouseNumberForCompare(desiredHouseNumber);
            var candidateHouse = NormalizeHouseNumberForCompare(candidateHouseNumber);

            if (string.IsNullOrWhiteSpace(candidateHouse))
            {
                score -= 220;
            }
            else if (!string.Equals(candidateHouse, desired, StringComparison.OrdinalIgnoreCase))
            {
                score -= 260;
            }

            if (!string.IsNullOrWhiteSpace(candidateType) &&
                (candidateType.Equals("road", StringComparison.OrdinalIgnoreCase) ||
                 candidateType.Equals("pedestrian", StringComparison.OrdinalIgnoreCase)))
            {
                score -= 60;
            }
        }

        score -= Math.Max(0, sourceOrder);
        return score;
    }

    private static string RemoveDiacritics(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return string.Empty;

        var normalized = text.Normalize(NormalizationForm.FormD);
        var filtered = normalized.Where(ch => CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark).ToArray();
        return new string(filtered).Normalize(NormalizationForm.FormC);
    }
}

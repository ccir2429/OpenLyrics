using System.Globalization;
using System.Text;

namespace OpenLyrics.Services
{
    public static class TextSearch
    {
        public static string Normalize(string? value)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;
            var decomposed = value.Normalize(NormalizationForm.FormD);
            var sb = new StringBuilder(decomposed.Length);
            foreach (var c in decomposed)
            {
                if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                    sb.Append(char.ToLowerInvariant(c));
            }
            return sb.ToString();
        }
    }
}

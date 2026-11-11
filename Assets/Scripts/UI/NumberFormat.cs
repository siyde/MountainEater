using System.Globalization;

namespace MountainEater.UI
{
    /// <summary>
    /// Arayüz sayı biçimleri (Türkçe: binlik ayırıcı nokta, ondalık virgül).
    /// 10 milyondan büyük paralar kısaltılır: 12,5 Mn $ / 3,4 Mr $.
    /// </summary>
    public static class NumberFormat
    {
        private static readonly CultureInfo Culture = CultureInfo.GetCultureInfo("tr-TR");

        public static string Integer(long value) => value.ToString("N0", Culture);

        public static string Money(long value)
        {
            if (value >= 1_000_000_000_000L) return (value / 1e12).ToString("0.##", Culture) + " Tn $";
            if (value >= 1_000_000_000L) return (value / 1e9).ToString("0.##", Culture) + " Mr $";
            if (value >= 10_000_000L) return (value / 1e6).ToString("0.##", Culture) + " Mn $";
            return Integer(value) + " $";
        }

        /// <summary>15.27 → "%15,27"</summary>
        public static string Percent(double value) => "%" + value.ToString("0.00", Culture);

        /// <summary>Tam sayı değerleri ondalıksız, diğerlerini en fazla 2 ondalıkla yazar.</summary>
        public static string Value(float value)
        {
            return System.Math.Abs(value - System.Math.Round(value)) < 0.0001
                ? ((long)System.Math.Round(value)).ToString(Culture)
                : value.ToString("0.##", Culture);
        }
    }
}

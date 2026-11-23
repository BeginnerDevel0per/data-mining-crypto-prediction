using Microsoft.ML.Data;

namespace CryptoMovementAnalyzer.Api.Models
{
    // ML.NET'in okuyacağı Girdi (Input) Modeli
    public class CryptoModelInput
    {
        [LoadColumn(0)] public float Open { get; set; }
        [LoadColumn(1)] public float High { get; set; }
        [LoadColumn(2)] public float Low { get; set; }
        [LoadColumn(3)] public float Close { get; set; }
        [LoadColumn(4)] public float Volume { get; set; }

        // Hedef değişken ML.NET'te "Label" olarak adlandırılmalıdır (İkili sınıflandırma için bool kullanılır)
        [LoadColumn(5), ColumnName("Label")]
        public bool Label { get; set; }
    }
}

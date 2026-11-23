using Microsoft.ML.Data;

namespace CryptoMovementAnalyzer.Api.Models
{
    // ML.NET'in üreteceği Çıktı (Output) Modeli
    public class CryptoModelOutput
    {
        [ColumnName("PredictedLabel")]
        public bool Prediction { get; set; } // Tahmin (Yükselecek mi?)

        public float Probability { get; set; } // Olasılık (Örn: %75 ihtimalle yükselecek)
        public float Score { get; set; }
    }
}

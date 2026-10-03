namespace CryptoMovementAnalyzer.Api.Models
{
    public class CryptoData
    {
        public DateTime Timestamp { get; set; }
        public double Open { get; set; }
        public double High { get; set; }
        public double Low { get; set; }
        public double Close { get; set; }
        public double Volume { get; set; }

        // Hedef Değişkenimiz (Target): Yükseliş ise 1, Düşüş ise 0
        public float Target { get; set; }
    }
}

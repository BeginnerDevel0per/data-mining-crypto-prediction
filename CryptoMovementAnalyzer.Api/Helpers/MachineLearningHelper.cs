using CryptoMovementAnalyzer.Api.Models;
using Microsoft.ML;

namespace CryptoMovementAnalyzer.Api.Helpers
{
    /// <summary>
    /// Makine öğrenmesi işlemleri (Çapraz Doğrulama, Ölçekleme) için yardımcı metotları içeren statik sınıf.
    /// Controller'ı temiz tutmak ve görev ayrımını (Separation of Concerns) sağlamak için oluşturulmuştur.
    /// </summary>
    public static class MachineLearningHelper
    {
        /// <summary>
        /// Zaman serisi verileri için İleri Doğru Zincirleme (Forward Chaining) yöntemiyle 5 katlı çapraz doğrulama yapar.
        /// Standart k-fold gibi veriyi rastgele karıştırmaz, tarih sırasını kesinlikle korur.
        /// </summary>
        /// <param name="mlContext">ML.NET Context nesnesi</param>
        /// <param name="trainDataList">Eğitim verisi seti</param>
        /// <param name="algorithm">Kullanılacak algoritma ("RandomForest" veya "XGBoost")</param>
        /// <returns>5 katmanın Macro F1 skorlarının ortalaması</returns>
        public static (double Accuracy, double Precision, double Recall, double F1) RunTimeSeriesCrossValidation(MLContext mlContext, List<CryptoModelInput> trainDataList, string algorithm)
        {
            int k = 5;
            int foldSize = trainDataList.Count / (k + 1);

            // Tüm metriklerin toplamını tutacağımız değişkenler
            double totalAcc = 0, totalPrec = 0, totalRec = 0, totalF1 = 0;

            for (int i = 1; i <= k; i++)
            {
                var currentTrain = trainDataList.Take(i * foldSize).ToList();
                var currentVal = trainDataList.Skip(i * foldSize).Take(foldSize).ToList();

                var trainData = mlContext.Data.LoadFromEnumerable(currentTrain);
                var valData = mlContext.Data.LoadFromEnumerable(currentVal);

                var pipeline = mlContext.Transforms.Concatenate("Features", "Open", "High", "Low", "Close", "Volume");

                IEstimator<ITransformer> trainer = algorithm == "RandomForest"
                    ? pipeline.Append(mlContext.BinaryClassification.Trainers.FastForest(labelColumnName: "Label", featureColumnName: "Features"))
                    : pipeline.Append(mlContext.BinaryClassification.Trainers.LightGbm(labelColumnName: "Label", featureColumnName: "Features"));

                var model = trainer.Fit(trainData);
                var predictions = model.Transform(valData);
                var metrics = mlContext.BinaryClassification.EvaluateNonCalibrated(predictions, labelColumnName: "Label");

                // Metrikleri topla (NaN hatalarına karşı korumalı)
                totalAcc += double.IsNaN(metrics.Accuracy) ? 0 : metrics.Accuracy;
                totalPrec += double.IsNaN(metrics.PositivePrecision) ? 0 : metrics.PositivePrecision;
                totalRec += double.IsNaN(metrics.PositiveRecall) ? 0 : metrics.PositiveRecall;
                totalF1 += double.IsNaN(metrics.F1Score) ? 0 : metrics.F1Score;
            }

            // K adet sonucun ortalamasını bir Tuple (çoklu değer) olarak döndür
            return (totalAcc / k, totalPrec / k, totalRec / k, totalF1 / k);
        }

        /// <summary>
        /// Kripto piyasasındaki ani hacim/fiyat patlamalarının (aykırı değerlerin) modelleri yanıltmasını önlemek için
        /// Ortalama yerine Medyan ve IQR kullanan RobustScaler uygular.
        /// </summary>
        /// <param name="trainData">Eğitim verisi</param>
        /// <param name="testData">Test verisi</param>
        public static void ApplyRobustScaler(List<CryptoModelInput> trainData, List<CryptoModelInput> testData)
        {
            // DİKKAT (Veri Sızıntısı Önlemi): Medyan ve Çeyrekler Açıklığı (IQR) YALNIZCA Eğitim (Train) verisinden öğrenilir.
            // Test verisi geleceği temsil ettiği için istatistikleri çıkarılırken hesaba katılmaz.
            var openStats = GetRobustStats(trainData.Select(x => (double)x.Open));
            var highStats = GetRobustStats(trainData.Select(x => (double)x.High));
            var lowStats = GetRobustStats(trainData.Select(x => (double)x.Low));
            var closeStats = GetRobustStats(trainData.Select(x => (double)x.Close));
            var volumeStats = GetRobustStats(trainData.Select(x => (double)x.Volume));

            // Öğrenilen bu değerler hem eğitim hem de test verisine uygulanır
            TransformRobust(trainData, openStats, highStats, lowStats, closeStats, volumeStats);
            TransformRobust(testData, openStats, highStats, lowStats, closeStats, volumeStats);
        }

        /// <summary>
        /// Verilen liste üzerindeki her bir satıra RobustScaler matematiksel formülünü uygular.
        /// Formül: (Değer - Medyan) / IQR
        /// </summary>
        private static void TransformRobust(List<CryptoModelInput> data,
            (double Median, double IQR) open, (double Median, double IQR) high,
            (double Median, double IQR) low, (double Median, double IQR) close, (double Median, double IQR) volume)
        {
            foreach (var item in data)
            {
                item.Open = (float)((item.Open - open.Median) / open.IQR);
                item.High = (float)((item.High - high.Median) / high.IQR);
                item.Low = (float)((item.Low - low.Median) / low.IQR);
                item.Close = (float)((item.Close - close.Median) / close.IQR);
                item.Volume = (float)((item.Volume - volume.Median) / volume.IQR);
            }
        }

        /// <summary>
        /// Bir sayı dizisi üzerinden Medyan ve Çeyrekler Açıklığı (IQR) değerlerini hesaplar.
        /// </summary>
        private static (double Median, double IQR) GetRobustStats(IEnumerable<double> data)
        {
            var sorted = data.OrderBy(x => x).ToList();

            // %25 (Q1), %50 (Medyan) ve %75 (Q3) çeyreklik dilimlerini hesapla
            double q1 = StatisticsHelper.CalculatePercentile(sorted, 0.25);
            double median = StatisticsHelper.CalculatePercentile(sorted, 0.50);
            double q3 = StatisticsHelper.CalculatePercentile(sorted, 0.75);

            // Çeyrekler Açıklığı (Interquartile Range)
            double iqr = q3 - q1;

            // Eğer tüm değerler aynıysa IQR 0 çıkar. 
            // Bölme işleminde "Sıfıra Bölünme" (DivideByZero) hatasını engellemek için 0 yerine çok küçük bir sayı atanır.
            if (iqr == 0) iqr = 1e-10;

            return (median, iqr);
        }
    }
}
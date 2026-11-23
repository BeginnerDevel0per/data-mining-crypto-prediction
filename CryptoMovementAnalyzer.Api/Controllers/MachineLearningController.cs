using CryptoMovementAnalyzer.Api.Helpers;
using CryptoMovementAnalyzer.Api.Models;
using CryptoMovementAnalyzer.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.ML;
using Microsoft.ML.Data;
using ScottPlot.Statistics;

namespace CryptoMovementAnalyzer.Api.Controllers
{
    /// <summary>
    /// Makine öğrenmesi modellerinin eğitim, çapraz doğrulama, test ve değerlendirme 
    /// süreçlerini (Lojistik Regresyon, Random Forest, XGBoost) yöneten ana denetleyici.
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    public class MachineLearningController : ControllerBase
    {
        private readonly BinanceService _binanceService;
        private readonly MLContext _mlContext;

        public MachineLearningController(BinanceService binanceService)
        {
            _binanceService = binanceService;
            // Eğitim süreçlerinde rastgeleliği sabitlemek ve sonuçların her seferinde
            // aynı çıkmasını (reproducibility) sağlamak için seed değeri (42) verilir.
            _mlContext = new MLContext(seed: 42);
        }

        /// <summary>
        /// Basit bir Lojistik Regresyon modeli eğiterek başlangıç (baseline) metriklerini oluşturur.
        /// Doğrusal modellerin kripto piyasasındaki karmaşık yapıları çözemediğini göstermek amacıyla kullanılır.
        /// (Not: Rapor 2, Tablo 2'deki LR karşılaştırması için bu metodun kalması zorunludur.)
        /// </summary>
        /// <returns>Model başarı metrikleri (JSON formatında)</returns>
        [HttpGet("train-logistic-regression")]
        public async Task<IActionResult> TrainLogisticRegression()
        {
            // 1. Veriyi Çekiyoruz (10.000 Saatlik)
            var rawData = await _binanceService.GetBtcHourlyDataAsync(10000);

            // 2. DATA LEAKAGE (VERİ SIZINTISI) ÇÖZÜMÜ: Hedef Değişkeni Bir Sonraki Saate Kaydırma
            var preparedData = new List<CryptoModelInput>();
            for (int i = 0; i < rawData.Count - 1; i++)
            {
                // Bir sonraki saatin (i+1) kapanışı, açılışından büyükse Yükseliş (true) olarak etiketlenir
                bool isNextHourUp = rawData[i + 1].Close > rawData[i + 1].Open;

                preparedData.Add(new CryptoModelInput
                {
                    Open = (float)rawData[i].Open,
                    High = (float)rawData[i].High,
                    Low = (float)rawData[i].Low,
                    Close = (float)rawData[i].Close,
                    Volume = (float)rawData[i].Volume,
                    Label = isNextHourUp // HOCANIN İSTEDİĞİ DATA LEAKAGE DÜZELTMESİ
                });
            }

            // 3. EĞİTİM VE TEST VERİSİNİ ZAMAN SIRALI BÖLME (%80 Eğitim - %20 Test)
            int trainSize = (int)(preparedData.Count * 0.80);
            var trainDataList = preparedData.Take(trainSize).ToList();
            var testDataList = preparedData.Skip(trainSize).ToList();

            // ML.NET'in anlayacağı IDataView formatına çeviriyoruz
            IDataView trainData = _mlContext.Data.LoadFromEnumerable(trainDataList);
            IDataView testData = _mlContext.Data.LoadFromEnumerable(testDataList);

            // 4. MODEL PİPELINE (Veri Ön İşleme + Algoritma)
            // Lojistik Regresyon için standart NormalizeMinMax kullanılır (Daha sonra RobustScaler ile karşılaştırmak için)
            var pipeline = _mlContext.Transforms.Concatenate("Features", "Open", "High", "Low", "Close", "Volume")
                .Append(_mlContext.Transforms.NormalizeMinMax("Features"))
                .Append(_mlContext.BinaryClassification.Trainers.SdcaLogisticRegression(labelColumnName: "Label", featureColumnName: "Features"));

            // 5. MODELİ EĞİT (Sadece %80 Eğitim Verisiyle)
            var trainedModel = pipeline.Fit(trainData);

            // 6. MODELİ TEST ET (Hiç Görmediği %20 Test Verisiyle)
            var predictions = trainedModel.Transform(testData);
            var metrics = _mlContext.BinaryClassification.EvaluateNonCalibrated(predictions, labelColumnName: "Label");

            // 7. SONUÇLARI JSON OLARAK DÖNDÜR
            return Ok(new
            {
                TotalData = preparedData.Count,
                TrainSize = trainSize,
                TestSize = testDataList.Count,
                Algorithm = "Logistic Regression",
                Accuracy = Math.Round(metrics.Accuracy * 100, 2) + "%",
                F1Score = Math.Round(metrics.F1Score * 100, 2) + "%",
                PositivePrecision = Math.Round(metrics.PositivePrecision * 100, 2) + "%",
                PositiveRecall = Math.Round(metrics.PositiveRecall * 100, 2) + "%",
                AreaUnderRocCurve = Math.Round(metrics.AreaUnderRocCurve * 100, 2) + "%",
                AreaUnderPrecisionRecallCurve = Math.Round(metrics.AreaUnderPrecisionRecallCurve * 100, 2) + "%"
            });
        }

        /// <summary>
        /// İleri düzey veri ön işleme (RobustScaler) ve zaman serisi çapraz doğrulama (Forward Chaining) 
        /// tekniklerini kullanarak ağaç tabanlı kompleks algoritmaları (Random Forest ve XGBoost) eğitir.
        /// </summary>
        /// <returns>Çapraz doğrulama skorları ve en iyi modelin test verisindeki sonuçları</returns>
        [HttpGet("run-advanced-models")]
        public async Task<IActionResult> RunAdvancedModels()
        {
            // 1. VERİ HAZIRLIĞI VE SIZINTI ÇÖZÜMÜ
            var rawData = await _binanceService.GetBtcHourlyDataAsync(10000);
            var preparedData = new List<CryptoModelInput>();

            for (int i = 0; i < rawData.Count - 1; i++)
            {
                preparedData.Add(new CryptoModelInput
                {
                    Open = (float)rawData[i].Open,
                    High = (float)rawData[i].High,
                    Low = (float)rawData[i].Low,
                    Close = (float)rawData[i].Close,
                    Volume = (float)rawData[i].Volume,
                    Label = rawData[i + 1].Close > rawData[i + 1].Open // Hedef bir sonraki saate kaydırıldı
                });
            }

            // 2. EĞİTİM VE TEST AYRIMI (Zaman sırası bozulmadan, %80 - %20)
            int trainSize = (int)(preparedData.Count * 0.80);
            var trainDataList = preparedData.Take(trainSize).ToList();
            var testDataList = preparedData.Skip(trainSize).ToList();

            // 3. MANUEL ROBUST SCALER UYGULAMASI 
            // Aşırı uç değerleri bastırmak için yalnızca Eğitim verisinden öğrenilen medyan/IQR test verisine de uygulanır.
            MachineLearningHelper.ApplyRobustScaler(trainDataList, testDataList);

            // 4. ZAMAN SERİSİ 5 KATLI ÇAPRAZ DOĞRULAMA (Forward Chaining Time-Series CV)
            // ML Context dışarıdan Helper metoduna enjekte edilerek modellerin başarı durumları hesaplanır.
            var rfCv = MachineLearningHelper.RunTimeSeriesCrossValidation(_mlContext, trainDataList, "RandomForest");
            var xgbCv = MachineLearningHelper.RunTimeSeriesCrossValidation(_mlContext, trainDataList, "XGBoost");

            // 5. EN İYİ MODEL İLE FİNAL TESTİ (Tüm eğitim verisiyle eğit, Test verisinde sına)
            var finalTrainData = _mlContext.Data.LoadFromEnumerable(trainDataList);
            var finalTestData = _mlContext.Data.LoadFromEnumerable(testDataList);

            // RobustScaling manuel olarak C# listelerinde yapıldığı için pipeline'da ekstra Normalize işlemine gerek yoktur.
            var pipeline = _mlContext.Transforms.Concatenate("Features", "Open", "High", "Low", "Close", "Volume")
                .Append(_mlContext.BinaryClassification.Trainers.LightGbm(labelColumnName: "Label", featureColumnName: "Features")); // LightGBM (XGBoost algoritması)

            // Nihai modeli eğit
            var finalModel = pipeline.Fit(finalTrainData);
            // Daha önce modelin hiç görmediği (Future) test verisi üzerinde tahmin yaptır
            var predictions = finalModel.Transform(finalTestData);
            // Başarı metriklerini hesapla
            var metrics = _mlContext.BinaryClassification.EvaluateNonCalibrated(predictions, labelColumnName: "Label");

            // 6. SONUÇLARI DÖNDÜR (JSON kısmını güncelliyoruz)
            return Ok(new
            {
                DatasetInfo = new { Total = preparedData.Count, Train = trainSize, Test = testDataList.Count },

                // Cross Validation sonuçlarını tabloya aynen yazabileceğin formata getirdik
                CrossValidation_RandomForest = new
                {
                    Accuracy = Math.Round(rfCv.Accuracy * 100, 2) + "%",
                    Precision = Math.Round(rfCv.Precision * 100, 2) + "%",
                    Recall = Math.Round(rfCv.Recall * 100, 2) + "%",
                    MacroF1 = Math.Round(rfCv.F1 * 100, 2) + "%"
                },
                CrossValidation_XGBoost = new
                {
                    Accuracy = Math.Round(xgbCv.Accuracy * 100, 2) + "%",
                    Precision = Math.Round(xgbCv.Precision * 100, 2) + "%",
                    Recall = Math.Round(xgbCv.Recall * 100, 2) + "%",
                    MacroF1 = Math.Round(xgbCv.F1 * 100, 2) + "%"
                },

                FinalTestResults_XGBoost = new
                {
                    Accuracy = Math.Round(metrics.Accuracy * 100, 2) + "%",
                    MacroF1 = Math.Round(metrics.F1Score * 100, 2) + "%",
                    Precision = Math.Round(metrics.PositivePrecision * 100, 2) + "%",
                    Recall = Math.Round(metrics.PositiveRecall * 100, 2) + "%",
                    PRAUC = Math.Round(metrics.AreaUnderPrecisionRecallCurve * 100, 2) + "%",
                    ROCAUC = Math.Round(metrics.AreaUnderRocCurve * 100, 2) + "%"
                }
            });
        }
    }
}
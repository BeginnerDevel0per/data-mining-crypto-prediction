using CryptoMovementAnalyzer.Api.Helpers;
using CryptoMovementAnalyzer.Api.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ScottPlot;
using System.Text;

namespace CryptoMovementAnalyzer.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class DataController : ControllerBase
    {
        private readonly BinanceService _binanceService;

        public DataController(BinanceService binanceService)
        {
            _binanceService = binanceService;
        }

        [HttpGet("get-data")]
        public async Task<IActionResult> GetData()
        {
            var data = await _binanceService.GetBtcHourlyDataAsync(1000); // 1000 saatlik veri

            // Raporun "4. Veri Setinin Genel Özellikleri" kısmı için 
            // Satır sayısını (Count) ve Yükseliş/Düşüş sayısını kontrol etmek için basit bir anonim nesne dönüyoruz.
            int upCount = data.Count(x => x.Target == 1);
            int downCount = data.Count(x => x.Target == 0);

            return Ok(new
            {
                TotalRows = data.Count,
                UpCount = upCount,
                DownCount = downCount,
                Data = data // Bütün veri
            });
        }

        [HttpGet("get-statistics")]
        public async Task<IActionResult> GetStatistics()
        {
            var data = await _binanceService.GetBtcHourlyDataAsync(1000);

            if (data == null || data.Count == 0) return BadRequest("Veri bulunamadı.");

            // Hacim (Volume) ve Kapanış Fiyatı (Close) için istatistikleri hesaplıyoruz
            var volumes = data.Select(x => x.Volume).ToList();
            var closes = data.Select(x => x.Close).ToList();

            var stats = new
            {
                Close = new
                {
                    Mean = closes.Average(),
                    StdDev = StatisticsHelper.CalculateStandardDeviation(closes),
                    Min = closes.Min(),
                    Q25 = StatisticsHelper.CalculatePercentile(closes, 0.25),
                    Median = StatisticsHelper.CalculatePercentile(closes, 0.50),
                    Q75 = StatisticsHelper.CalculatePercentile(closes, 0.75),
                    Max = closes.Max()
                },
                Volume = new
                {
                    Mean = volumes.Average(),
                    StdDev = StatisticsHelper.CalculateStandardDeviation(volumes),
                    Min = volumes.Min(),
                    Q25 = StatisticsHelper.CalculatePercentile(volumes, 0.25),
                    Median = StatisticsHelper.CalculatePercentile(volumes, 0.50),
                    Q75 = StatisticsHelper.CalculatePercentile(volumes, 0.75),
                    Max = volumes.Max()
                }
            };

            return Ok(stats);
        }

        [HttpGet("export-csv")]
        public async Task<IActionResult> ExportToCsv()
        {
            var data = await _binanceService.GetBtcHourlyDataAsync(1000);

            var builder = new StringBuilder();
            builder.AppendLine("Timestamp,Open,High,Low,Close,Volume,Target");

            foreach (var item in data)
            {
                // Excel'de sorun yaşamamak için tarih formatını ve sayıları düzenliyoruz
                builder.AppendLine($"{item.Timestamp:yyyy-MM-dd HH:mm:ss}," +
                                   $"{item.Open.ToString(System.Globalization.CultureInfo.InvariantCulture)}," +
                                   $"{item.High.ToString(System.Globalization.CultureInfo.InvariantCulture)}," +
                                   $"{item.Low.ToString(System.Globalization.CultureInfo.InvariantCulture)}," +
                                   $"{item.Close.ToString(System.Globalization.CultureInfo.InvariantCulture)}," +
                                   $"{item.Volume.ToString(System.Globalization.CultureInfo.InvariantCulture)}," +
                                   $"{item.Target}");
            }

            return File(Encoding.UTF8.GetBytes(builder.ToString()), "text/csv", "CryptoData.csv");
        }

        [HttpGet("get-histogram")]
        public async Task<IActionResult> GetHistogram()
        {
            var data = await _binanceService.GetBtcHourlyDataAsync(10000);

            if (data == null || data.Count == 0) return BadRequest("Veri bulunamadı.");

            // Sadece kapanış (Close) fiyatlarını bir diziye alıyoruz
            double[] closes = data.Select(x => x.Close).ToArray();

            // 800x500 boyutlarında yeni bir çizim alanı oluşturuyoruz
            var myPlot = new ScottPlot.Plot();

            // 1. Veriyi 50 gruba (bin) bölerek histogram istatistiğini oluşturuyoruz
            var hist = ScottPlot.Statistics.Histogram.WithBinCount(50, closes);

            // 2. Histogram istatistiğini bir çubuk grafiği (Bar Plot) olarak ekliyoruz
            var barPlot = myPlot.Add.Bars(hist.Bins, hist.Counts);

            // 3. Görseli rapor formatına uygun hale getirmek için renklendirme
            foreach (var bar in barPlot.Bars)
            {
                // Çubuk genişliklerini bin boyutuna göre ayarlıyoruz (aradaki boşlukları silmek için)
                bar.Size = hist.FirstBinSize;
                bar.FillColor = Colors.SteelBlue.WithOpacity(0.7);
                bar.LineColor = Colors.DarkBlue;
                bar.LineWidth = 1;
            }

            // Eksen isimleri ve başlık
            myPlot.Title("Kapanış Fiyatı (Close) Dağılımı");
            myPlot.XLabel("Fiyat (USDT)");
            myPlot.YLabel("Frekans (Görülme Sayısı)");

            // Grafiği belleğe PNG olarak kaydedip döndürüyoruz
            byte[] imageBytes = myPlot.GetImageBytes(800, 500, ScottPlot.ImageFormat.Png);

            return File(imageBytes, "image/png", "Close_Histogram.png");
        }
        [HttpGet("get-target-chart")]
        public async Task<IActionResult> GetTargetChart()
        {
            var data = await _binanceService.GetBtcHourlyDataAsync(10000); // 15.000 adet veri çekiliyor

            int upCount = data.Count(x => x.Target == 1);
            int downCount = data.Count(x => x.Target == 0);

            // KONSOLA YAZDIRMA KISMI (Buradan gerçek sayıları göreceğiz)
            Console.WriteLine(new string('-', 30));
            Console.WriteLine($"ÇEKİLEN TOPLAM VERİ: {data.Count}");
            Console.WriteLine($"YÜKSELİŞ (1) SAYISI: {upCount}");
            Console.WriteLine($"DÜŞÜŞ (0) SAYISI:   {downCount}");
            Console.WriteLine(new string('-', 30));

            var myPlot = new ScottPlot.Plot();

            // Çubukları değerlere göre ekliyoruz
            double[] values = { downCount, upCount };
            var bars = myPlot.Add.Bars(values);

            // Barlara renk veriyoruz (Düşüş için kırmızımsı, Yükseliş için yeşilimsi)
            bars.Bars[0].FillColor = Colors.IndianRed;
            bars.Bars[1].FillColor = Colors.SeaGreen;

            // Alt eksen isimlerini ayarlıyoruz
            double[] tickPositions = { 0, 1 };
            string[] tickLabels = { "Düşüş (0)", "Yükseliş (1)" };
            myPlot.Axes.Bottom.SetTicks(tickPositions, tickLabels);

            myPlot.Title("Hedef Değişken (Yön) Dağılımı");
            myPlot.YLabel("Saat Sayısı");

            byte[] imageBytes = myPlot.GetImageBytes(800, 500, ScottPlot.ImageFormat.Png);
            return File(imageBytes, "image/png", "Target_Chart.png");
        }

        [HttpGet("get-scatter-chart")]
        public async Task<IActionResult> GetScatterChart()
        {
            var data = await _binanceService.GetBtcHourlyDataAsync(10000);

            // X ekseni için Hacim, Y ekseni için Kapanış Fiyatı
            double[] volumes = data.Select(x => x.Volume).ToArray();
            double[] closes = data.Select(x => x.Close).ToArray();

            var myPlot = new ScottPlot.Plot();

            // Saçılım (Scatter) grafiğini ekliyoruz
            var scatter = myPlot.Add.Scatter(volumes, closes);

            // Noktaları birleştiren çizgileri kapatıp, sadece yuvarlak noktalar bırakıyoruz
            scatter.LineWidth = 0;
            scatter.MarkerSize = 4;

            // Noktaların üst üste bindiği yerleri görmek için şeffaf bir mavi renk veriyoruz
            scatter.MarkerColor = Colors.SteelBlue.WithOpacity(0.5);

            myPlot.Title("İşlem Hacmi ve Kapanış Fiyatı İlişkisi");
            myPlot.XLabel("İşlem Hacmi (Volume)");
            myPlot.YLabel("Kapanış Fiyatı (USDT)");

            byte[] imageBytes = myPlot.GetImageBytes(800, 500, ScottPlot.ImageFormat.Png);
            return File(imageBytes, "image/png", "Scatter_Chart.png");
        }


        // Pearson Korelasyonu hesaplama fonksiyonu
        private double CalculatePearson(double[] x, double[] y)
        {
            double xAvg = x.Average();
            double yAvg = y.Average();
            double sum1 = 0, sum2 = 0, sum3 = 0;

            for (int i = 0; i < x.Length; i++)
            {
                double xDiff = x[i] - xAvg;
                double yDiff = y[i] - yAvg;
                sum1 += xDiff * yDiff;
                sum2 += xDiff * xDiff;
                sum3 += yDiff * yDiff;
            }

            if (sum2 == 0 || sum3 == 0) return 0;
            return sum1 / Math.Sqrt(sum2 * sum3);
        }


        [HttpGet("get-heatmap")]
        public async Task<IActionResult> GetHeatmap()
        {
            var data = await _binanceService.GetBtcHourlyDataAsync(10000);

            // Değişkenlerimizi dizilere alıyoruz
            double[] open = data.Select(x => x.Open).ToArray();
            double[] high = data.Select(x => x.High).ToArray();
            double[] low = data.Select(x => x.Low).ToArray();
            double[] close = data.Select(x => x.Close).ToArray();
            double[] volume = data.Select(x => x.Volume).ToArray();
            double[] target = data.Select(x => (double)x.Target).ToArray(); // Target float olduğu için double'a çevirdik

            List<double[]> vars = new List<double[]> { open, high, low, close, volume, target };
            string[] labels = { "Open", "High", "Low", "Close", "Volume", "Target" };
            int n = vars.Count;

            // 6x6 Korelasyon Matrisini oluşturuyoruz
            double[,] matrix = new double[n, n];
            for (int i = 0; i < n; i++)
            {
                for (int j = 0; j < n; j++)
                {
                    matrix[i, j] = CalculatePearson(vars[i], vars[j]);
                }
            }

            var myPlot = new ScottPlot.Plot();

            // Matrisi ısı haritası (Heatmap) olarak grafiğe ekliyoruz
            var hm = myPlot.Add.Heatmap(matrix);

            // X ve Y eksenlerine değişken isimlerini (Open, Close vb.) yazdırıyoruz
            double[] tickPositions = { 0, 1, 2, 3, 4, 5 };
            myPlot.Axes.Bottom.SetTicks(tickPositions, labels);
            myPlot.Axes.Left.SetTicks(tickPositions, labels);

            myPlot.Title("Değişkenler Arası Korelasyon Matrisi (Isı Haritası)");

            byte[] imageBytes = myPlot.GetImageBytes(800, 600, ScottPlot.ImageFormat.Png);
            return File(imageBytes, "image/png", "Heatmap.png");
        }
    }
}

using CryptoMovementAnalyzer.Api.Models;
using System.Globalization;
using System.Text.Json;

namespace CryptoMovementAnalyzer.Api.Services
{
    public class BinanceService
    {
        private readonly HttpClient _httpClient;

        public BinanceService(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        // Limit maksimum 1000 olabilir. Rapor için 10000 saatlik veri gayet yeterlidir.
        public async Task<List<CryptoData>> GetBtcHourlyDataAsync(int targetCount = 10000)
        {
            var dataList = new List<CryptoData>();
            long? endTime = null;

            // Hedeflenen sayıya ulaşana kadar döngü ile geçmişe doğru istek atıyoruz
            while (dataList.Count < targetCount)
            {
                // Tek seferde çekilebilecek maksimum limit 1000'dir
                int limit = Math.Min(1000, targetCount - dataList.Count);

                string url = $"https://api.binance.com/api/v3/klines?symbol=BTCUSDT&interval=1h&limit={limit}";
                if (endTime.HasValue)
                {
                    url += $"&endTime={endTime.Value}";
                }

                var response = await _httpClient.GetStringAsync(url);
                var rawData = JsonSerializer.Deserialize<List<JsonElement>>(response);

                if (rawData == null || rawData.Count == 0) break;

                var batchData = new List<CryptoData>();

                foreach (var item in rawData)
                {
                    double open = double.Parse(item[1].GetString(), CultureInfo.InvariantCulture);
                    double close = double.Parse(item[4].GetString(), CultureInfo.InvariantCulture);

                    batchData.Add(new CryptoData
                    {
                        Timestamp = DateTimeOffset.FromUnixTimeMilliseconds(item[0].GetInt64()).DateTime,
                        Open = open,
                        High = double.Parse(item[2].GetString(), CultureInfo.InvariantCulture),
                        Low = double.Parse(item[3].GetString(), CultureInfo.InvariantCulture),
                        Close = close,
                        Volume = double.Parse(item[5].GetString(), CultureInfo.InvariantCulture),
                        Target = close > open ? 1f : 0f
                    });
                }

                // Yeni çekilen verileri ana listenin BAŞINA ekliyoruz (kronolojik sırayı korumak için)
                dataList.InsertRange(0, batchData);

                // Bir sonraki istek için endTime değerini, çektiğimiz en eski verinin zamanından 1 milisaniye öncesine ayarlıyoruz
                endTime = rawData[0][0].GetInt64() - 1;
            }

            return dataList;
        }
    }
}

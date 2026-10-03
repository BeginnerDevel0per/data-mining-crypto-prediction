namespace CryptoMovementAnalyzer.Api.Helpers
{
    public static class StatisticsHelper
    {
        public static double CalculateStandardDeviation(IEnumerable<double> values)
        {
            double avg = values.Average();
            double sumOfSquaresOfDifferences = values.Select(val => (val - avg) * (val - avg)).Sum();
            return Math.Sqrt(sumOfSquaresOfDifferences / values.Count());
        }

        public static double CalculatePercentile(IEnumerable<double> sequence, double percentile)
        {
            var elements = sequence.OrderBy(v => v).ToArray();
            int N = elements.Length;
            double n = (N - 1) * percentile + 1;

            if (n == 1d) return elements[0];
            else if (n == N) return elements[N - 1];
            else
            {
                int k = (int)n;
                double d = n - k;
                return elements[k - 1] + d * (elements[k] - elements[k - 1]);
            }
        }
    }
}

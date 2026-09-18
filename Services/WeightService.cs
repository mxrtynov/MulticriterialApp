using System.Linq;

namespace MulticriterialApp.Services
{
    public static class WeightService
    {
        // Метод ранга: ранг 1 — самый важный
        // w_i = (n - r_i + 1) / Σ(n - r_j + 1)
        public static double[] RankMethodWeights(double[] ranks)
        {
            int n = ranks.Length;
            var raw = ranks.Select(r => n - r + 1).ToArray();
            double sum = raw.Sum();
            return raw.Select(x => x / sum).ToArray();
        }

        public static double[] AverageWeights(double[] w1, double[] w2)
        {
            return w1.Zip(w2, (a, b) => (a + b) / 2.0).ToArray();
        }
    }
}
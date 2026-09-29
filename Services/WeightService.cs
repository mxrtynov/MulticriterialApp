using System.Linq;

namespace MulticriterialApp.Services
{
    public static class WeightService
    {

        public static double[] RankMethodWeights(double[] ranks)
        {
            int n = ranks.Length;
            var raw = ranks.Select(r => n - r + 1).ToArray();
            double sum = raw.Sum();
            return raw.Select(x => x / sum).ToArray();
        }

        public static double[] AverageWeights(double[] w1, double[] w2)
        {
            var result = new double[w1.Length];
            for (int i = 0; i < w1.Length; i++)
                result[i] = (w1[i] + w2[i]) / 2.0;
            return result;
        }
    }
}
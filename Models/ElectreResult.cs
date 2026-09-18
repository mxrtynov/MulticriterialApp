using System.Collections.Generic;

namespace MulticriterialApp.Models
{
    public class ElectreResult
    {
        public List<Alternative> ParetoSet { get; set; } = new();
        public double[] Weights { get; set; } = System.Array.Empty<double>();
        public double[,] Concordance { get; set; } = new double[0, 0];
        public double[,] Discordance { get; set; } = new double[0, 0];
        public List<string> DominancePairs { get; set; } = new();
        public List<(Alternative Alt, int Out, int In, int Score)> Ranking { get; set; } = new();
        public List<string> Best { get; set; } = new();
    }
}
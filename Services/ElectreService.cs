using System;
using System.Collections.Generic;
using System.Linq;
using MulticriterialApp.Models;

namespace MulticriterialApp.Services
{
   
    public static class ElectreService
    {
        public static ElectreRunResult Run(
            List<Criterion> criteria,
            List<Alternative> paretoAlts,
            double cThreshold,
            double dThreshold)
        {
            var result = new ElectreRunResult();
            int n = paretoAlts.Count;
            int m = criteria.Count;

            //нормализация оценок
            var norm = Normalize(paretoAlts, m);

            //матрицы согл. и несогл.
            var C = new double[n, n];
            var D = new double[n, n];
            BuildMatrices(criteria, norm, C, D);

            result.C = C;
            result.D = D;

            var outgoing = new int[n];
            var incoming = new int[n];

            for (int i = 0; i < n; i++)
            {
                for (int j = 0; j < n; j++)
                {
                    if (i == j) continue;
                    if (C[i, j] >= cThreshold && D[i, j] <= dThreshold)
                    {
                        outgoing[i]++; //a лучше б
                        incoming[j]++; //б хуже а

                        result.DominancePairs.Add(
                            $"{paretoAlts[i].Name} -> {paretoAlts[j].Name}   (C={C[i, j]:F3},  D={D[i, j]:F3})");
                    }
                }
            }

            for (int i = 0; i < n; i++)
                result.Ranking.Add((paretoAlts[i], outgoing[i], incoming[i], outgoing[i] - incoming[i]));

            result.Ranking = result.Ranking.OrderByDescending(r => r.Score).ToList();

            if (result.Ranking.Count > 0)
            {
                int bestScore = result.Ranking[0].Score;
                result.Best = result.Ranking
                    .Where(r => r.Score == bestScore)
                    .Select(r => r.Alt.Name)
                    .ToList();
            }

            return result;
        }

        private static double[,] Normalize(List<Alternative> alts, int m)
        {
            int n = alts.Count;
            var res = new double[n, m];
            for (int j = 0; j < m; j++)
            {
                double sumSq = 0;
                for (int i = 0; i < n; i++) 
                    sumSq += alts[i].Scores[j] * alts[i].Scores[j];

                double denom = Math.Sqrt(sumSq);

                for (int i = 0; i < n; i++)
                    res[i, j] = denom > 0 ? alts[i].Scores[j] / denom : 0;
            }
            return res;
        }

        private static void BuildMatrices(
            List<Criterion> crit, double[,] norm, double[,] C, double[,] D)
        {
            int n = norm.GetLength(0);
            int m = norm.GetLength(1);

            var maxDiff = new double[m];
            for (int j = 0; j < m; j++)
            {
                double mx = 0;
                for (int i = 0; i < n; i++)
                    for (int k = 0; k < n; k++)
                    {
                        double diff = Math.Abs(norm[i, j] - norm[k, j]);
                        if (diff > mx) mx = diff;
                    }
                maxDiff[j] = mx;
            }


            for (int i = 0; i < n; i++)
            {
                for (int k = 0; k < n; k++)
                {
                    if (i == k) { C[i, k] = 1; D[i, k] = 0; continue; }

                    double conc = 0, disc = 0;
                    for (int j = 0; j < m; j++)
                    {
                        double vi = norm[i, j], vk = norm[k, j];

                        //С
                        bool iNotWorse = crit[j].Type == CriterionType.Max ? vi >= vk : vi <= vk;
                        if (iNotWorse) conc += crit[j].Weight;

                        //D
                        bool kWorseForI = crit[j].Type == CriterionType.Max ? vk > vi : vk < vi;
                        if (kWorseForI && maxDiff[j] > 0)
                        {
                            double d = Math.Abs(vk - vi) / maxDiff[j];
                            if (d > disc) disc = d;
                        }
                    }
                    C[i, k] = conc;
                    D[i, k] = disc;
                }
            }
        }
    }


    public class ElectreRunResult
    {
        public double[,] C { get; set; } = new double[0, 0];
        public double[,] D { get; set; } = new double[0, 0];
        public List<string> DominancePairs { get; set; } = new();
        public List<(Alternative Alt, int Out, int In, int Score)> Ranking { get; set; } = new();
        public List<string> Best { get; set; } = new();
    }
}
using System.Collections.Generic;
using MulticriterialApp.Models;

namespace MulticriterialApp.Services
{
    public static class ParetoService
    {
        public static List<Alternative> GetParetoSet(List<Criterion> crit, List<Alternative> alts)
        {
            var pareto = new List<Alternative>(alts);

            foreach (var a in alts)
                foreach (var b in alts)
                {
                    if (ReferenceEquals(a, b)) continue;
                    if (Dominates(a, b, crit))
                        pareto.Remove(b);
                }

            return pareto;
        }

        private static bool Dominates(Alternative a, Alternative b, List<Criterion> crit)
        {
            bool atLeastOneBetter = false;
            for (int i = 0; i < crit.Count; i++)
            {
                double va = a.Scores[i], vb = b.Scores[i];
                bool aBetter = crit[i].Type == CriterionType.Max ? va > vb : va < vb;
                bool aWorse = crit[i].Type == CriterionType.Max ? va < vb : va > vb;
                if (aWorse) return false;
                if (aBetter) atLeastOneBetter = true;
            }
            return atLeastOneBetter;
        }
    }
}
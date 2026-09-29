using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Data;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Windows.Input;
using MulticriterialApp.Models;
using MulticriterialApp.Services;

namespace MulticriterialApp.ViewModels
{
    public class MainViewModel : INotifyPropertyChanged
    {
        private const double DefaultC = 0.48;
        private const double DefaultD = 0.62;

        public ObservableCollection<Criterion> Criteria { get; } = new();
        public ObservableCollection<Alternative> Alternatives { get; } = new();
        public ObservableCollection<Alternative> Pareto { get; } = new();
        public ObservableCollection<string> DominancePairs { get; } = new();

        public double[] W1 { get; private set; } = Array.Empty<double>();
        public double[] W2 { get; private set; } = Array.Empty<double>();
        public double[] W { get; private set; } = Array.Empty<double>();

        private ElectreRunResult? _lastResult;
        public ElectreRunResult? LastResult
        {
            get => _lastResult;
            private set { _lastResult = value; OnPropertyChanged(); }
        }

        private double _c = DefaultC;
        public double C
        {
            get => _c;
            set { if (_c != value) { _c = value; OnPropertyChanged(); OnPropertyChanged(nameof(TxtC)); Recalculate(); } }
        }

        private double _d = DefaultD;
        public double D
        {
            get => _d;
            set { if (_d != value) { _d = value; OnPropertyChanged(); OnPropertyChanged(nameof(TxtD)); Recalculate(); } }
        }

        public string TxtC => C.ToString("F2");
        public string TxtD => D.ToString("F2");

        private DataView? _altsView;
        public DataView? AltsView { get => _altsView; private set { _altsView = value; OnPropertyChanged(); } }

        private DataView? _weightsView;
        public DataView? WeightsView { get => _weightsView; private set { _weightsView = value; OnPropertyChanged(); } }

        private DataView? _matrixCView;
        public DataView? MatrixCView { get => _matrixCView; private set { _matrixCView = value; OnPropertyChanged(); } }

        private DataView? _matrixDView;
        public DataView? MatrixDView { get => _matrixDView; private set { _matrixDView = value; OnPropertyChanged(); } }

        private DataView? _rankingView;
        public DataView? RankingView { get => _rankingView; private set { _rankingView = value; OnPropertyChanged(); } }

        private string _bestText = "";
        public string BestText { get => _bestText; private set { _bestText = value; OnPropertyChanged(); } }

        private string _hintText = "";
        public string HintText { get => _hintText; private set { _hintText = value; OnPropertyChanged(); } }

        private string _interpretation = "";
        public string Interpretation { get => _interpretation; private set { _interpretation = value; OnPropertyChanged(); } }

        public event Action? GraphInvalidated;
        public event Action<string>? ExploreRequested;

        public ICommand ResetCommand { get; }
        public ICommand ExploreCommand { get; }

        public MainViewModel()
        {
            ResetCommand = new RelayCommand(_ => { C = DefaultC; D = DefaultD; });
            ExploreCommand = new RelayCommand(_ => ExploreRequested?.Invoke(BuildExploreText()));
            BuildData();
            Recalculate();
        }

        private void BuildData()
        {
            Criteria.Add(new Criterion("K1 Условия доставки", CriterionType.Max));
            Criteria.Add(new Criterion("K2 Затраты на подготовку", CriterionType.Min));
            Criteria.Add(new Criterion("K3 Опасность загрязнения", CriterionType.Min));

            Alternatives.Add(new Alternative("П1", 3.0, 3.5, 2.0));
            Alternatives.Add(new Alternative("П2", 4.0, 1.8, 3.0));
            Alternatives.Add(new Alternative("П3", 2.0, 4.0, 1.0));
            Alternatives.Add(new Alternative("П4", 2.5, 3.0, 2.0));
            Alternatives.Add(new Alternative("П5", 2.0, 3.5, 1.0));
            Alternatives.Add(new Alternative("П6", 5.0, 4.0, 1.0));

            var r1 = new double[] { 3.0, 2.0, 1.0 };
            var r2 = new double[] { 3.0, 1.5, 1.5 };
            W1 = WeightService.RankMethodWeights(r1);
            W2 = WeightService.RankMethodWeights(r2);
            W = WeightService.AverageWeights(W1, W2);

            for (int i = 0; i < Criteria.Count; i++)
                Criteria[i].Weight = W[i];

            BuildAlternativesView();
            BuildWeightsView();
        }

        private void BuildAlternativesView()
        {
            var dt = new DataTable();
            dt.Columns.Add("Альтернатива", typeof(string));
            foreach (var c in Criteria) dt.Columns.Add(c.Name, typeof(double));

            foreach (var a in Alternatives)
            {
                var row = dt.NewRow();
                row[0] = a.Name;
                for (int i = 0; i < Criteria.Count; i++)
                    row[i + 1] = Math.Round(a.Scores[i], 2);
                dt.Rows.Add(row);
            }
            AltsView = dt.DefaultView;
        }

        private void BuildWeightsView()
        {
            var dt = new DataTable();
            dt.Columns.Add("Критерий", typeof(string));
            dt.Columns.Add("Тип", typeof(string));
            dt.Columns.Add("Эксперт 1", typeof(string));
            dt.Columns.Add("Эксперт 2", typeof(string));
            dt.Columns.Add("Итоговый вес", typeof(string));

            for (int i = 0; i < Criteria.Count; i++)
            {
                var row = dt.NewRow();
                row[0] = Criteria[i].Name;
                row[1] = Criteria[i].Type == CriterionType.Max ? "max" : "min";
                row[2] = W1[i].ToString("F4");
                row[3] = W2[i].ToString("F4");
                row[4] = W[i].ToString("F4");
                dt.Rows.Add(row);
            }
            WeightsView = dt.DefaultView;
        }

        public void Recalculate()
        {
            var paretoList = ParetoService.GetParetoSet(Criteria.ToList(), Alternatives.ToList());

            Pareto.Clear();
            foreach (var p in paretoList) Pareto.Add(p);

            var result = ElectreService.Run(Criteria.ToList(), paretoList, C, D);
            LastResult = result;

            MatrixCView = BuildMatrixView(result.C, paretoList).DefaultView;
            MatrixDView = BuildMatrixView(result.D, paretoList).DefaultView;

            DominancePairs.Clear();
            if (result.DominancePairs.Count == 0)
            {
                DominancePairs.Add("(нет пар превосходства при данных порогах)");
                HintText = "Граф пуст. Снизьте c* или увеличьте d*, чтобы появились отношения превосходства.";
            }
            else
            {
                HintText = "";
                foreach (var s in result.DominancePairs) DominancePairs.Add(s);
            }

            var sumC = ComputeSumConcordance(result.C, paretoList);
            var ordered = result.Ranking
                .OrderByDescending(r => r.Score)
                .ThenByDescending(r => sumC[r.Alt.Name])
                .ToList();

            var dtR = new DataTable();
            dtR.Columns.Add("Альтернатива", typeof(string));
            dtR.Columns.Add("Исходящих", typeof(int));
            dtR.Columns.Add("Входящих", typeof(int));
            dtR.Columns.Add("Разница (out-in)", typeof(int));
            dtR.Columns.Add("Сумма C(a,*)", typeof(string));

            foreach (var r in ordered)
            {
                var row = dtR.NewRow();
                row[0] = r.Alt.Name;
                row[1] = r.Out;
                row[2] = r.In;
                row[3] = r.Score;
                row[4] = sumC[r.Alt.Name].ToString("F3");
                dtR.Rows.Add(row);
            }
            RankingView = dtR.DefaultView;

            string bestNames;
            if (result.Best.Count == paretoList.Count && result.Best.Count > 1)
                bestNames = ordered.First().Alt.Name + "  (единоличного лидера нет — выбран по сумме индексов согласия)";
            else
                bestNames = string.Join(", ", result.Best);

            BestText = bestNames + "   (Парето-множество: " + string.Join(", ", paretoList.Select(p => p.Name)) + ")";
            Interpretation = BuildInterpretation();
            GraphInvalidated?.Invoke();
        }

        public string BuildExploreText()
        {
            var sb = new StringBuilder();
            sb.AppendLine("Подбор порогов (c* — согласие, d* — несогласие):");
            sb.AppendLine();
            sb.AppendLine($"{"c*",-6}{"d*",-6}{"Кол-во",-8}Лучшие");
            sb.AppendLine(new string('-', 60));

            var paretoList = Pareto.ToList();
            for (double c = 0.40; c <= 0.951; c += 0.05)
            {
                for (double d = 0.10; d <= 0.901; d += 0.10)
                {
                    var res = ElectreService.Run(Criteria.ToList(), paretoList, c, d);
                    if (res.Ranking.Count == 0) continue;
                    int bestScore = res.Ranking[0].Score;
                    var best = res.Ranking.Where(r => r.Score == bestScore).Select(r => r.Alt.Name).ToList();
                    if (best.Count >= 1 && best.Count <= 2)
                        sb.AppendLine($"{c,-6:F2}{d,-6:F2}{best.Count,-8}{string.Join(", ", best)}");
                }
            }
            return sb.ToString();
        }

        public string BuildReport()
        {
            var sb = new StringBuilder();
            string line = new string('=', 70);

            sb.AppendLine(line);
            sb.AppendLine("Задание 8. Выбор площадки под химическое предприятие");
            sb.AppendLine("Метод: Парето → веса критериев (метод ранга) → ЭЛЕКТРА I");
            sb.AppendLine($"Дата: {DateTime.Now:dd.MM.yyyy HH:mm:ss}");
            sb.AppendLine(line);
            sb.AppendLine();

            sb.AppendLine("ИСХОДНЫЕ ДАННЫЕ");
            sb.AppendLine(new string('-', 70));
            sb.Append("Альтернатива".PadRight(16));
            foreach (var c in Criteria) sb.Append(c.Name.PadRight(26));
            sb.AppendLine();
            foreach (var a in Alternatives)
            {
                sb.Append(a.Name.PadRight(16));
                for (int i = 0; i < Criteria.Count; i++)
                    sb.Append(a.Scores[i].ToString("F2").PadRight(26));
                sb.AppendLine();
            }
            sb.AppendLine();

            sb.AppendLine("ВЕСА КРИТЕРИЕВ (МЕТОД РАНГА)");
            sb.AppendLine(new string('-', 70));
            sb.AppendLine($"{"Критерий",-30}{"Тип",-8}{"Эксперт 1",-12}{"Эксперт 2",-12}{"Итог",-10}");
            for (int i = 0; i < Criteria.Count; i++)
            {
                string type = Criteria[i].Type == CriterionType.Max ? "max" : "min";
                sb.AppendLine($"{Criteria[i].Name,-30}{type,-8}{W1[i],-12:F4}{W2[i],-12:F4}{W[i],-10:F4}");
            }
            sb.AppendLine();

            if (LastResult == null)
            {
                sb.AppendLine("Результаты ЭЛЕКТРА отсутствуют.");
                return sb.ToString();
            }

            sb.AppendLine("МНОЖЕСТВО ПАРЕТО");
            sb.AppendLine(new string('-', 70));
            sb.AppendLine(string.Join(", ", Pareto.Select(p => p.Name)));
            sb.AppendLine();

            sb.AppendLine($"ПОРОГОВЫЕ ЗНАЧЕНИЯ:  c* = {C:F2},  d* = {D:F2}");
            sb.AppendLine();

            sb.AppendLine("МАТРИЦА ИНДЕКСОВ СОГЛАСИЯ C(a,b)");
            sb.AppendLine(new string('-', 70));
            AppendMatrix(sb, LastResult.C, Pareto.ToList());
            sb.AppendLine();

            sb.AppendLine("МАТРИЦА ИНДЕКСОВ НЕСОГЛАСИЯ D(a,b)");
            sb.AppendLine(new string('-', 70));
            AppendMatrix(sb, LastResult.D, Pareto.ToList());
            sb.AppendLine();

            sb.AppendLine("ГРАФ ПРЕВОСХОДСТВА");
            sb.AppendLine(new string('-', 70));
            if (LastResult.DominancePairs.Count == 0)
                sb.AppendLine("(нет пар превосходства при данных порогах)");
            else
                foreach (var p in LastResult.DominancePairs) sb.AppendLine(p);
            sb.AppendLine();

            sb.AppendLine("РЕЙТИНГ АЛЬТЕРНАТИВ");
            sb.AppendLine(new string('-', 70));
            sb.AppendLine($"{"Альтернатива",-16}{"Исходящих",-12}{"Входящих",-12}{"out-in",-8}");
            foreach (var r in LastResult.Ranking)
                sb.AppendLine($"{r.Alt.Name,-16}{r.Out,-12}{r.In,-12}{r.Score,-8}");
            sb.AppendLine();

            sb.AppendLine("ПОДБОР ПОРОГОВ ДЛЯ 1–2 ЛУЧШИХ АЛЬТЕРНАТИВ");
            sb.AppendLine(new string('-', 70));
            sb.AppendLine($"{"c*",-6}{"d*",-6}{"Кол-во",-8}Лучшие");
            var paretoList = Pareto.ToList();
            for (double c = 0.40; c <= 0.951; c += 0.05)
            {
                for (double d = 0.10; d <= 0.901; d += 0.10)
                {
                    var res = ElectreService.Run(Criteria.ToList(), paretoList, c, d);
                    if (res.Ranking.Count == 0) continue;
                    int bestScore = res.Ranking[0].Score;
                    var best = res.Ranking.Where(x => x.Score == bestScore).Select(x => x.Alt.Name).ToList();
                    if (best.Count >= 1 && best.Count <= 2)
                        sb.AppendLine($"{c,-6:F2}{d,-6:F2}{best.Count,-8}{string.Join(", ", best)}");
                }
            }
            sb.AppendLine();

            sb.AppendLine("РЕЗУЛЬТАТ");
            sb.AppendLine(new string('-', 70));
            sb.AppendLine("Лучшая альтернатива(ы): " + BestText);
            sb.AppendLine();

            sb.AppendLine("ИНТЕРПРЕТАЦИЯ");
            sb.AppendLine(new string('-', 70));
            sb.AppendLine(Interpretation);
            sb.AppendLine();
            sb.AppendLine(line);

            return sb.ToString();
        }

        private string BuildInterpretation()
        {
            if (LastResult == null || LastResult.Ranking.Count == 0)
                return "Результат отсутствует.";

            int n = Pareto.Count;
            var sb = new StringBuilder();

            var winners = LastResult.Ranking
                .Where(r => r.Score == LastResult.Ranking[0].Score)
                .ToList();

            if (LastResult.Ranking[0].Score <= 0)
            {
                sb.Append("Ни одна пара не проходит пороги: граф превосходства пуст. ");
                sb.Append("Рекомендуется снизить c* или увеличить d*, чтобы появились отношения превосходства.");
                return sb.ToString();
            }

            if (winners.Count == 1)
            {
                var w = winners[0];
                sb.Append("Альтернатива ");
                sb.Append(w.Alt.Name);
                sb.Append(" — единоличный лидер с показателем (out − in) = ");
                sb.Append(w.Score);
                sb.Append(". Исходящих превосходств: ");
                sb.Append(w.Out);
                sb.Append(" из ");
                sb.Append(n - 1);
                sb.Append(", входящих: ");
                sb.Append(w.In);
                sb.Append(". ");

                var alt = Alternatives.First(a => a.Name == w.Alt.Name);
                sb.Append("Оценки: K1 = ");
                sb.Append(alt.Scores[0].ToString("F2"));
                sb.Append(", K2 = ");
                sb.Append(alt.Scores[1].ToString("F2"));
                sb.Append(", K3 = ");
                sb.Append(alt.Scores[2].ToString("F2"));
                sb.Append(". ");

                double share = (double)w.Out / (n - 1);
                if (share >= 0.6)
                    sb.Append("Данная площадка превосходит большинство остальных по совокупности критериев с учётом весов.");
                else if (share >= 0.3)
                    sb.Append("Данная площадка превосходит часть остальных, но не доминирует над всеми.");
                else
                    sb.Append("Преимущество незначительно: показатель out−in мал относительно числа альтернатив.");
            }
            else
            {
                sb.Append("Единоличного лидера нет: сразу ");
                sb.Append(winners.Count);
                sb.Append(" альтернативы имеют одинаковый максимальный показатель (out − in) = ");
                sb.Append(winners[0].Score);
                sb.Append(" — это ");
                sb.Append(string.Join(", ", winners.Select(x => x.Alt.Name)));
                sb.Append(". ");
                sb.Append("Рекомендуется ужесточить пороги (увеличить c* или уменьшить d*), ");
                sb.Append("чтобы выделить одну альтернативу, либо рассмотреть их как равноценные.");
            }

            return sb.ToString();
        }

        private Dictionary<string, double> ComputeSumConcordance(double[,] C, List<Alternative> alts)
        {
            var res = new Dictionary<string, double>();
            for (int i = 0; i < alts.Count; i++)
            {
                double s = 0;
                for (int j = 0; j < alts.Count; j++)
                    if (i != j) s += C[i, j];
                res[alts[i].Name] = s;
            }
            return res;
        }

        private DataTable BuildMatrixView(double[,] M, List<Alternative> alts)
        {
            var dt = new DataTable();
            dt.Columns.Add("a \\ b", typeof(string));
            foreach (var a in alts) dt.Columns.Add(a.Name, typeof(string));

            for (int i = 0; i < alts.Count; i++)
            {
                var row = dt.NewRow();
                row[0] = alts[i].Name;
                for (int j = 0; j < alts.Count; j++)
                    row[j + 1] = M[i, j].ToString("F3");
                dt.Rows.Add(row);
            }
            return dt;
        }

        private void AppendMatrix(StringBuilder sb, double[,] M, List<Alternative> alts)
        {
            sb.Append(" ".PadRight(8));
            foreach (var a in alts) sb.Append(a.Name.PadLeft(10));
            sb.AppendLine();

            for (int i = 0; i < alts.Count; i++)
            {
                sb.Append(alts[i].Name.PadRight(8));
                for (int j = 0; j < alts.Count; j++)
                    sb.Append(M[i, j].ToString("F3").PadLeft(10));
                sb.AppendLine();
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        private void OnPropertyChanged([CallerMemberName] string? name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    public class RelayCommand : ICommand
    {
        private readonly Action<object?> _execute;

        public RelayCommand(Action<object?> execute) => _execute = execute;

        public bool CanExecute(object? parameter) => true;
        public void Execute(object? parameter) => _execute(parameter);

        public event EventHandler? CanExecuteChanged
        {
            add => CommandManager.RequerySuggested += value;
            remove => CommandManager.RequerySuggested -= value;
        }
    }
}
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using MulticriterialApp.Models;
using MulticriterialApp.Services;

namespace MulticriterialApp
{
    public partial class MainWindow : Window
    {
        private const double DefaultC = 0.48;
        private const double DefaultD = 0.62;

        private List<Criterion> _criteria = new();
        private List<Alternative> _alternatives = new();
        private double[] _w1 = Array.Empty<double>();
        private double[] _w2 = Array.Empty<double>();
        private double[] _w = Array.Empty<double>();
        private List<Alternative> _pareto = new();
        private ElectreRunResult? _lastResult;

        public MainWindow()
        {
            InitializeComponent();
            BuildData();
            Recalculate();
        }

        private void BuildData()
        {
            _criteria = new List<Criterion>
            {
                new Criterion("K1 Условия доставки",      CriterionType.Max),
                new Criterion("K2 Затраты на подготовку", CriterionType.Min),
                new Criterion("K3 Опасность загрязнения", CriterionType.Min)
            };

            _alternatives = new List<Alternative>
            {
                new Alternative("П1", 3.0, 3.5, 2.0),
                new Alternative("П2", 4.0, 1.8, 3.0),
                new Alternative("П3", 2.0, 4.0, 1.0),
                new Alternative("П4", 2.5, 3.0, 2.0),
                new Alternative("П5", 2.0, 3.5, 1.0),
                new Alternative("П6", 5.0, 4.0, 1.0),
            };

            var r1 = new double[] { 3.0, 2.0, 1.0 };
            var r2 = new double[] { 3.0, 1.5, 1.5 };
            _w1 = WeightService.RankMethodWeights(r1);
            _w2 = WeightService.RankMethodWeights(r2);
            _w = WeightService.AverageWeights(_w1, _w2);

            for (int i = 0; i < _criteria.Count; i++)
                _criteria[i].Weight = _w[i];

            BuildAlternativesGrid();
            BuildWeightsGrid();
        }

        private void BuildAlternativesGrid()
        {
            var dt = new DataTable();
            dt.Columns.Add("Альтернатива", typeof(string));
            foreach (var c in _criteria)
                dt.Columns.Add(c.Name, typeof(double));

            foreach (var a in _alternatives)
            {
                var row = dt.NewRow();
                row[0] = a.Name;
                for (int i = 0; i < _criteria.Count; i++)
                    row[i + 1] = Math.Round(a.Scores[i], 2);
                dt.Rows.Add(row);
            }

            DataGridAlts.ItemsSource = dt.DefaultView;
        }

        private void BuildWeightsGrid()
        {
            var dt = new DataTable();
            dt.Columns.Add("Критерий", typeof(string));
            dt.Columns.Add("Тип", typeof(string));
            dt.Columns.Add("Эксперт 1", typeof(string));
            dt.Columns.Add("Эксперт 2", typeof(string));
            dt.Columns.Add("Итоговый вес", typeof(string));

            for (int i = 0; i < _criteria.Count; i++)
            {
                var row = dt.NewRow();
                row[0] = _criteria[i].Name;
                row[1] = _criteria[i].Type == CriterionType.Max ? "max" : "min";
                row[2] = _w1[i].ToString("F4");
                row[3] = _w2[i].ToString("F4");
                row[4] = _w[i].ToString("F4");
                dt.Rows.Add(row);
            }
            DataGridWeights.ItemsSource = dt.DefaultView;
        }

        private void Recalculate()
        {
            double c = SliderC.Value;
            double d = SliderD.Value;

            TxtC.Text = c.ToString("F2");
            TxtD.Text = d.ToString("F2");

            _pareto = ParetoService.GetParetoSet(_criteria, _alternatives);
            var result = ElectreService.Run(_criteria, _pareto, c, d);
            _lastResult = result;

            DataGridC.ItemsSource = BuildMatrixView(result.C, _pareto).DefaultView;
            DataGridD.ItemsSource = BuildMatrixView(result.D, _pareto).DefaultView;

            ListDominance.Items.Clear();
            if (result.DominancePairs.Count == 0)
            {
                ListDominance.Items.Add("(нет пар превосходства при данных порогах)");
                TxtHint.Text = "Граф пуст. Снизьте c* или увеличьте d*, " +
                               "чтобы появились отношения превосходства.";
            }
            else
            {
                TxtHint.Text = "";
                foreach (var s in result.DominancePairs) ListDominance.Items.Add(s);
            }

            var dtR = new DataTable();
            dtR.Columns.Add("Альтернатива", typeof(string));
            dtR.Columns.Add("Исходящих", typeof(int));
            dtR.Columns.Add("Входящих", typeof(int));
            dtR.Columns.Add("Разница (out-in)", typeof(int));
            dtR.Columns.Add("Сумма C(a,*)", typeof(string));

            var sumC = ComputeSumConcordance(result.C, _pareto);

            var ordered = result.Ranking
                .OrderByDescending(r => r.Score)
                .ThenByDescending(r => sumC[r.Alt.Name])
                .ToList();

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
            DataGridRanking.ItemsSource = dtR.DefaultView;

            string bestNames;
            if (result.Best.Count == _pareto.Count && result.Best.Count > 1)
            {
                bestNames = ordered.First().Alt.Name +
                            "  (единоличного лидера нет — выбран по сумме индексов согласия)";
            }
            else
            {
                bestNames = string.Join(", ", result.Best);
            }

            TxtBest.Text = bestNames +
                           "   (Парето-множество: " +
                           string.Join(", ", _pareto.Select(p => p.Name)) + ")";
        }

        private Dictionary<string, double> ComputeSumConcordance(
            double[,] C, List<Alternative> alts)
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

        private void SliderC_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (!IsLoaded) return;
            Recalculate();
        }

        private void SliderD_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (!IsLoaded) return;
            Recalculate();
        }

        private void BtnReset_Click(object sender, RoutedEventArgs e)
        {
            SliderC.Value = DefaultC;
            SliderD.Value = DefaultD;
            Recalculate();
        }

        private void BtnExport_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new SaveFileDialog
            {
                Title = "Сохранить результаты",
                Filter = "Текстовый файл (*.txt)|*.txt",
                FileName = $"electre_results_{DateTime.Now:yyyyMMdd_HHmmss}.txt"
            };

            if (dlg.ShowDialog() != true) return;

            try
            {
                File.WriteAllText(dlg.FileName, BuildReport(), new UTF8Encoding(true));
                MessageBox.Show($"Результаты сохранены:\n{dlg.FileName}",
                                "Экспорт завершён",
                                MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ошибка сохранения: " + ex.Message,
                                "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private string BuildReport()
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
            foreach (var c in _criteria) sb.Append(c.Name.PadRight(26));
            sb.AppendLine();
            foreach (var a in _alternatives)
            {
                sb.Append(a.Name.PadRight(16));
                for (int i = 0; i < _criteria.Count; i++)
                    sb.Append(a.Scores[i].ToString("F2").PadRight(26));
                sb.AppendLine();
            }
            sb.AppendLine();

            sb.AppendLine("ВЕСА КРИТЕРИЕВ (МЕТОД РАНГА)");
            sb.AppendLine(new string('-', 70));
            sb.AppendLine($"{"Критерий",-30}{"Тип",-8}{"Эксперт 1",-12}{"Эксперт 2",-12}{"Итог",-10}");
            for (int i = 0; i < _criteria.Count; i++)
            {
                string type = _criteria[i].Type == CriterionType.Max ? "max" : "min";
                sb.AppendLine($"{_criteria[i].Name,-30}{type,-8}" +
                              $"{_w1[i],-12:F4}{_w2[i],-12:F4}{_w[i],-10:F4}");
            }
            sb.AppendLine();

            if (_lastResult == null)
            {
                sb.AppendLine("Результаты ЭЛЕКТРА отсутствуют.");
                return sb.ToString();
            }

            sb.AppendLine("МНОЖЕСТВО ПАРЕТО");
            sb.AppendLine(new string('-', 70));
            sb.AppendLine(string.Join(", ", _pareto.Select(p => p.Name)));
            sb.AppendLine();

            sb.AppendLine($"ПОРОГОВЫЕ ЗНАЧЕНИЯ:  c* = {SliderC.Value:F2},  d* = {SliderD.Value:F2}");
            sb.AppendLine();

            sb.AppendLine("МАТРИЦА ИНДЕКСОВ СОГЛАСИЯ C(a,b)");
            sb.AppendLine(new string('-', 70));
            AppendMatrix(sb, _lastResult.C, _pareto);
            sb.AppendLine();

            sb.AppendLine("МАТРИЦА ИНДЕКСОВ НЕСОГЛАСИЯ D(a,b)");
            sb.AppendLine(new string('-', 70));
            AppendMatrix(sb, _lastResult.D, _pareto);
            sb.AppendLine();

            sb.AppendLine("ГРАФ ПРЕВОСХОДСТВА");
            sb.AppendLine(new string('-', 70));
            if (_lastResult.DominancePairs.Count == 0)
                sb.AppendLine("(нет пар превосходства при данных порогах)");
            else
                foreach (var p in _lastResult.DominancePairs) sb.AppendLine(p);
            sb.AppendLine();

            sb.AppendLine("РЕЙТИНГ АЛЬТЕРНАТИВ");
            sb.AppendLine(new string('-', 70));
            sb.AppendLine($"{"Альтернатива",-16}{"Исходящих",-12}{"Входящих",-12}{"out-in",-8}");
            foreach (var r in _lastResult.Ranking)
                sb.AppendLine($"{r.Alt.Name,-16}{r.Out,-12}{r.In,-12}{r.Score,-8}");
            sb.AppendLine();

            sb.AppendLine("ПОДБОР ПОРОГОВ ДЛЯ 1–2 ЛУЧШИХ АЛЬТЕРНАТИВ");
            sb.AppendLine(new string('-', 70));
            sb.AppendLine($"{"c*",-6}{"d*",-6}{"Кол-во",-8}Лучшие");
            for (double c = 0.40; c <= 0.951; c += 0.05)
            {
                for (double d = 0.10; d <= 0.901; d += 0.10)
                {
                    var res = ElectreService.Run(_criteria, _pareto, c, d);
                    if (res.Ranking.Count == 0) continue;
                    int bestScore = res.Ranking[0].Score;
                    var best = res.Ranking.Where(x => x.Score == bestScore)
                                          .Select(x => x.Alt.Name).ToList();
                    if (best.Count >= 1 && best.Count <= 2)
                        sb.AppendLine($"{c,-6:F2}{d,-6:F2}{best.Count,-8}{string.Join(", ", best)}");
                }
            }
            sb.AppendLine();

            sb.AppendLine("РЕЗУЛЬТАТ");
            sb.AppendLine(new string('-', 70));
            sb.AppendLine("Лучшая альтернатива(ы): " + TxtBest.Text);
            sb.AppendLine();

            sb.AppendLine("ИНТЕРПРЕТАЦИЯ");
            sb.AppendLine(new string('-', 70));
            sb.AppendLine(BuildInterpretation());
            sb.AppendLine();
            sb.AppendLine(line);

            return sb.ToString();
        }

        private string BuildInterpretation()
        {
            if (_lastResult == null || _lastResult.Ranking.Count == 0)
                return "Результат отсутствует.";

            int n = _pareto.Count;
            var sb = new StringBuilder();

            var winners = _lastResult.Ranking
                .Where(r => r.Score == _lastResult.Ranking[0].Score)
                .ToList();

            if (_lastResult.Ranking[0].Score <= 0)
            {
                sb.Append("Ни одна пара не проходит пороги: граф превосходства пуст. ");
                sb.Append("Рекомендуется снизить c* или увеличить d*, ");
                sb.Append("чтобы появились отношения превосходства.");
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

                var alt = _alternatives.First(a => a.Name == w.Alt.Name);
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

        private void BtnExplore_Click(object sender, RoutedEventArgs e)
        {
            var sb = new StringBuilder();
            sb.AppendLine("Подбор порогов (c* — согласие, d* — несогласие):");
            sb.AppendLine();
            sb.AppendLine($"{"c*",-6}{"d*",-6}{"Кол-во",-8}Лучшие");
            sb.AppendLine(new string('-', 60));

            for (double c = 0.40; c <= 0.951; c += 0.05)
            {
                for (double d = 0.10; d <= 0.901; d += 0.10)
                {
                    var res = ElectreService.Run(_criteria, _pareto, c, d);
                    if (res.Ranking.Count == 0) continue;
                    int bestScore = res.Ranking[0].Score;
                    var best = res.Ranking.Where(r => r.Score == bestScore)
                                          .Select(r => r.Alt.Name).ToList();
                    if (best.Count >= 1 && best.Count <= 2)
                        sb.AppendLine($"{c,-6:F2}{d,-6:F2}{best.Count,-8}{string.Join(", ", best)}");
                }
            }

            var win = new Window
            {
                Title = "Подбор порогов",
                Width = 720,
                Height = 620,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = this,
                Background = System.Windows.Media.Brushes.White,
                Content = new ScrollViewer
                {
                    Content = new TextBox
                    {
                        Text = sb.ToString(),
                        FontFamily = new System.Windows.Media.FontFamily("Consolas"),
                        FontSize = 12,
                        IsReadOnly = true,
                        TextWrapping = TextWrapping.NoWrap,
                        Padding = new Thickness(14),
                        BorderThickness = new Thickness(0),
                        VerticalScrollBarVisibility = ScrollBarVisibility.Auto
                    }
                }
            };
            win.ShowDialog();
        }
    }
}
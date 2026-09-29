using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using Microsoft.Win32;
using MulticriterialApp.ViewModels;

namespace MulticriterialApp
{
    public partial class MainWindow : Window
    {
        private MainViewModel Vm => (MainViewModel)DataContext;

        public MainWindow()
        {
            InitializeComponent();
            DataContext = new MainViewModel();
            Vm.GraphInvalidated += DrawGraph;
            Vm.ExploreRequested += ShowExploreWindow;
            Loaded += (_, _) => DrawGraph();
        }

        private void GraphCanvas_SizeChanged(object sender, SizeChangedEventArgs e) => DrawGraph();

        private void DrawGraph()
        {
            if (GraphCanvas == null) return;
            GraphCanvas.Children.Clear();

            var result = Vm.LastResult;
            var pareto = Vm.Pareto;
            if (result == null || pareto.Count == 0) return;

            double w = GraphCanvas.ActualWidth;
            double h = GraphCanvas.ActualHeight;
            if (w < 10 || h < 10) return;

            int n = pareto.Count;
            double cx = w / 2, cy = h / 2;
            double radius = Math.Min(w, h) * 0.35;
            double nodeR = 24;

            var pos = new Point[n];
            for (int i = 0; i < n; i++)
            {
                double angle = 2 * Math.PI * i / n - Math.PI / 2;
                pos[i] = new Point(cx + radius * Math.Cos(angle), cy + radius * Math.Sin(angle));
            }

            var edgeBrush = new SolidColorBrush(Color.FromRgb(0x64, 0x74, 0x8B));
            var nodeStroke = new SolidColorBrush(Color.FromRgb(0x25, 0x63, 0xEB));
            var textBrush = new SolidColorBrush(Color.FromRgb(0x0F, 0x17, 0x2A));

            var used = new HashSet<string>();
            foreach (var edge in result.DominancePairs)
            {
                var parts = edge.Split(new[] { "->" }, StringSplitOptions.None);
                if (parts.Length < 2) continue;
                string fromName = parts[0].Trim();
                string toName = parts[1].Trim().Split(' ')[0];

                int iFrom = -1, iTo = -1;
                for (int k = 0; k < pareto.Count; k++)
                {
                    if (pareto[k].Name == fromName) iFrom = k;
                    if (pareto[k].Name == toName) iTo = k;
                }
                if (iFrom < 0 || iTo < 0) continue;

                string key = iFrom + "->" + iTo;
                if (used.Contains(key)) continue;
                used.Add(key);

                DrawArrow(GraphCanvas, pos[iFrom], pos[iTo], nodeR, edgeBrush);
            }

            for (int i = 0; i < n; i++)
            {
                var ellipse = new Ellipse
                {
                    Width = nodeR * 2,
                    Height = nodeR * 2,
                    Fill = Brushes.White,
                    Stroke = nodeStroke,
                    StrokeThickness = 2
                };
                Canvas.SetLeft(ellipse, pos[i].X - nodeR);
                Canvas.SetTop(ellipse, pos[i].Y - nodeR);
                GraphCanvas.Children.Add(ellipse);

                var tb = new TextBlock
                {
                    Text = pareto[i].Name,
                    FontSize = 12,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = textBrush
                };
                tb.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                Canvas.SetLeft(tb, pos[i].X - tb.DesiredSize.Width / 2);
                Canvas.SetTop(tb, pos[i].Y - tb.DesiredSize.Height / 2);
                GraphCanvas.Children.Add(tb);
            }
        }

        private void DrawArrow(Canvas canvas, Point from, Point to, double nodeR, Brush brush)
        {
            double dx = to.X - from.X, dy = to.Y - from.Y;
            double len = Math.Sqrt(dx * dx + dy * dy);
            if (len < 1) return;

            double ux = dx / len, uy = dy / len;
            var start = new Point(from.X + ux * nodeR, from.Y + uy * nodeR);
            var end = new Point(to.X - ux * nodeR, to.Y - uy * nodeR);

            canvas.Children.Add(new Line
            {
                X1 = start.X,
                Y1 = start.Y,
                X2 = end.X,
                Y2 = end.Y,
                Stroke = brush,
                StrokeThickness = 1.5,
                StrokeEndLineCap = PenLineCap.Round
            });

            double arrowLen = 12, arrowAngle = Math.PI / 7;
            double angle = Math.Atan2(uy, ux);
            var p1 = new Point(end.X - arrowLen * Math.Cos(angle - arrowAngle),
                               end.Y - arrowLen * Math.Sin(angle - arrowAngle));
            var p2 = new Point(end.X - arrowLen * Math.Cos(angle + arrowAngle),
                               end.Y - arrowLen * Math.Sin(angle + arrowAngle));

            canvas.Children.Add(new Polygon
            {
                Points = new PointCollection { end, p1, p2 },
                Fill = brush
            });
        }

        private void ShowExploreWindow(string text)
        {
            var win = new Window
            {
                Title = "Подбор порогов",
                Width = 720,
                Height = 620,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = this,
                Background = Brushes.White,
                Content = new ScrollViewer
                {
                    Content = new TextBox
                    {
                        Text = text,
                        FontFamily = new FontFamily("Consolas"),
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
                File.WriteAllText(dlg.FileName, Vm.BuildReport(), new UTF8Encoding(true));
                MessageBox.Show($"Результаты сохранены:\n{dlg.FileName}", "Экспорт завершён",
                                MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ошибка сохранения: " + ex.Message, "Ошибка",
                                MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
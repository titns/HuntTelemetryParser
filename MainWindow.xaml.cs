using HuntTelemetry.Logic;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Printing;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Security.Principal;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace HuntTelemetry
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {

        private List<LevelLog> _logs;
        private string _telemetryDirectory = @"C:\temp\hunt map\telemetry\tt";
        private IEnumerable<string> _files;
        private string _telemetrySearchPattern = null;
        private int _maxFilesToLoad = default;
        private bool _showAll = false;


        WriteableBitmap bmp = null;
        List<int> shownIndex = new List<int>();

        public MainWindow()
        {
            InitializeComponent();
        }
        private void Window_Initialized(object sender, EventArgs e)
        {
            ResetMap();
        }

        private void ResetMap()
        {
            spawnMapImg.Source = new BitmapImage(
       new Uri(Environment.CurrentDirectory + @"\spawn_map.png"));
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            _files = String.IsNullOrEmpty(_telemetrySearchPattern) ? System.IO.Directory.GetFiles(_telemetryDirectory) : System.IO.Directory.GetFiles(_telemetryDirectory, _telemetrySearchPattern).AsEnumerable();
        }


        private void LoadFiles()
        {
            List<LevelLog> logs = new List<LevelLog>();

            var index = 0;
            var files = _maxFilesToLoad == default ? _files : _files.Take(_maxFilesToLoad);
            files.AsParallel().All(file =>
            {
                var result = TelemetryParser.ParseTelemtryFile(file);
                lock (logs)
                {
                    Interlocked.Increment(ref index);
                    if (index % 5 == 0)
                    {
                        this.Dispatcher.Invoke(() =>
                        {
                            textBox.Text = $"loaded {index}";
                        });
                    }
                    logs.AddRange(result);
                    this.Dispatcher.Invoke(() =>
                    {
                        var orderedLogs = logs.OrderBy(x => x.StartTime);
                        foreach(var log in orderedLogs)
                        {
                            tileListLbx.Items.Add(log);
                        }
                    });
                }
                return true;
            });

            _logs = logs;
        }



        private void ShowPaths(List<LevelLog> logs)
        {
            bmp = bmp ?? new WriteableBitmap((BitmapSource)spawnMapImg.Source);

            var props = new PathCalculatorProperties()
            {
                Image = new Int32Rect(0, 0, (int)spawnMapImg.Width, (int)spawnMapImg.Height)
            };
            var calc = new PathCalculator(props);
            var dots = calc.GetPaths(logs);

            foreach (var dotToPaint in dots) {
                PaintDot(bmp, dotToPaint);
            }
            spawnMapImg.Source = bmp;
            spawnMapImg.InvalidateVisual();
        }

        /// <summary>
        /// Paints a specified size dot on the map based on the 
        /// </summary>
        /// <param name="bmp"></param>
        /// <param name="valueToBePainted"></param>
        /// <param name="size"></param>
        /// <returns></returns>
        private void PaintDot(WriteableBitmap bmp, Dot dot)
        {
            var pixelArray = new byte[dot.Rectangle.Width * dot.Rectangle.Height * 4];
            for (int j = 0; j < pixelArray.Length; j += 4)
            {
                pixelArray[j] = dot.Color.B;
                pixelArray[j + 1] = dot.Color.G;
                pixelArray[j + 2] = dot.Color.R;
                pixelArray[j + 3] = 255;
            }
            
            bmp.WritePixels(dot.Rectangle, pixelArray, dot.Rectangle.Width * 4, 0);
        }



        private void nextBtn_Click(object sender, RoutedEventArgs e)
        {
            if (!_showAll)
            {
                    var maxIndex = 0;
                    if (shownIndex.Count > 0)
                    {
                        maxIndex = shownIndex.Max() + 1;
                    }
                    var logToShow = _logs[maxIndex];
                    ShowPaths(new List<LevelLog> { logToShow });
                shownIndex.Add(maxIndex);

            }
            else
            {
                ShowPaths(_logs);
            }
        }

        private void TelemtryDirPathTbx_TextChanged(object sender, TextChangedEventArgs e)
        {
            var text = (sender as TextBox).Text.Split(',');
            _telemetryDirectory = text[0];
            if (text.Length > 1)
            {
                _telemetrySearchPattern = text[1];
            }
            
            _files = String.IsNullOrEmpty(_telemetrySearchPattern) ? System.IO.Directory.GetFiles(_telemetryDirectory) : System.IO.Directory.GetFiles(_telemetryDirectory, _telemetrySearchPattern).AsEnumerable();
        }
        private void loadFilesBtn_Click(object sender, RoutedEventArgs e)
        {
            var fileLoadingTask = new Task(LoadFiles);
            fileLoadingTask.Start();
            fileLoadingTask.GetAwaiter().OnCompleted(() => { nextBtn.IsEnabled = true; });
        }
    }
}
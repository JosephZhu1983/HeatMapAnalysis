using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using Microsoft.Win32;
using System.IO;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Threading;
using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using MongoDB.Driver;
using MongoDB.Bson;
using System.Configuration;

namespace HeatMap.Analysis
{
    public partial class MainWindow : Window
    {
        private static string MongonDbConnString = ConfigurationManager.AppSettings["AnalyseCenterDB"];
        private readonly string fileSuffix = "_yyyyMMddhhmmss";
        public MainWindow()
        {
            InitializeComponent();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            this.SourceFilePath.Text = @"e:\5173.png";
            ChangeAlpha.Value = 100;
            ChangeRadius.Value = 10;
            ChangeIntensity.Value = 95;
            Directory.GetFiles(System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Palettes"), "*.png", SearchOption.AllDirectories).ToList().ForEach(file =>
                PaletteStyle.Items.Add(new ComboBoxItem() { Tag = file, Content = System.IO.Path.GetFileNameWithoutExtension(file) }));
            PaletteStyle.SelectedIndex = 0;
        }

        private void SourceFilePath_TextChanged(object sender, TextChangedEventArgs e)
        {
            ImagePreview.Source = new BitmapImage(new Uri(SourceFilePath.Text, UriKind.Absolute));
            string filePath = System.IO.Path.GetDirectoryName(SourceFilePath.Text);
            string fileName = System.IO.Path.GetFileNameWithoutExtension(SourceFilePath.Text);
            string fileExt = System.IO.Path.GetExtension(SourceFilePath.Text);
            OutputFilePath.Text = System.IO.Path.Combine(filePath, fileName + fileSuffix + fileExt);
        }

        private void SelectSourceFile_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog ofd = new OpenFileDialog();
            ofd.FileName = "";
            ofd.Filter = "图片|*.png;*.jpg;*.bmp|所有文件|*.*";
            if (ofd.ShowDialog().Value == true)
            {
                SourceFilePath.Text = ofd.FileName;
            }
        }

        private void ChangeRadius_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            RadiusValue.Content = ChangeRadius.Value;
        }

        private void ChangeIntensity_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            IntensityValue.Content = ChangeIntensity.Value;
        }

        private void PaletteStyle_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            string file = (PaletteStyle.SelectedItem as ComboBoxItem).Tag.ToString();
            PaletteStylePreview.Source = new BitmapImage(new Uri(file, UriKind.Absolute));

        }

        private void ChangeAlpha_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            AlphaValue.Content = ChangeAlpha.Value;
        }

        private void SelectOutputFile_Click(object sender, RoutedEventArgs e)
        {
            SaveFileDialog sfd = new SaveFileDialog();
            if (sfd.ShowDialog().Value == true)
            {
                OutputFilePath.Text = sfd.FileName;
            }
        }

        int combine = 0;
        private void Create_Click(object sender, RoutedEventArgs e)
        {
            var sourceFilePath = SourceFilePath.Text;
            combine = int.Parse(combineNumber.Text);
            sum = 0;
            if (string.IsNullOrEmpty(sourceFilePath))
            {
                MessageBox.Show(Application.Current.MainWindow, "请选择原始图片", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
            var outputFilePath = OutputFilePath.Text.Replace(fileSuffix, DateTime.Now.ToString(fileSuffix));
            byte intensity = Convert.ToByte(ChangeIntensity.Value);
            byte radius = Convert.ToByte(ChangeRadius.Value);
            byte alpha = Convert.ToByte(ChangeAlpha.Value);
            var paletteFile = (PaletteStyle.SelectedItem as ComboBoxItem).Tag.ToString();
            new Thread(() =>
            {
                try
                {
                    var stopWatch = Stopwatch.StartNew();
                    Dispatcher.Invoke(new Action(() => Progress.Content = "正在加载原始图片"));
                    var sourceImage = (Bitmap)System.Drawing.Image.FromFile(sourceFilePath);
                    File.AppendAllText(System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "log.txt"), "正在加载原始图片：" + stopWatch.ElapsedMilliseconds + Environment.NewLine);

                    Dispatcher.Invoke(new Action(() => Progress.Content = "正在加载热点数据"));
                    var points = GetHeatPoints(sourceImage.Width, sourceImage.Height);
                    File.AppendAllText(System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "log.txt"), "正在加载热点数据：" + stopWatch.ElapsedMilliseconds + Environment.NewLine);

                    Dispatcher.Invoke(new Action(() => Progress.Content = "正在生成热点"));
                    var heatMap = new Bitmap(sourceImage.Width, sourceImage.Height);
                    DoDrawMask(heatMap, points, intensity, radius);
                    File.AppendAllText(System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "log.txt"), "正在生成热点：" + stopWatch.ElapsedMilliseconds + Environment.NewLine);

                    Dispatcher.Invoke(new Action(() => Progress.Content = "正在为热点应用颜色风格"));
                    var colorHeapMap = DoColorize(heatMap, LoadPalette(paletteFile), alpha);
                    File.AppendAllText(System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "log.txt"), "正在为热点应用颜色风格：" + stopWatch.ElapsedMilliseconds + Environment.NewLine);

                    Dispatcher.Invoke(new Action(() => Progress.Content = "正在合并图片"));

                    Graphics g = Graphics.FromImage(sourceImage);
                    g.DrawImage(colorHeapMap, new System.Drawing.Point(0, 0));

                    Dispatcher.Invoke(new Action(() => Progress.Content = "正在保存输出图片"));
                    sourceImage.Save(outputFilePath, ImageFormat.Png);
                    File.AppendAllText(System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "log.txt"), "正在合并图片：" + stopWatch.ElapsedMilliseconds + Environment.NewLine);
                    sourceImage.Dispose();

                    Dispatcher.Invoke(new Action(() =>
                    {
                        ImagePreview.Source = new BitmapImage(new Uri(outputFilePath, UriKind.Absolute));
                        Progress.Content = string.Format("耗时： {0} 毫秒，总条数：{1}，总样本：{2}", stopWatch.ElapsedMilliseconds, points.Count,  sum);
                    }));
                }
                catch (Exception ex)
                {
                    Dispatcher.Invoke(new Action(() =>
                    {
                        MessageBox.Show(ex.ToString(), "出错了", MessageBoxButton.OK, MessageBoxImage.Error);
                    }));
                }
            }).Start();
        }

        private void DoDrawMask(Bitmap bmp, List<System.Drawing.Point> heatPoints, byte intensity, byte radius)
        {
            Graphics surface = Graphics.FromImage(bmp);

            ColorBlend colors = GetColorBlend(intensity);
            foreach (var heatPoint in heatPoints)
            {
                DrawHeatPoint(surface, heatPoint, colors, radius);
            }
        }

        private List<int> LoadPalette(string file)
        {
            List<int> data = new List<int>();
            Bitmap paletteImage = (Bitmap)Bitmap.FromFile(file);
            for (int i = 0; i < paletteImage.Height; i++)
            {
                data.Add(paletteImage.GetPixel(0, i).ToArgb());
            }
            data[data.Count - 1] = 0;
            return data;
        }


        private static Bitmap DoColorize(Bitmap originalMask, List<int> palette, byte alpha)
        {
            var w = originalMask.Width;
            var h = originalMask.Height;

            Bitmap output = new Bitmap(w, h, System.Drawing.Imaging.PixelFormat.Format32bppArgb);

            BitmapData outputData = output.LockBits(new System.Drawing.Rectangle(0, 0, output.Width, output.Height), ImageLockMode.WriteOnly, output.PixelFormat);

            BitmapData markData = originalMask.LockBits(new System.Drawing.Rectangle(0, 0, originalMask.Width, originalMask.Height), ImageLockMode.ReadOnly, originalMask.PixelFormat);

            Parallel.For(0, h, y =>
            {
                Parallel.For(0, w, x =>
                {

                    int offset = y * markData.Stride + x * (markData.Stride / markData.Width);

                    int a = Marshal.ReadByte(markData.Scan0, offset + 3);
                    int r = Marshal.ReadByte(markData.Scan0, offset + 2);
                    int g = Marshal.ReadByte(markData.Scan0, offset + 1);
                    int b = Marshal.ReadByte(markData.Scan0, offset);

                    System.Drawing.Color c = System.Drawing.Color.FromArgb(a, r, g, b);
                    var black = c.ToArgb();
                    var colored = System.Drawing.Color.FromArgb(alpha, System.Drawing.Color.FromArgb(
                                palette[(byte)~(((uint)(black)) >> 24)]));

                    Marshal.WriteByte(outputData.Scan0, offset, (byte)colored.B);
                    Marshal.WriteByte(outputData.Scan0, offset + 1, (byte)colored.G);
                    Marshal.WriteByte(outputData.Scan0, offset + 2, (byte)colored.R);
                    Marshal.WriteByte(outputData.Scan0, offset + 3, (byte)colored.A);
                });
            });


            originalMask.UnlockBits(markData);
            output.UnlockBits(outputData);

            return output;
        }

        private void DrawHeatPoint(Graphics surface, System.Drawing.Point heatPoint, ColorBlend colors, int radius)
        {
            var ellipsePath = new GraphicsPath();
            ellipsePath.AddEllipse(heatPoint.X - radius, heatPoint.Y - radius,
                radius * 2, radius * 2);

            PathGradientBrush brush = new PathGradientBrush(ellipsePath);
            ColorBlend gradientSpecifications = colors;
            brush.InterpolationColors = gradientSpecifications;

            surface.FillEllipse(brush, heatPoint.X - radius,
                heatPoint.Y - radius, radius * 2, radius * 2);
        }

        private ColorBlend GetColorBlend(byte intensity)
        {
            ColorBlend colors = new ColorBlend(3);
            colors.Positions = new float[3] { 0, 0.5F, 1 };
            colors.Colors = new System.Drawing.Color[3]
            {
                System.Drawing.Color.FromArgb(0, System.Drawing.Color.White),
                System.Drawing.Color.FromArgb(intensity, System.Drawing.Color.White),
                System.Drawing.Color.FromArgb(intensity, System.Drawing. Color.White)
            };
            return colors;
        }

        private List<System.Drawing.Point> GetHeatPoints(int width = 500, int height = 600)
        {
#if DEBUG
            var heatPoints = new List<System.Drawing.Point>();

            Random r = new Random();
            for (int i = 0; i < 2000; i++)
            {
                int x = r.Next(0, width);
                int y = r.Next(0, height);
                heatPoints.Add(new System.Drawing.Point(x, y));
            }
            return heatPoints;
#else
            return GetData();
#endif
            
        }

        private long sum = 0;
        private List<System.Drawing.Point> GetData()
        {
            var heatPoints = new List<System.Drawing.Point>();
            MongoServer server = MongoServer.Create(MongonDbConnString);
            try
            {
                server.Connect();
                MongoDatabase db = server["AnalyseCenter"];
                MongoCollection<BsonDocument> table = db["PageInfo"];
                MongoCursor<BsonDocument> datas = table.FindAll();
                foreach (BsonDocument bd in datas)
                {
                    int count = int.Parse(bd["Count"].ToString());
                    sum += count;
                    for (int i = 0; i < count; i += combine)
                    {
                        heatPoints.Add(new System.Drawing.Point(DoubleToInt(bd["X"].ToString()), DoubleToInt(bd["Y"].ToString())));
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString());
            }
            finally
            {
                server.Disconnect();
            }
            return heatPoints;

        }

        public int DoubleToInt(string value)
        {
            if (string.IsNullOrEmpty(value)) return 0;
            decimal result = 0;
            decimal.TryParse(value, out result);
            return System.Convert.ToInt32(result);
        }
    }
}

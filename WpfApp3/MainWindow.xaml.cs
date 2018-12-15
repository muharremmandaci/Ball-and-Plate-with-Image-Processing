using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO.Ports;
using System.Linq;
using System.Runtime.InteropServices;
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
using Emgu.CV;
using Emgu.CV.CvEnum;
using Emgu.CV.Features2D;
using Emgu.CV.Structure;
using Emgu.CV.UI;
using Emgu.CV.Util;

namespace WpfApp3
{
    /// <summary>
    /// MainWindow.xaml etkileşim mantığı
    /// </summary>
    public partial class MainWindow : Window
    {
        VideoCapture _capture = null;
        private Mat _frame;
        Mat img;
        Matrix<Byte> matrix;
        private BitmapSource colorBitmap;

        int ct = 0;

        int centerX;
        int centerY;
        float[] angles = { 0, 0 };
        bool is_started = false;

        static SerialPort _serialPort;

        [DllImport("kernel32.dll", EntryPoint = "CopyMemory", SetLastError = false)]
        public static extern void CopyMemory(IntPtr dest, IntPtr src, uint count);

        public MainWindow()
        {
            _serialPort = new SerialPort();

            try
            {
                _capture = new VideoCapture();
                _capture.Start();
                _capture.ImageGrabbed += ProcessFrame;

            }
            catch (NullReferenceException excpt)
            {
                MessageBox.Show(excpt.Message);
            }

            _frame = new Mat();
 
            InitializeComponent();
            DataContext = this;
        }

        private void ProcessFrame(object sender, EventArgs arg)
        {
            if (_capture != null && _capture.Ptr != IntPtr.Zero)
            {
                _capture.Retrieve(_frame, 1);

                this.Dispatcher.Invoke((Action)(() =>
                {
                    img = find_ball();

                    colorBitmap = BSC.ToBitmapSource(img);
                    IS.Source = colorBitmap;

                    if (is_started)
                    {
                        run_control_algorithm();
                    }

                }));
            }
        }

        private void run_control_algorithm()
        {
            //angles = calculate_angles();
            send_angels_to_arduino(angles);
        }

        private void send_angels_to_arduino(float[] angles)
        {
            _serialPort.WriteLine(angles[0].ToString());
            Thread.Sleep(1);
            _serialPort.WriteLine(angles[1].ToString());
        }

        private float[] calculate_angles()
        {
            float[] result= {0, 0};
            return result;
        }

        private Mat foo(Mat img, MCvScalar orangeMin, MCvScalar orangeMax)
        {
            Mat hsvImg = new Mat();
            CvInvoke.CvtColor(img, hsvImg, ColorConversion.Bgr2Hsv);
            CvInvoke.InRange(hsvImg, new ScalarArray(orangeMin), new ScalarArray(orangeMax), hsvImg);
            //CvInvoke.MorphologyEx(hsvImg, hsvImg, MorphOp.Close, new Mat(), new System.Drawing.Point(-1, -1), 5, BorderType.Default, new MCvScalar());

            return hsvImg;
        }

        private Mat find_ball()
        {
            MCvScalar orangeMin = new MCvScalar(0,0,212);//10 120 100
            MCvScalar orangeMax = new MCvScalar(131, 255,255);//70 255 255

            Mat arr = new Mat();

            Mat img = _frame;
            Mat hsvImg = new Mat();

            CvInvoke.CvtColor(img, hsvImg, ColorConversion.Bgr2Hsv);
            CvInvoke.InRange(hsvImg, new ScalarArray(orangeMin), new ScalarArray(orangeMax),
            hsvImg);
            //CvInvoke.MorphologyEx(hsvImg, hsvImg, MorphOp.Close, new Mat(), new System.Drawing.Point(-1, -1), 5, BorderType.Default, new MCvScalar());
            SimpleBlobDetectorParams param = new SimpleBlobDetectorParams();
            param.FilterByCircularity = false;
            param.FilterByConvexity = false;
            param.FilterByInertia = false;
            param.FilterByColor = false;
            param.MinArea = 1000;
            param.MaxArea = 3000;
            SimpleBlobDetector detector = new SimpleBlobDetector(param);
            MKeyPoint[] keypoints = detector.Detect(hsvImg);
            Features2DToolbox.DrawKeypoints(img, new VectorOfKeyPoint(keypoints), img, new
            Bgr(255, 0, 0), Features2DToolbox.KeypointDrawType.DrawRichKeypoints);

            foreach (var item in keypoints)
            {
                centerX = (int)item.Point.X;
                centerY = (int)item.Point.Y;
            }

            lbl_x.Content = "Center X: " + centerX;
            lbl_y.Content = "Center Y: " + centerY;

            return img;
        }

        private void btn_search_Click(object sender, RoutedEventArgs e)
        {
            foreach (string s in SerialPort.GetPortNames())
            {
                if (!cb_port.Items.Contains(s))
                {
                    cb_port.Items.Add(s);
                }
                
            }
        }

        private void btn_stop_Click(object sender, RoutedEventArgs e)
        {
            is_started = false;

            _serialPort.Close();
            btn_start.IsEnabled = true;
            btn_stop.IsEnabled = false;
        }

        private void btn_start_Click(object sender, RoutedEventArgs e)
        {
            if (cb_baudrate.SelectedItem != null && cb_port.SelectedItem != null)
            {
                _serialPort.PortName = cb_port.SelectedItem.ToString();
                _serialPort.BaudRate = Convert.ToInt32(cb_baudrate.SelectedItem);

                _serialPort.Open();

                is_started = true;
                btn_start.IsEnabled=false;
                btn_stop.IsEnabled = true;
            }
            else
            {
                MessageBox.Show("null error");
            }

        }

        private void IS_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            System.Windows.Point position = e.GetPosition(this) ;
            MessageBox.Show(position.X + " " + position.Y);
        }

        private void Window_Initialized(object sender, EventArgs e)
        {
            foreach (string s in SerialPort.GetPortNames())
            {
                cb_port.Items.Add(s);
                cb_port.SelectedItem = cb_port.Items.GetItemAt(0);
            }

            cb_baudrate.Items.Add("9600");
            cb_baudrate.Items.Add("19200");
            cb_baudrate.Items.Add("57600");
            cb_baudrate.Items.Add("115200");

            cb_baudrate.SelectedItem = cb_baudrate.Items.GetItemAt(0);

            angles[0] = 45;
            angles[1] = 25;

            txt_value.Text ="45";
            txt_value2.Text = "25";

            btn_start.IsEnabled = true;
            btn_stop.IsEnabled = false;
        }

        private void btn_change_angle_Click(object sender, RoutedEventArgs e)
        {
            angles[0] = Convert.ToInt32(txt_value.Text);
            angles[1] = Convert.ToInt32(txt_value2.Text);
        }
    }
}

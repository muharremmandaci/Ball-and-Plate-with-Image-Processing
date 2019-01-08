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
        byte[] angles = { 0, 0 };
        bool is_started = false;

        float total_error_x = 0;
        float[] error_x = { 0, 0 };

        float total_error_y = 0;
        float[] error_y = { 0, 0 };

        float def_angle1 = 50;
        float def_angle2 = 25;

        int dX = 358, dY = 264;

        double Kp = -1.05;   //
        double Kd = -0.373;  //0.373
        double Ki = -0.8302;  //0.8302

        double cam_ratio = 0.0573;

        double Ts = 0.04;

        int max_angle = 10;

        float[] result = { 0, 0 };

        int x_max = 800;
        int x_min = 0;
        int y_max = 800;
        int y_min = 0;

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

            txt_kp.Text = Kp.ToString();
            txt_kd.Text = Kd.ToString();
            txt_ki.Text = Ki.ToString();

            txt_ref_x.Text = dX.ToString();
            txt_ref_y.Text = dY.ToString();

            txt_def_angle1.Text = def_angle1.ToString();
            txt_def_angle2.Text = def_angle2.ToString();
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
            float[] d_angles = calculate_angles();

            angles[0] = Convert.ToByte((def_angle1 - d_angles[1]) * 3);
            angles[1] = Convert.ToByte((def_angle2 - d_angles[0]) * 3);

            send_angels_to_arduino(angles);
        }

        private void send_angels_to_arduino(byte[] angles)
        {
            //txt_value.Text = (angles[0]/3).ToString();
            //txt_value2.Text = (angles[1]/3).ToString();

            _serialPort.WriteLine(angles[0].ToString());
            Thread.Sleep(1);
            _serialPort.WriteLine(angles[1].ToString());
        }

        private float[] calculate_angles()
        {
            error_x[0] = error_x[1];
            error_x[1] = (float)((dX - centerX) * cam_ratio);

            error_y[0] = error_y[1];
            error_y[1] = (float)((dY - centerY) * cam_ratio);

            if (error_x[1] < 0.4 && error_x[1] > -0.4)
            {
                error_x[1] = 0;
            }

            if (error_y[1] < 0.4 && error_y[1] > -0.4)
            {
                error_y[1] = 0;
            }

            total_error_x += error_x[1];
            total_error_y += error_y[1];

            result[0] = (float)(Kp * error_x[1] + Ki * Ts * total_error_x + Kd * ((error_x[1] - error_x[0]) / Ts));



            result[1] = (float)(Kp * error_y[1] + Ki * Ts * total_error_y + Kd * ((error_y[1] - error_y[0]) / Ts));

            txt_error_x.Text = error_x[1].ToString();
            txt_error_y.Text = error_y[1].ToString();

            if (result[0] > max_angle)
            {
                result[0] = max_angle;
            }
            else if (result[0] < -max_angle)
            {
                result[0] = -max_angle;
            }

            if (result[1] > max_angle)
            {
                result[1] = max_angle;
            }
            else if (result[1] < -max_angle)
            {
                result[1] = -max_angle;
            }

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
            MCvScalar orangeMin = new MCvScalar(0, 0, 212);//10 120 100
            MCvScalar orangeMax = new MCvScalar(131, 255, 255);//70 255 255

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
            param.MinArea = 800;
            param.MaxArea = 5000;
            SimpleBlobDetector detector = new SimpleBlobDetector(param);
            MKeyPoint[] keypoints = detector.Detect(hsvImg);
            Features2DToolbox.DrawKeypoints(img, new VectorOfKeyPoint(keypoints), img, new
            Bgr(255, 0, 0), Features2DToolbox.KeypointDrawType.DrawRichKeypoints);

            foreach (var item in keypoints)
            {
                if ((int)item.Point.X > x_min && (int)item.Point.X < x_max && (int)item.Point.Y > y_min && (int)item.Point.Y < y_max)
                {
                    centerX = (int)item.Point.X;
                    centerY = (int)item.Point.Y;
                }
                else
                {
                    centerX = dX;
                    centerY = dY;

                    total_error_x = 0;

                    total_error_y = 0;
                }

            }
            if (keypoints.Length == 0)
            {
                centerX = dX;
                centerY = dY;

                total_error_x = 0;
            
                total_error_y = 0;
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

            byte[] zero = { (byte)(def_angle1 * 3), (byte)(def_angle2 * 3) };
            send_angels_to_arduino(zero);

            total_error_x = 0;
            error_x[0] = 0;
            error_x[1] = 0;

            total_error_y = 0;
            error_y[0] = 0;
            error_y[1] = 0;

            _serialPort.Close();
            btn_start.IsEnabled = true;
            btn_stop.IsEnabled = false;
        }

        private void btn_start_Click(object sender, RoutedEventArgs e)
        {
            if (cb_baudrate.SelectedItem != null && cb_port.SelectedItem != null)
            {
                if (!_serialPort.IsOpen)
                {
                    _serialPort.PortName = cb_port.SelectedItem.ToString();
                    _serialPort.BaudRate = Convert.ToInt32(cb_baudrate.SelectedItem);

                    _serialPort.Open();
                }

                is_started = true;
                btn_start.IsEnabled = false;
                btn_stop.IsEnabled = true;
            }
            else
            {
                MessageBox.Show("null error");
            }

        }

        private void IS_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            System.Windows.Point position = e.GetPosition(this);
            MessageBox.Show("X: " + position.X + " Y: " + position.Y);
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

            btn_start.IsEnabled = true;
            btn_stop.IsEnabled = false;
        }

        private void btn_update_servo_Click(object sender, RoutedEventArgs e)
        {
            if (!_serialPort.IsOpen)
            {
                _serialPort.PortName = cb_port.SelectedItem.ToString();
                _serialPort.BaudRate = Convert.ToInt32(cb_baudrate.SelectedItem);

                _serialPort.Open();
            }

            def_angle1 = Convert.ToInt32(txt_def_angle1.Text);
            def_angle2 = Convert.ToInt32(txt_def_angle2.Text);

            byte[] angles = { (byte)(def_angle1 * 3), (byte)(def_angle2 * 3) };
            send_angels_to_arduino(angles);
        }

        private void btn_update_refs_Click(object sender, RoutedEventArgs e)
        {
            dX = Convert.ToInt32(txt_ref_x.Text);
            dY = Convert.ToInt32(txt_ref_y.Text);
        }

        private void btn_update_Click(object sender, RoutedEventArgs e)
        {
            Kp = Convert.ToDouble(txt_kp.Text);
            Kd = Convert.ToDouble(txt_kd.Text);
            Ki = Convert.ToDouble(txt_ki.Text);  
        }
    }
}

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
using System.IO.Ports;
using System.Management;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace ToolUartESP
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        // Biến để quản lý cổng Serial
        private SerialPort serialPort;
        
        // Hàng đợi thread-safe để lưu trữ dữ liệu nhận được từ Serial
        private ConcurrentQueue<string> dataQueue = new ConcurrentQueue<string>();
        
        // Task để xử lý dữ liệu nhận được
        private Task dataProcessingTask;
        
        // Biến cờ để theo dõi trạng thái xử lý dữ liệu
        private bool isProcessingData = false;
        
        // Object dùng để đồng bộ hóa truy cập
        private readonly object lockObject = new object();
        
        // Quản lý việc hủy task
        private CancellationTokenSource cancellationTokenSource;

        /// <summary>
        /// Constructor của MainWindow
        /// </summary>
        public MainWindow()
        {
            InitializeComponent();
            this.Loaded += MainWindow_Loaded;
            this.Closing += MainWindow_Closing;
        }

        /// <summary>
        /// Sự kiện được gọi khi cửa sổ được tải hoàn tất
        /// </summary>
        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            LoadComPorts();       // Tải danh sách cổng COM
            SelectBuadRate();     // Thiết lập các tốc độ baud
            SelectSize();         // Thiết lập các kích thước dữ liệu
            
            // Thiết lập giá trị mặc định cho giao diện
            connectionStatus.Text = "Not Connected";
            connectionStatus.Foreground = new SolidColorBrush(Color.FromRgb(231, 76, 60)); // Màu đỏ
        }

        /// <summary>
        /// Sự kiện được gọi khi cửa sổ đang đóng
        /// </summary>
        private void MainWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            // Dọn dẹp tài nguyên khi đóng ứng dụng
            StopDataProcessing(); // Dừng xử lý dữ liệu
            CloseSerialPort();    // Đóng cổng Serial
        }

        /// <summary>
        /// Lớp chứa thông tin về cổng COM
        /// </summary>
        public class ComPortInfo
        {
            public string DisplayName { get; set; } // Tên hiển thị của cổng
            public string PortName { get; set; }   // Tên cổng COM

            public override string ToString()
            {
                return DisplayName; // Trả về tên hiển thị khi chuyển đối tượng thành chuỗi
            }
        }

        /// <summary>
        /// Tải danh sách cổng COM có sẵn trên hệ thống
        /// </summary>
        private void LoadComPorts()
        {
            List<ComPortInfo> portList = new List<ComPortInfo>();

            // Sử dụng WMI để tìm các thiết bị USB Serial
            using (var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_PnPEntity WHERE Name LIKE '%(COM%'"))
            {
                foreach (var device in searcher.Get())
                {
                    string name = device["Name"]?.ToString(); // Ví dụ: "USB Serial Device (COM3)"
                    if (!string.IsNullOrEmpty(name))
                    {
                        // Trích xuất COMx từ chuỗi bằng regex
                        var match = System.Text.RegularExpressions.Regex.Match(name, @"\(COM\d+\)");
                        if (match.Success)
                        {
                            string portName = match.Value.Trim('(', ')'); // Lấy "COM3" từ "(COM3)"
                            portList.Add(new ComPortInfo
                            {
                                DisplayName = name,      // Tên đầy đủ của thiết bị
                                PortName = portName      // Tên cổng (COM3)
                            });
                        }
                    }
                }
            }

            // Cập nhật giao diện với danh sách cổng
            comPortComboBox.ItemsSource = portList;
            if (portList.Count > 0)
                comPortComboBox.SelectedIndex = 0;  // Chọn cổng đầu tiên mặc định
        }

        /// <summary>
        /// Thiết lập các tốc độ Baud rate cho ComboBox
        /// </summary>
        private void SelectBuadRate()
        {
            List<string> baudRateList = new List<string>()
            {
                "9600",
                "19200",
                "38400",
                "57600",
                "115200"
            };

            baudRateComboBox.ItemsSource = baudRateList;
            if (baudRateList.Count > 0)
                baudRateComboBox.SelectedIndex = 4; // Mặc định chọn 115200
        }

        /// <summary>
        /// Thiết lập các kích thước dữ liệu (Data Bits) cho ComboBox
        /// </summary>
        private void SelectSize()
        {
            List<string> sizeList = new List<string>()
            {
                "7",
                "8",              
            };

            dataSizeBox.ItemsSource = sizeList;
            if (sizeList.Count > 0)
                dataSizeBox.SelectedIndex = 1; // Mặc định chọn 8
        }

        /// <summary>
        /// Xử lý sự kiện nhấn nút Refresh Ports
        /// </summary>
        private void RefreshPortsButton_Click(object sender, RoutedEventArgs e)
        {
            LoadComPorts(); // Tải lại danh sách cổng COM
        }

        /// <summary>
        /// Xử lý sự kiện nhấn nút Connect/Disconnect
        /// </summary>
        private void ConnectButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Nếu đã kết nối thì ngắt kết nối
                if (serialPort != null && serialPort.IsOpen)
                {
                    StopDataProcessing();  // Dừng xử lý dữ liệu
                    CloseSerialPort();     // Đóng cổng Serial
                    
                    // Cập nhật giao diện về trạng thái ngắt kết nối
                    connectButton.Content = "Connect";
                    connectionStatus.Text = "Not Connected";
                    connectionStatus.Foreground = new SolidColorBrush(Color.FromRgb(231, 76, 60)); // Màu đỏ
                    
                    return;
                }

                // Chưa kết nối => tiến hành kết nối
                if (comPortComboBox.SelectedItem == null)
                {
                    MessageBox.Show("Vui lòng chọn một cổng COM.", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
                
                // Lấy thông tin cổng COM đã chọn
                var selectedPort = comPortComboBox.SelectedItem as ComPortInfo;
                string portName = selectedPort?.PortName;

                if (string.IsNullOrEmpty(portName))
                {
                    MessageBox.Show("Cổng COM không hợp lệ.", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                // Lấy các thông số kết nối từ giao diện
                int baudRate = int.Parse(baudRateComboBox.SelectedItem?.ToString());
                int dataBits = int.Parse(dataSizeBox.SelectedItem?.ToString());

                // Khởi tạo và cấu hình đối tượng SerialPort
                serialPort = new SerialPort(portName, baudRate, Parity.None, dataBits, StopBits.One);
                serialPort.Encoding = Encoding.UTF8;  // Sử dụng UTF-8 để hỗ trợ Unicode
                serialPort.ReadBufferSize = 4096;    // Tăng kích thước buffer để cải thiện hiệu suất
                serialPort.DataReceived += SerialPort_DataReceived;  // Đăng ký sự kiện nhận dữ liệu
                serialPort.Open();  // Mở cổng Serial

                // Bắt đầu task xử lý dữ liệu
                StartDataProcessing();
                
                // Cập nhật giao diện về trạng thái đã kết nối
                connectButton.Content = "Disconnect";
                connectionStatus.Text = $"Đã kết nối tới {portName} ở {baudRate}";
                connectionStatus.Foreground = new SolidColorBrush(Color.FromRgb(39, 174, 96)); // Màu xanh lá
            }
            catch (Exception ex)
            {
                MessageBox.Show("Lỗi kết nối: " + ex.Message, "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
                connectButton.Content = "Connect";
                connectionStatus.Text = "Kết nối thất bại";
                connectionStatus.Foreground = new SolidColorBrush(Color.FromRgb(231, 76, 60)); // Màu đỏ
                
                // Dọn dẹp trong trường hợp lỗi
                StopDataProcessing();
                CloseSerialPort();
            }
        }

        /// <summary>
        /// Đóng cổng Serial và giải phóng tài nguyên
        /// </summary>
        private void CloseSerialPort()
        {
            if (serialPort != null && serialPort.IsOpen)
            {
                try
                {
                    serialPort.DataReceived -= SerialPort_DataReceived;  // Hủy đăng ký sự kiện
                    serialPort.Close();   // Đóng cổng
                    serialPort.Dispose(); // Giải phóng tài nguyên
                }
                catch (Exception) { /* Bỏ qua lỗi trong quá trình dọn dẹp */ }
                finally
                {
                    serialPort = null;  // Đảm bảo tham chiếu được giải phóng
                }
            }
        }

        /// <summary>
        /// Xử lý sự kiện khi có dữ liệu đến từ cổng Serial
        /// </summary>
        private void SerialPort_DataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            try
            {
                if (serialPort == null || !serialPort.IsOpen)
                    return;

                // Đọc tất cả dữ liệu có sẵn một lần
                string data = serialPort.ReadExisting();
                if (!string.IsNullOrEmpty(data))
                {
                    // Thêm dữ liệu vào hàng đợi để xử lý
                    dataQueue.Enqueue(data);
                }
            }
            catch (Exception)
            {
                // Bỏ qua lỗi trong quá trình nhận dữ liệu - sẽ được xử lý bởi task xử lý
            }
        }
        
        /// <summary>
        /// Bắt đầu task xử lý dữ liệu từ hàng đợi
        /// </summary>
        private void StartDataProcessing()
        {
            if (isProcessingData) // Nếu đã đang xử lý thì không khởi động lại
                return;

            isProcessingData = true;
            cancellationTokenSource = new CancellationTokenSource();
            var token = cancellationTokenSource.Token;

            // Tạo và chạy task xử lý dữ liệu
            dataProcessingTask = Task.Run(async () => 
            {
                try
                {
                    StringBuilder batchData = new StringBuilder();
                    int batchSize = 0;
                    
                    // Vòng lặp xử lý dữ liệu liên tục cho đến khi bị hủy
                    while (!token.IsCancellationRequested)
                    {
                        // Cố gắng lấy dữ liệu từ hàng đợi
                        if (dataQueue.TryDequeue(out string data))
                        {
                            batchData.Append(data);
                            batchSize++;
                            
                            // Xử lý theo lô để tăng hiệu suất
                            if (batchSize >= 10 || !dataQueue.IsEmpty)
                            {
                                string textToAppend = batchData.ToString();
                                await Dispatcher.InvokeAsync(() => 
                                {
                                    uartReceiveTextBox.AppendText(textToAppend);
                                    uartReceiveTextBox.ScrollToEnd();
                                });
                                
                                batchData.Clear();
                                batchSize = 0;
                            }
                        }
                        else
                        {
                            // Nếu có dữ liệu trong buffer nhưng ít hơn kích thước lô
                            if (batchData.Length > 0)
                            {
                                string textToAppend = batchData.ToString();
                                await Dispatcher.InvokeAsync(() => 
                                {
                                    uartReceiveTextBox.AppendText(textToAppend);
                                    uartReceiveTextBox.ScrollToEnd();
                                });
                                
                                batchData.Clear();
                                batchSize = 0;
                            }
                            
                            // Chờ một chút trước khi kiểm tra lại để giảm sử dụng CPU
                            await Task.Delay(10, token);
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    // Hủy bình thường, không cần làm gì
                }
                catch (Exception ex)
                {
                    await Dispatcher.InvokeAsync(() => 
                    {
                        uartReceiveTextBox.AppendText($"[Lỗi xử lý: {ex.Message}]\n");
                    });
                }
                finally
                {
                    isProcessingData = false;
                }
            }, token);
        }
        
        /// <summary>
        /// Dừng task xử lý dữ liệu
        /// </summary>
        private void StopDataProcessing()
        {
            if (cancellationTokenSource != null)
            {
                cancellationTokenSource.Cancel(); // Gửi tín hiệu hủy tới task
                try
                {
                    dataProcessingTask?.Wait(1000); // Đợi tối đa 1 giây để task hoàn thành
                }
                catch (Exception) { /* Bỏ qua lỗi */ }
                
                cancellationTokenSource.Dispose();
                cancellationTokenSource = null;
            }
            
            // Xóa bỏ dữ liệu còn lại trong hàng đợi
            while (dataQueue.TryDequeue(out _)) { }
        }

        /// <summary>
        /// Xử lý sự kiện nhấn nút Send để gửi dữ liệu
        /// </summary>
        private void SendButton_Click(object sender, RoutedEventArgs e)
        {
            if (serialPort == null || !serialPort.IsOpen)
            {
                MessageBox.Show("Vui lòng kết nối tới cổng COM trước.", "Chưa kết nối", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            string textToSend = uartSendTextBox.Text;
            if (string.IsNullOrEmpty(textToSend))
            {
                MessageBox.Show("Vui lòng nhập nội dung để gửi.", "Không có dữ liệu", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            try
            {
                // Thêm <CL><RF> vào cuối nếu checkbox được chọn
                if (appendCLRFCheckbox.IsChecked == true)
                {
                    textToSend += "<CL><RF>";
                }

                // Gửi dữ liệu
                serialPort.Write(textToSend);
                
                // Hiển thị dữ liệu đã gửi vào cửa sổ nhận với tiền tố
                Dispatcher.Invoke(() =>
                {
                    uartReceiveTextBox.AppendText($"[ĐÃ GỬI] {textToSend}\n");
                    uartReceiveTextBox.ScrollToEnd();
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi khi gửi dữ liệu: {ex.Message}", "Lỗi gửi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Xử lý sự kiện nhấn nút Clear để xóa nội dung trong ô gửi
        /// </summary>
        private void ClearSendButton_Click(object sender, RoutedEventArgs e)
        {
            uartSendTextBox.Clear(); // Xóa nội dung trong ô nhập liệu gửi
        }

        /// <summary>
        /// Xử lý sự kiện nhấn nút Clear Receive để xóa nội dung nhận được
        /// </summary>
        private void ClearReceiveButton_Click(object sender, RoutedEventArgs e)
        {
            uartReceiveTextBox.Clear(); // Xóa nội dung trong ô hiển thị dữ liệu nhận
        }
    }
}
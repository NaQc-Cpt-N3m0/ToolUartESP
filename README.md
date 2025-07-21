# Công cụ UART ESP (Tool UART ESP)

Đây là một ứng dụng WPF được viết bằng C# dùng để giao tiếp với các thiết bị ESP (ESP8266, ESP32, v.v.) thông qua cổng UART (Serial). Ứng dụng cho phép người dùng kết nối với thiết bị, gửi và nhận dữ liệu qua cổng Serial.

## Tính năng chính

- Tự động phát hiện các cổng COM có sẵn trên hệ thống
- Hỗ trợ nhiều tốc độ Baudrate (9600, 19200, 38400, 57600, 115200)
- Giao diện hiển thị dữ liệu gửi và nhận
- Tùy chọn thêm <CL><RF> khi gửi dữ liệu
- Xử lý dữ liệu theo lô để tăng hiệu suất

## Yêu cầu hệ thống

- Windows (đã kiểm thử trên Windows 10)
- .NET 8.0 Runtime (hoặc SDK để phát triển)
- Quyền truy cập các cổng COM

## Hướng dẫn cài đặt

### Cách 1: Sử dụng file thực thi đã biên dịch

1. Tải phiên bản mới nhất từ mục Releases trên GitHub (hoặc từ thư mục Portable)
2. Giải nén file nếu cần
3. Chạy file `ToolUartESP.exe`

### Cách 2: Biên dịch từ mã nguồn

1. Clone repository này về máy tính:
   ```
   git clone <địa-chỉ-repository>
   ```

2. Cài đặt .NET 8.0 SDK nếu bạn chưa có:
   - Tải từ [trang web chính thức của Microsoft](https://dotnet.microsoft.com/download/dotnet/8.0)

3. Mở Terminal/Command Prompt và di chuyển đến thư mục dự án:
   ```
   cd ToolUartESP
   ```

4. Biên dịch và chạy ứng dụng:
   ```
   dotnet build
   dotnet run --project ToolUartESP
   ```

5. Nếu muốn biên dịch thành file thực thi độc lập:
   ```
   dotnet publish -c Release -r win-x64 --self-contained false
   ```
   - File thực thi sẽ nằm trong thư mục `ToolUartESP\bin\Release\net8.0-windows\win-x64\publish`

## Hướng dẫn sử dụng

1. Khởi động ứng dụng
2. Kết nối thiết bị ESP với máy tính qua cáp USB
3. Chọn cổng COM tương ứng (nhấn "Refresh Ports" nếu không thấy cổng)
4. Chọn tốc độ Baudrate phù hợp (mặc định là 115200)
5. Nhấn "Connect" để kết nối với thiết bị
6. Sau khi kết nối, bạn có thể:
   - Nhập nội dung trong ô bên phải và nhấn "Send" để gửi đến thiết bị
   - Xem dữ liệu nhận được từ thiết bị trong ô bên trái
   - Tích chọn "Append <CL><RF>" để thêm ký tự xuống dòng vào cuối lệnh gửi

7. Nhấn "Disconnect" khi hoàn thành để ngắt kết nối

## Xử lý sự cố

1. **Không tìm thấy cổng COM**
   - Kiểm tra kết nối USB
   - Cài đặt driver cho chip USB-to-UART (CP210x, CH340, FTDI, v.v.)
   - Nhấn nút "Refresh Ports"

2. **Không kết nối được**
   - Đảm bảo cổng COM không bị chiếm bởi ứng dụng khác
   - Kiểm tra Baudrate có phù hợp với cấu hình của thiết bị ESP không
   - Thử khởi động lại ứng dụng

3. **Không nhận được dữ liệu**
   - Kiểm tra kết nối phần cứng
   - Xác nhận ESP đang gửi dữ liệu đúng tốc độ Baudrate đã cấu hình

## Phát triển

Dự án được viết bằng C# với .NET 8.0 và WPF. Cấu trúc dự án:
- `MainWindow.xaml` & `MainWindow.xaml.cs`: Giao diện người dùng và logic chính
- `App.xaml` & `App.xaml.cs`: Điểm vào ứng dụng
- Thư viện chính: System.IO.Ports và System.Management

## Giấy phép

[Thêm thông tin về giấy phép nếu có] 
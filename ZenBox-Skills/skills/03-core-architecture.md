# 03 - Kiến trúc Kỹ thuật Cốt lõi của ZenBox

Tài liệu này định nghĩa kiến trúc phần mềm, cấu trúc Solution, danh mục thư viện phụ thuộc và cấu hình đóng gói phát hành cho **ZenBox** trên nền tảng Windows 11.

---

## 1. Cấu trúc Solution & Phân tầng MVVM

Solution `ZenBox.sln` bao gồm 2 dự án chuẩn `.NET 8.0-windows`:

1. **`ZenBox` (`net8.0-windows`, WPF Application):**
   - `Models/`: Các lớp dữ liệu miền (`AppTileItem`, `MediaItem`, `AppConfig`, `VietnameseErrorInfo`, `RemoteCommandPayload`).
   - `ViewModels/`: Quản lý trạng thái giao diện theo mẫu MVVM với `CommunityToolkit.Mvvm` (`MainViewModel`, `HomeViewModel`, `WebContainerViewModel`, `MediaLibraryViewModel`, `MediaPlayerViewModel`, `SettingsViewModel`).
   - `Views/`: Các màn hình `UserControl` & cửa sổ chính/phụ (`MainWindow`, `FloatingMenuWindow`, `HomeView`, `WebContainerView`, `MediaLibraryView`, `MediaPlayerView`, `SettingsView`).
   - `Controls/`: Các thành phần giao diện tái sử dụng (`ErrorDialogOverlay`, thẻ kính mờ).
   - `Services/`:
     - `ConfigService`: Đọc/ghi `config.json` và quản lý khóa Registry khởi động cùng Windows 11 (`HKCU\Software\Microsoft\Windows\CurrentVersion\Run`).
     - `NetworkMonitorService`: Theo dõi kết nối mạng thời gian thực qua `NetworkInterface` & `NetworkChange`.
     - `WebBrowserService`: Quản lý `CoreWebView2Environment`, giữ trạng thái Pool các tab `WebView2`, nạp extension `uBlock Origin` kết hợp bộ lọc chặn domain quảng cáo tầng mạng.
     - `MediaLibraryService`: Quét đệ quy kho phim/nhạc offline, đọc ảnh bìa nhạc bằng `TagLibSharp` và trích xuất thumbnail video gốc từ Windows Shell bằng COM `IShellItemImageFactory` (không phụ thuộc FFmpeg).
     - `SystemAudioService`: Điều khiển âm lượng hệ thống Windows 11 trực tiếp qua P/Invoke `SendMessageW` (`WM_APPCOMMAND`).
     - `ErrorHandlingService`: Ánh xạ 4 nhóm lỗi tiếng Việt bình dân và ghi nhật ký `logs/error_YYYYMMDD.txt`.
     - `RemoteControlServer`: Máy chủ WebSocket nền dùng `Fleck` lắng nghe tại `ws://0.0.0.0:8989` nhận lệnh điều khiển từ xa trên mạng nội bộ.
   - `Themes/`: Từ điển tài nguyên `GlassTheme.xaml` định nghĩa toàn bộ màu sắc, hiệu ứng kính mờ, kiểu nút 10-Foot UI.
   - `Assets/`: Chứa tài nguyên biểu tượng và bộ tiện ích mở rộng `Assets/Extensions/uBlock/`.

2. **`ZenBox.Tests` (`net8.0-windows`, xUnit Test Project):**
   - Kiểm thử tự động tính tuân thủ quy chuẩn giao diện XAML (`FontSize >= 16`, không chứa từ tiếng Anh kỹ thuật cấm).
   - Xác minh sự hiện diện và hợp lệ của `Assets/Extensions/uBlock/manifest.json`.
   - Kiểm thử ánh xạ thông báo tiếng Việt và ghi nhật ký của `ErrorHandlingService`.
   - Kiểm thử bộ lọc định dạng tệp đa phương tiện của `MediaLibraryService`.

---

## 2. Chế độ Hiển thị & Xử lý Airspace (HWND Interop)

- **Chế độ Toàn màn hình Mặc định (Kiosk / TV Mode):**
  - `MainWindow` khởi chạy với `WindowStyle="None"`, `WindowState="Maximized"`, `ResizeMode="NoResize"`, tràn toàn bộ màn hình TV/máy tính.
- **Giải pháp Chống lỗi Airspace cho Nút Tròn Nổi (AssistiveTouch):**
  - Do `WebView2` và `LibVLCSharp.WPF.VideoView` sở hữu cửa sổ con Win32 riêng (`HWND`), các lớp phủ WPF thông thường trong cùng một cửa sổ sẽ bị che khuất.
  - `FloatingMenuWindow` được thiết kế thành một cửa sổ độc lập (`AllowsTransparency="True"`, `WindowStyle="None"`, `Background="Transparent"`, `Topmost="True"`, `ShowInTaskbar="False"`, `Owner = MainWindow`), hỗ trợ kéo thả mượt mà và bung bảng điều khiển kính mờ đè lên trên mọi nội dung video/web.

---

## 3. Danh sách Gói NuGet Bắt buộc

- `Microsoft.Web.WebView2` — Trình duyệt nhân Chromium nhúng hỗ trợ nạp Chrome Extension.
- `LibVLCSharp.WPF` — Giao diện trình phát đa phương tiện phần cứng cho WPF.
- `VideoLAN.LibVLC.Windows` — Thư viện nhân giải mã đa phương tiện gốc cho Windows.
- `TagLibSharp` — Đọc siêu dữ liệu và ảnh bìa nhúng trong tệp âm thanh.
- `Fleck` — Máy chủ WebSocket gọn nhẹ cho tính năng điều khiển từ xa qua điện thoại.
- `CommunityToolkit.Mvvm` — Bộ công cụ MVVM hiện đại hiệu năng cao (`ObservableObject`, `RelayCommand`).

---

## 4. Cấu hình Đóng gói Phát hành (Single-File `.exe`)

Dự án `ZenBox.csproj` được cấu hình sẵn sàng cho phát hành thực thi độc lập trên Windows 11 (`win-x64`):
- `PublishSingleFile=true`
- `SelfContained=true`
- `IncludeNativeLibrariesForSelfExtract=true`
- `IncludeAllContentForSelfExtract=false` (giữ thư mục `Assets/Extensions/uBlock/**` bên cạnh hoặc giải nén hợp lệ để trình duyệt Chromium nạp trực tiếp từ đường dẫn thư mục vật lý thông qua `AppContext.BaseDirectory`).

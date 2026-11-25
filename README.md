# ZenBox — Trung tâm Giải trí Gia đình 10-Foot UI cho Windows 11

**ZenBox** là ứng dụng Windows Desktop (`.NET 8 / WPF`) thiết kế theo chuẩn **10-Foot TV Interface** kết hợp phong cách kính mờ **Glassmorphism**, giúp người lớn tuổi và cả gia đình dễ dàng xem YouTube, Facebook, TikTok không quảng cáo, cũng như thưởng thức kho Phim & Nhạc Offline sẵn trong máy tính.

## Tính năng nổi bật

- **Giao diện 10-Foot Glassmorphism 100% Tiếng Việt:** Chữ lớn sắc nét (`>= 18px`), thẻ ứng dụng cỡ lớn, phản hồi phóng to `1.05x` mượt mà trong `180ms` cùng viền xanh ngọc `#38BDF8`.
- **Nút tròn nổi trợ năng (AssistiveTouch) chống đè HWND:** Cửa sổ nổi độc lập luôn hiển thị trên cả trình duyệt `WebView2` và trình phát `LibVLC`, hỗ trợ kéo thả tự do, chuyển kênh nhanh và tăng/giảm âm lượng hệ thống Windows 11.
- **Chặn quảng cáo 2 lớp tự động:** Tích hợp sẵn tiện ích mở rộng **uBlock Origin** nạp vào `CoreWebView2` kết hợp bộ lọc chặn tên miền quảng cáo tầng mạng, đồng thời giữ nguyên trạng thái các tab khi chuyển đổi.
- **Kho Phim & Nhạc Offline Poster Grid:** Tự động đọc ảnh bìa nhạc bằng `TagLibSharp` và trích xuất ảnh thu nhỏ video từ Windows Shell (`IShellItemImageFactory`) không cần FFmpeg; phát mượt mà với giải mã phần cứng GPU (`LibVLCSharp.WPF`).
- **Báo lỗi Tiếng Việt bình dân & Tự phục hồi:** Không bao giờ hiển thị mã lỗi kỹ thuật phức tạp lên màn hình; tự động ghi nhật ký chi tiết vào `logs/error_YYYYMMDD.txt`.
- **Máy chủ điều khiển từ xa WebSocket (`Fleck`):** Lắng nghe tại `ws://0.0.0.0:8989` sẵn sàng kết nối điều khiển qua điện thoại trong mạng nội bộ.

## Đóng gói bản chạy độc lập (`ZenBox.exe`)

```powershell
dotnet publish ZenBox/ZenBox.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o ./publish
```

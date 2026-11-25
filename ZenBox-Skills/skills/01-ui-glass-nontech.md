# 01 - Quy chuẩn Giao diện Glassmorphism & Thân thiện Người dùng Phổ thông (10-Foot TV UI)

Tài liệu này quy định tiêu chuẩn thiết kế giao diện bắt buộc cho toàn bộ dự án **ZenBox** trên Windows 11, tối ưu hóa cho trải nghiệm nhìn từ khoảng cách xa (10-Foot TV Interface) và thân thiện tuyệt đối với người lớn tuổi hoặc người không rành công nghệ.

---

## 1. Phong cách Nền & Hiệu ứng Kính mờ (Glassmorphism)

- **Nền tổng thể (Deep Dark Mode):**
  - Sử dụng dải chuyển sắc (LinearGradientBrush) sâu dịu từ `#0B0F19` sang `#111827` để chống chói mắt khi sử dụng trong phòng khách hoặc phòng ngủ tối.
- **Thẻ nội dung (Card) & Bảng điều khiển (Menu/Dialog):**
  - Nền bán trong suốt: `#14FFFFFF`.
  - Viền sáng mảnh: độ dày `1px`, mã màu `#2EFFFFFF`.
  - Bo góc lớn mềm mại: `CornerRadius="24"`.
  - Đổ bóng chiều sâu: `DropShadowEffect` với `BlurRadius="24"`, `ShadowDepth="8"`, `Opacity="0.4"`, `Color="#000000"`.

---

## 2. Hệ thống Màu sắc & Phản hồi Tương tác Tức thì

- **Màu nhấn chủ đạo (Primary Accent):** Xanh ngọc sáng `#38BDF8`.
- **Màu trạng thái kết nối tốt:** Xanh ngọc lục bảo `#34D399`.
- **Màu cảnh báo / Mất kết nối:** Cam san hô dịu `#FB7185` (không gây chói gắt như đỏ tươi thuần túy).
- **Màu chữ:**
  - Chữ chính (Primary Text): `#F8FAFC`
  - Chữ phụ (Secondary Text): `#CBD5E1`
- **Hiệu ứng Hover & Focus (Bắt buộc cho 10-Foot UI):**
  - Khi rê chuột (`IsMouseOver`) hoặc di chuyển tiêu điểm bàn phím/điều khiển (`IsKeyboardFocused` / `IsFocused`):
    - Viền thẻ chuyển sang màu xanh ngọc `#38BDF8` với độ dày `3px`.
    - Phóng to mượt mà tỷ lệ `ScaleX = 1.05`, `ScaleY = 1.05` quanh tâm (`RenderTransformOrigin="0.5,0.5"`) trong thời gian `180ms` (`CubicEase` EaseOut) giúp người lớn tuổi hoặc người ngồi xa màn hình TV nhận biết ngay mục đang chọn.

---

## 3. Kích thước & Kiểu chữ chuẩn 10-Foot UI

- **Phông chữ chuẩn:** `Segoe UI Variable Display` (fallback `Segoe UI`), hiển thị sắc nét và đầy đủ dấu tiếng Việt.
- **Quy mô cỡ chữ (Font Scale):**
  - Tiêu đề màn hình lớn: `32px – 40px` (`FontWeight="Bold"`).
  - Tên ứng dụng / Tên bộ phim / Bài hát: `22px – 26px` (`FontWeight="SemiBold"`).
  - Văn bản phụ / Mô tả / Hướng dẫn: tối thiểu `18px` (`FontWeight="Regular"` hoặc `Medium`).
  - **QUY TẮC CẤM TUYỆT ĐỐI:** Không được phép sử dụng `FontSize` nhỏ hơn `16` ở bất kỳ thành phần giao diện XAML hay code-behind nào.
- **Kích thước vùng chạm & khoảng cách:**
  - Thẻ ứng dụng (App Card): tối thiểu `160x160px`.
  - Nút chức năng (Action Button): chiều cao tối thiểu `56px`, vùng đệm rộng rãi.
  - Khoảng cách tách biệt giữa các thẻ (Margin/Spacing): tối thiểu `24px`.

---

## 4. Ngôn ngữ Giao diện & Bố cục Thanh Trạng thái

- **100% Tiếng Việt tự nhiên, bình dân:**
  - Tuyệt đối cấm xuất hiện các thuật ngữ tiếng Anh chuyên ngành hoặc kỹ thuật trên giao diện người dùng như: *Settings, Cache, Buffer, Network Error, Retry, Exception, Timeout, URL, Reload*.
  - Thay thế bằng từ ngữ đời thường: *Cài đặt, Bộ nhớ tạm, Đang tải phim, Mất kết nối mạng, Thử lại ngay, Tải lại trang*.
- **Thanh trạng thái cố định phía trên (Top Status Bar):**
  - Luôn hiển thị:
    1. Biểu tượng & Logo **ZenBox**.
    2. Đồng hồ thời gian thực định dạng `HH:mm`.
    3. Thứ, Ngày/Tháng/Năm bằng tiếng Việt chuẩn (ví dụ: *Thứ Tư, 25/11/2026*).
    4. Huy hiệu trạng thái mạng trực quan: **`Đã kết nối mạng`** (chấm tròn & chữ xanh) hoặc **`Mất kết nối mạng`** (chấm tròn & chữ cam san hô `#FB7185`).
- **Thanh gợi ý thao tác nhanh phía dưới (Bottom Hint Bar):**
  - Luôn hướng dẫn ngắn gọn cách dùng chuột, phím tắt hoặc nút tròn nổi trợ năng để quay về Trang Chủ và chỉnh âm lượng bất cứ lúc nào.

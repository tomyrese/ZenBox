# 02 - Quy chuẩn Báo lỗi Tiếng Việt Thân thiện & Tự Phục hồi

Tài liệu này quy định cơ chế bắt giữ, ghi nhận và trình bày thông báo sự cố trong **ZenBox**, đảm bảo người dùng phổ thông không bao giờ hoang mang trước các thông báo lỗi máy tính phức tạp.

---

## 1. Nguyên tắc Cốt lõi

- **CẤM TUYỆT ĐỐI** hiển thị mã lỗi kỹ thuật (`0x800...`, `HRESULT`, `Exception`, `StackTrace`, `HTTP 502`, `ERR_NAME_NOT_RESOLVED`, `NullReferenceException`...) lên màn hình người dùng.
- Mọi sự cố phát sinh từ trình duyệt nhúng (`WebView2`), trình phát đa phương tiện (`LibVLCSharp`), kiểm tra kết nối mạng, hoặc ngoại lệ toàn cục (`DispatcherUnhandledException`, `UnobservedTaskException`) đều phải đi qua dịch vụ trung tâm `ErrorHandlingService`.

---

## 2. Cấu trúc Hộp thoại Báo lỗi Glassmorphism Cỡ lớn

Mỗi khi cần thông báo sự cố, giao diện hiển thị hộp thoại kính mờ nổi bật ở chính giữa màn hình gồm đúng **3 thành phần rõ ràng**:

1. **Phần 1 - Tiêu đề & Giải thích bình dân:**
   - Tiêu đề cỡ lớn (`32px`, Bold, màu nhấn cảnh báo dịu `#FB7185`) kết hợp câu giải thích ngắn gọn, dễ hiểu về chuyện gì đang xảy ra.
2. **Phần 2 - Hướng dẫn tự khắc phục đơn giản (1–2 bước):**
   - Đưa ra đúng 1 đến 2 gợi ý đời thường, rõ ràng (cỡ chữ tối thiểu `20px`) để người nhà có thể tự kiểm tra ngay.
3. **Phần 3 - Cụm nút hành động to rõ (cao tối thiểu `60px`):**
   - **`[ Thử lại ngay ]`** — Thực hiện lại thao tác vừa bị gián đoạn.
   - **`[ Về Trang Chủ ]`** — Đưa ứng dụng về màn hình chính an toàn.
   - **`[ Mở Phim Offline ]`** — Chuyển ngay sang kho phim có sẵn trong máy để tiếp tục giải trí khi mất mạng.

---

## 3. Bảng Ánh xạ Bắt buộc cho 4 Nhóm Lỗi

| Nhóm sự cố | Tiêu đề thân thiện | Giải thích bình dân | Bước tự khắc phục |
| :--- | :--- | :--- | :--- |
| **1. Mất kết nối mạng** (`NetworkOffline`) | Không có kết nối mạng Internet | Máy tính hiện chưa kết nối được với mạng Wi-Fi hoặc dây mạng trong nhà. | 1. Kiểm tra lại cục phát Wi-Fi hoặc dây cắm mạng sau máy.<br>2. Trong lúc chờ mạng, bạn có thể bấm **Mở Phim Offline** để xem phim có sẵn trong máy. |
| **2. Trang web tải quá lâu / lỗi** (`WebLoadFailed`) | Trang web đang bận hoặc phản hồi chậm | Kênh giải trí trực tuyến này hiện đang quá tải hoặc đường truyền chập chờn nên chưa mở được ngay. | 1. Chờ vài giây rồi bấm nút **Thử lại ngay**.<br>2. Hoặc quay **Về Trang Chủ** để chọn kênh giải trí khác. |
| **3. Tệp phim / nhạc bị hỏng hoặc không tồn tại** (`MediaFileCorrupted`) | Không mở được tệp phim hoặc bài hát này | Tệp tin này có thể đã bị di chuyển sang thư mục khác, rút ổ cứng ngoài, hoặc định dạng tệp bị lỗi. | 1. Kiểm tra xem ổ đĩa chứa phim/nhạc còn gắn vào máy không.<br>2. Vào mục **Cài đặt** để chọn lại thư mục lưu trữ nếu bạn vừa đổi chỗ lưu. |
| **4. Lỗi hệ thống bất ngờ** (`UnexpectedSystemError`) | ZenBox vừa gặp chút trục trặc nhỏ | Ứng dụng vừa tự động xử lý một sự cố bất ngờ để bảo vệ trải nghiệm của bạn. | 1. Bấm **Về Trang Chủ** để tiếp tục sử dụng bình thường.<br>2. Nếu vẫn lặp lại, hãy khởi động lại ZenBox. |

---

## 4. Ghi Nhật ký Kỹ thuật Tự động & Khôi phục Trạng thái

- Với mọi sự cố (đặc biệt là nhóm **Lỗi hệ thống bất ngờ**), hệ thống tự động ghi đầy đủ thời gian, phân loại sự cố, thông điệp nội bộ và `StackTrace` chi tiết vào tệp nhật ký:
  - Đường dẫn: `logs/error_YYYYMMDD.txt` (cạnh thư mục thực thi của ứng dụng).
- Ngay sau khi ghi nhật ký, `ErrorHandlingService` đánh dấu ngoại lệ đã được xử lý (`Handled = true`), giữ cho tiến trình ứng dụng không bị văng (crash) và cho phép người dùng khôi phục về giao diện chính chỉ bằng một cú nhấp chuột.

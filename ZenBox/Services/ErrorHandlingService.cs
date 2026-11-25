using System.IO;
using System.Text;
using ZenBox.Models;

namespace ZenBox.Services;

public sealed class ErrorHandlingService
{
    private readonly string _logDirectory;
    private readonly object _logLock = new();

    public event EventHandler<VietnameseErrorInfo>? ErrorReported;

    public ErrorHandlingService(string? baseDirectory = null)
    {
        var root = string.IsNullOrWhiteSpace(baseDirectory) ? AppContext.BaseDirectory : baseDirectory;
        _logDirectory = Path.Combine(root, "logs");
    }

    public string GetTodayLogFilePath()
    {
        return Path.Combine(_logDirectory, $"error_{DateTime.Now:yyyyMMdd}.txt");
    }

    public VietnameseErrorInfo CreateErrorInfo(ErrorCategory category, Action? retryAction = null)
    {
        return category switch
        {
            ErrorCategory.NetworkOffline => new VietnameseErrorInfo
            {
                Category = ErrorCategory.NetworkOffline,
                Title = "Chưa có kết nối mạng",
                Explanation = "Máy tính hiện chưa kết nối được với mạng Wi-Fi hoặc dây mạng trong nhà nên tạm thời chưa mở được kênh trực tuyến.",
                RecoverySteps =
                [
                    "Bước 1: Kiểm tra lại cục phát Wi-Fi trong nhà hoặc dây mạng cắm phía sau máy tính.",
                    "Bước 2: Bấm Thử lại ngay sau khi có mạng, hoặc chọn Mở Phim Offline để xem phim có sẵn trong máy."
                ],
                RetryAction = retryAction
            },
            ErrorCategory.WebLoadFailed => new VietnameseErrorInfo
            {
                Category = ErrorCategory.WebLoadFailed,
                Title = "Trang giải trí đang phản hồi chậm",
                Explanation = "Kênh trực tuyến này hiện đang bận hoặc đường truyền mạng trong nhà chập chờn nên chưa hiển thị kịp.",
                RecoverySteps =
                [
                    "Bước 1: Chờ vài giây rồi bấm nút Thử lại ngay bên dưới.",
                    "Bước 2: Hoặc bấm Về Trang Chủ để chuyển sang kênh giải trí khác."
                ],
                RetryAction = retryAction
            },
            ErrorCategory.MediaFileCorrupted => new VietnameseErrorInfo
            {
                Category = ErrorCategory.MediaFileCorrupted,
                Title = "Không mở được tệp phim hoặc bài hát này",
                Explanation = "Tệp phim hoặc bài hát bạn chọn có thể đã bị chuyển sang chỗ khác, rút ổ đĩa rời, hoặc tệp bị hỏng.",
                RecoverySteps =
                [
                    "Bước 1: Kiểm tra xem ổ đĩa chứa phim và nhạc còn cắm chắc vào máy tính không.",
                    "Bước 2: Vào mục Cài đặt ở Trang Chủ để chọn lại đúng thư mục lưu trữ nếu bạn vừa đổi chỗ lưu."
                ],
                RetryAction = retryAction
            },
            _ => new VietnameseErrorInfo
            {
                Category = ErrorCategory.UnexpectedSystemError,
                Title = "ZenBox vừa gặp một trục trặc nhỏ",
                Explanation = "Ứng dụng đã tự động xử lý sự cố bất ngờ để giữ an toàn và sẵn sàng đưa bạn quay lại màn hình chính.",
                RecoverySteps =
                [
                    "Bước 1: Bấm nút Về Trang Chủ bên dưới để tiếp tục sử dụng bình thường.",
                    "Bước 2: Nếu sự cố vẫn lặp lại, hãy đóng và mở lại ứng dụng ZenBox."
                ],
                RetryAction = retryAction
            }
        };
    }

    public void LogError(ErrorCategory category, string context, Exception? exception = null)
    {
        try
        {
            lock (_logLock)
            {
                Directory.CreateDirectory(_logDirectory);
                var logPath = GetTodayLogFilePath();
                var sb = new StringBuilder();
                sb.AppendLine("==========================================================");
                sb.AppendLine($"Thời gian : {DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}");
                sb.AppendLine($"Phân loại : {category}");
                sb.AppendLine($"Ngữ cảnh  : {context}");
                if (exception is not null)
                {
                    sb.AppendLine($"Chi tiết  : {exception.GetType().FullName}: {exception.Message}");
                    sb.AppendLine("StackTrace:");
                    sb.AppendLine(exception.ToString());
                }
                sb.AppendLine();

                File.AppendAllText(logPath, sb.ToString(), Encoding.UTF8);
            }
        }
        catch
        {
            // Bỏ qua lỗi ghi tệp nhật ký để không bao giờ làm sập ứng dụng.
        }
    }

    public VietnameseErrorInfo ReportError(
        ErrorCategory category,
        string context,
        Exception? exception = null,
        Action? retryAction = null)
    {
        LogError(category, context, exception);
        var info = CreateErrorInfo(category, retryAction);
        ErrorReported?.Invoke(this, info);
        return info;
    }
}

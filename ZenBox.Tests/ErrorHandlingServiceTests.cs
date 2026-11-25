using System.IO;
using ZenBox.Models;
using ZenBox.Services;

namespace ZenBox.Tests;

public sealed class ErrorHandlingServiceTests
{
    private static readonly string[] TechnicalTerms =
    [
        "0x800",
        "Exception",
        "StackTrace",
        "HTTP 502",
        "ERR_",
        "Network Error",
        "Buffer",
        "Cache"
    ];

    [Theory]
    [InlineData(ErrorCategory.NetworkOffline)]
    [InlineData(ErrorCategory.WebLoadFailed)]
    [InlineData(ErrorCategory.MediaFileCorrupted)]
    [InlineData(ErrorCategory.UnexpectedSystemError)]
    public void CreateErrorInfo_ShouldMapAllFourCategoriesToFriendlyVietnamese(ErrorCategory category)
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "ZenBox_ErrorTest_" + Guid.NewGuid().ToString("N"));
        try
        {
            var service = new ErrorHandlingService(tempDir);
            var info = service.CreateErrorInfo(category, () => { });

            Assert.Equal(category, info.Category);
            Assert.False(string.IsNullOrWhiteSpace(info.Title));
            Assert.False(string.IsNullOrWhiteSpace(info.Explanation));
            Assert.InRange(info.RecoverySteps.Count, 1, 2);
            Assert.True(info.CanRetry);

            // Xác nhận 100% không rò rỉ thuật ngữ hay mã lỗi kỹ thuật lên giao diện
            foreach (var term in TechnicalTerms)
            {
                Assert.DoesNotContain(term, info.Title, StringComparison.OrdinalIgnoreCase);
                Assert.DoesNotContain(term, info.Explanation, StringComparison.OrdinalIgnoreCase);
                foreach (var step in info.RecoverySteps)
                {
                    Assert.DoesNotContain(term, step, StringComparison.OrdinalIgnoreCase);
                }
            }
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, recursive: true);
            }
        }
    }

    [Fact]
    public void ReportError_ShouldWriteDetailedLogFileAndRaiseEvent()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "ZenBox_LogTest_" + Guid.NewGuid().ToString("N"));
        try
        {
            var service = new ErrorHandlingService(tempDir);
            VietnameseErrorInfo? raisedError = null;
            service.ErrorReported += (_, info) => raisedError = info;

            Exception capturedEx;
            try
            {
                throw new InvalidOperationException("Simulated internal crash 0x80070005");
            }
            catch (Exception ex)
            {
                capturedEx = ex;
            }

            var result = service.ReportError(
                ErrorCategory.UnexpectedSystemError,
                "UnitTest Crash Simulation",
                capturedEx);

            Assert.NotNull(raisedError);
            Assert.Equal(ErrorCategory.UnexpectedSystemError, result.Category);
            Assert.DoesNotContain("0x80070005", result.Explanation, StringComparison.OrdinalIgnoreCase);

            var expectedLogPath = service.GetTodayLogFilePath();
            Assert.True(File.Exists(expectedLogPath), $"Không tìm thấy file log tại {expectedLogPath}");

            var logContent = File.ReadAllText(expectedLogPath);
            Assert.Contains("InvalidOperationException", logContent);
            Assert.Contains("0x80070005", logContent);
            Assert.Contains("UnitTest Crash Simulation", logContent);
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, recursive: true);
            }
        }
    }
}

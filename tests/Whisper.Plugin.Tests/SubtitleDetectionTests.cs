using System.IO;
using Jellyfin.Plugin.WhisperSubtitles.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Whisper.Plugin.Tests
{
    public class SubtitleDetectionTests
    {
        [Fact]
        public void HasSubtitles_ReturnsFalse_WhenNoSubtitleFiles()
        {
            var tempDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            Directory.CreateDirectory(tempDir);
            var videoPath = Path.Combine(tempDir, "movie.mkv");
            File.WriteAllText(videoPath, "");

            var service = new SubtitleDetectionService(new NullLogger<SubtitleDetectionService>());
            var result = service.HasSubtitles(videoPath, "en");

            // Clean up
            File.Delete(videoPath);
            Directory.Delete(tempDir);

            Assert.False(result);
        }

        [Fact]
        public void HasSubtitles_ReturnsTrue_WhenSrtExists()
        {
            var tempDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            Directory.CreateDirectory(tempDir);
            var videoPath = Path.Combine(tempDir, "movie.mkv");
            File.WriteAllText(videoPath, "");
            var srtPath = Path.Combine(tempDir, "movie.en.srt");
            File.WriteAllText(srtPath, "1\n00:00:00,000 --> 00:00:01,000\nHello\n");

            var service = new SubtitleDetectionService(new NullLogger<SubtitleDetectionService>());
            var result = service.HasSubtitles(videoPath, "en");

            // Clean up
            File.Delete(videoPath);
            File.Delete(srtPath);
            Directory.Delete(tempDir);

            Assert.True(result);
        }
    }
}

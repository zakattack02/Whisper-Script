using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.WhisperSubtitles.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Whisper.Plugin.Tests
{
    public class WhisperPostScanTests
    {
        [Fact]
        public async Task Run_DoesNotThrow_WhenNoFiles()
        {
            var libraryManager = Mock.Of<MediaBrowser.Controller.Library.ILibraryManager>();
            var whisperService = Mock.Of<IWhisperService>();
            var subtitleService = new SubtitleDetectionService(new NullLogger<SubtitleDetectionService>());
            var logger = new NullLogger<Jellyfin.Plugin.WhisperSubtitles.Tasks.WhisperPostScanTask>();

            var task = new Jellyfin.Plugin.WhisperSubtitles.Tasks.WhisperPostScanTask(
                libraryManager,
                whisperService,
                subtitleService,
                logger);

            await task.Run(new Progress<double>(), CancellationToken.None);
        }
    }
}

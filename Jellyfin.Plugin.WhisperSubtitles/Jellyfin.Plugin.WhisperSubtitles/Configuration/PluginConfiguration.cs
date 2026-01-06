using MediaBrowser.Model.Plugins;

namespace Jellyfin.Plugin.WhisperSubtitles.Configuration
{
    /// <summary>
    /// Whisper model options.
    /// </summary>
    public enum WhisperModelType
    {
        /// <summary>
        /// Tiny model (~10x speed, ~1GB VRAM, ~75MB download).
        /// </summary>
        Tiny,

        /// <summary>
        /// Base model (~7x speed, ~1GB VRAM, ~140MB download).
        /// </summary>
        Base,

        /// <summary>
        /// Small model (~4x speed, ~2GB VRAM, ~460MB download) - Recommended.
        /// </summary>
        Small,

        /// <summary>
        /// Medium model (~2x speed, ~5GB VRAM, ~1.5GB download).
        /// </summary>
        Medium,

        /// <summary>
        /// Turbo model (~8x speed, ~6GB VRAM, ~1.6GB download).
        /// </summary>
        Turbo,

        /// <summary>
        /// Large model (Best quality, ~10GB VRAM, ~3GB download).
        /// </summary>
        Large
    }

    /// <summary>
    /// Plugin configuration.
    /// </summary>
    public class PluginConfiguration : BasePluginConfiguration
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="PluginConfiguration"/> class.
        /// </summary>
        public PluginConfiguration()
        {
            WhisperModel = WhisperModelType.Small;
            TargetLanguage = "en";
            AIIdentifier = "whisper";
            TranslateToEnglish = false;
            WordTimestamps = false;
            ProcessOnLibraryScan = false;
            SkipExisting = true;
            RegenerateAI = false;
            UseGPUAcceleration = true;
        }

        /// <summary>
        /// Gets or sets the Whisper model to use.
        /// </summary>
        public WhisperModelType WhisperModel { get; set; }

        /// <summary>
        /// Gets or sets the target language for subtitles.
        /// </summary>
        public string TargetLanguage { get; set; }

        /// <summary>
        /// Gets or sets the AI identifier to add to subtitle filenames.
        /// </summary>
        public string AIIdentifier { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether to translate to English.
        /// </summary>
        public bool TranslateToEnglish { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether to enable word-level timestamps.
        /// </summary>
        public bool WordTimestamps { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether to process on library scan.
        /// </summary>
        public bool ProcessOnLibraryScan { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether to skip existing subtitles.
        /// </summary>
        public bool SkipExisting { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether to regenerate AI subtitles.
        /// </summary>
        public bool RegenerateAI { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether to enable GPU acceleration.
        /// </summary>
        public bool UseGPUAcceleration { get; set; }
    }
}

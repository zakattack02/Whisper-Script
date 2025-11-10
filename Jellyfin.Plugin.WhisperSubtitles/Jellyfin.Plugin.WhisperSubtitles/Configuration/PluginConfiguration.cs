using MediaBrowser.Model.Plugins;

namespace Jellyfin.Plugin.WhisperSubtitles.Configuration
{
    /// <summary>
    /// Plugin configuration.
    /// </summary>
    public class PluginConfiguration : BasePluginConfiguration
    {
        /// <summary>
        /// Gets or sets the Whisper model to use.
        /// </summary>
        public string WhisperModel { get; set; } = "small";

        /// <summary>
        /// Gets or sets the target language for subtitles.
        /// </summary>
        public string TargetLanguage { get; set; } = "en";

        /// <summary>
        /// Gets or sets a value indicating whether to translate audio to English.
        /// </summary>
        public bool TranslateToEnglish { get; set; } = true;

        /// <summary>
        /// Gets or sets the AI identifier to add to subtitle filenames.
        /// </summary>
        public string AIIdentifier { get; set; } = "whisper";

        /// <summary>
        /// Gets or sets a value indicating whether to use word-level timestamps.
        /// </summary>
        public bool WordTimestamps { get; set; } = false;

        /// <summary>
        /// Gets or sets a value indicating whether to process media on library scan.
        /// </summary>
        public bool ProcessOnLibraryScan { get; set; } = false;

        /// <summary>
        /// Gets or sets a value indicating whether to skip files with existing subtitles.
        /// </summary>
        public bool SkipExisting { get; set; } = true;

        /// <summary>
        /// Gets or sets a value indicating whether to regenerate AI-generated subtitles.
        /// </summary>
        public bool RegenerateAI { get; set; } = false;

        /// <summary>
        /// Gets or sets the library IDs to process.
        /// </summary>
        public string[] LibrariesToProcess { get; set; } = System.Array.Empty<string>();
    }
}

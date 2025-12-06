using System;
using System.Collections.Generic;
using Jellyfin.Plugin.WhisperSubtitles.Configuration;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Serialization;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.WhisperSubtitles
{
    /// <summary>
    /// The main plugin class.
    /// </summary>
    public class Plugin : BasePlugin<PluginConfiguration>, IHasWebPages
    {
        private readonly ILogger<Plugin> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="Plugin"/> class.
        /// </summary>
        /// <param name="applicationPaths">Instance of the <see cref="IApplicationPaths"/> interface.</param>
        /// <param name="xmlSerializer">Instance of the <see cref="IXmlSerializer"/> interface.</param>
        /// <param name="logger">Instance of the <see cref="ILogger{Plugin}"/> interface.</param>
        public Plugin(IApplicationPaths applicationPaths, IXmlSerializer xmlSerializer, ILogger<Plugin> logger)
            : base(applicationPaths, xmlSerializer)
        {
            Instance = this;
            _logger = logger;
            
            _logger.LogInformation("Whisper Subtitles Plugin v{Version} initialized", Version);
            _logger.LogInformation("Plugin ID: {PluginId}", Id);
            _logger.LogInformation("Configuration loaded - Model: {Model}, Language: {Language}, TranslateToEnglish: {Translate}",
                Configuration.WhisperModel,
                Configuration.TargetLanguage,
                Configuration.TranslateToEnglish);
        }

        /// <inheritdoc />
        public override string Name => "Whisper Subtitles";

        /// <inheritdoc />
        public override Guid Id => Guid.Parse("a8b7c6d5-e4f3-4a5b-9c8d-7e6f5a4b3c2d");

        /// <summary>
        /// Gets the current plugin instance.
        /// </summary>
        public static Plugin? Instance { get; private set; }

        /// <inheritdoc />
        public override void UpdateConfiguration(BasePluginConfiguration configuration)
        {
            var oldConfig = Configuration;
            base.UpdateConfiguration(configuration);
            
            var newConfig = (PluginConfiguration)configuration;
            
            _logger.LogInformation("Configuration updated");
            _logger.LogInformation("  Whisper Model: {OldModel} -> {NewModel}", 
                oldConfig.WhisperModel, newConfig.WhisperModel);
            _logger.LogInformation("  Target Language: {OldLang} -> {NewLang}", 
                oldConfig.TargetLanguage, newConfig.TargetLanguage);
            _logger.LogInformation("  Translate to English: {OldTranslate} -> {NewTranslate}", 
                oldConfig.TranslateToEnglish, newConfig.TranslateToEnglish);
            _logger.LogInformation("  AI Identifier: {OldId} -> {NewId}", 
                oldConfig.AIIdentifier, newConfig.AIIdentifier);
            _logger.LogInformation("  Word Timestamps: {OldWord} -> {NewWord}", 
                oldConfig.WordTimestamps, newConfig.WordTimestamps);
            _logger.LogInformation("  Process on Library Scan: {OldScan} -> {NewScan}", 
                oldConfig.ProcessOnLibraryScan, newConfig.ProcessOnLibraryScan);
            _logger.LogInformation("  Skip Existing: {OldSkip} -> {NewSkip}", 
                oldConfig.SkipExisting, newConfig.SkipExisting);
            _logger.LogInformation("  Regenerate AI: {OldRegen} -> {NewRegen}", 
                oldConfig.RegenerateAI, newConfig.RegenerateAI);
        }

        /// <inheritdoc />
        public IEnumerable<PluginPageInfo> GetPages()
        {
            var configPagePath = GetType().Namespace + ".Configuration.configPage.html";
            var logoPath = GetType().Namespace + ".Configuration.Logo.png";
            
            _logger.LogInformation("GetPages() called - returning configuration page and logo");
            _logger.LogInformation("  Configuration Page: {ConfigPage}", configPagePath);
            _logger.LogInformation("  Logo: {Logo}", logoPath);
            
            return new[]
            {
                new PluginPageInfo
                {
                    Name = Name,
                    EmbeddedResourcePath = configPagePath
                },
                new PluginPageInfo
                {
                    Name = Name + " Logo",
                    EmbeddedResourcePath = logoPath
                }
            };
        }
    }
}

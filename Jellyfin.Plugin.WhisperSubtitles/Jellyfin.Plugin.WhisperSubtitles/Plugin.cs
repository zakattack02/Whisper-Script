using System;
using System.Collections.Generic;
using Jellyfin.Plugin.WhisperSubtitles.Configuration;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Serialization;
using Microsoft.Extensions.DependencyInjection;
using Jellyfin.Plugin.WhisperSubtitles.Services;
using Jellyfin.Plugin.WhisperSubtitles.Tasks;
using MediaBrowser.Controller.Plugins;
using MediaBrowser.Common;

namespace Jellyfin.Plugin.WhisperSubtitles
{
    /// <summary>
    /// The main plugin class.
    /// </summary>
    public class Plugin : BasePlugin<PluginConfiguration>, IHasWebPages, IPluginServiceRegistrator
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Plugin"/> class.
        /// </summary>
        /// <param name="applicationPaths">Instance of the <see cref="IApplicationPaths"/> interface.</param>
        /// <param name="xmlSerializer">Instance of the <see cref="IXmlSerializer"/> interface.</param>
        public Plugin(IApplicationPaths applicationPaths, IXmlSerializer xmlSerializer)
            : base(applicationPaths, xmlSerializer)
        {
            Instance = this;
        }

        /// <inheritdoc />
        public override string Name => "Whisper Subtitles";

        /// <inheritdoc />
        public override Guid Id => Guid.Parse("a8b7c6d5-e4f3-4a5b-9c8d-7e6f5a4b3c2d");

        /// <summary>
        /// Gets the current plugin instance.
        /// </summary>
        public static Plugin? Instance { get; private set; }

        /// <summary>
        /// Register services into the host's service collection.
        /// This method is called by Jellyfin to allow plugins to add services.
        /// </summary>
        /// <param name="services">Service collection to register into.</param>
        public void RegisterServices(IServiceCollection services)
        {
            // Register core services
            services.AddSingleton<IWhisperService, WhisperService>();
            services.AddSingleton<ISubtitleDetectionService, SubtitleDetectionService>();

            // Register tasks
            services.AddTransient<WhisperSubtitleTask>();
            services.AddTransient<WhisperPostScanTask>();
        }

        /// <inheritdoc />
        public IEnumerable<PluginPageInfo> GetPages()
        {
            return new[]
            {
                new PluginPageInfo
                {
                    Name = Name,
                    EmbeddedResourcePath = GetType().Namespace + ".Configuration.configPage.html"
                }
            };
        }
    }
}

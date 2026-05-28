using System;
using System.Collections.Generic;
using Jellyfin.Plugin.WhisperSubtitles.Configuration;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Serialization;

namespace Jellyfin.Plugin.WhisperSubtitles
{
    public class Plugin : BasePlugin<PluginConfiguration>, IHasWebPages
    {
        public Plugin(IApplicationPaths applicationPaths, IXmlSerializer xmlSerializer)
            : base(applicationPaths, xmlSerializer)
        {
            Instance = this;
            ApplicationPaths = applicationPaths;
        }

        public static Plugin? Instance { get; private set; }

        public new IApplicationPaths ApplicationPaths { get; private set; }

        public override string Name => "Whisper Subtitles";

        public override Guid Id => new Guid("a8b7c6d5-e4f3-4a5b-9c8d-7e6f5a4b3c2d");

        public IEnumerable<PluginPageInfo> GetPages()
        {
            return new[]
            {
                new PluginPageInfo
                {
                    Name = "Whisper Subtitles",
                    EmbeddedResourcePath = GetType().Namespace + ".Configuration.configPage.html",
                    MenuSection = "server",
                    MenuIcon = "subtitles",
                    EnableInMainMenu = Configuration.EnableMainMenu
                }
            };
        }
    }
}

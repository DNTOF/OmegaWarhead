using System.ComponentModel;
using Exiled.API.Interfaces;

namespace OmegaWarhead.Configs
{
    /// <summary>
    /// Plugin configuration. Only enable/disable and debug flags are exposed here.
    /// All gameplay balance values are hardcoded in <see cref="Constants"/> and
    /// cannot be modified via config.yml.
    /// </summary>
    public sealed class Config : IConfig
    {
        [Description("Whether the plugin is enabled")]
        public bool IsEnabled { get; set; } = true;

        [Description("Whether to output debug logs")]
        public bool Debug { get; set; } = false;

        [Description("Plugin language. 'zh' = Simplified Chinese, 'en' = English")]
        public string Lang { get; set; } = "zh";
    }
}

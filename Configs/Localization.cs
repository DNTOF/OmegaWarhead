using System;

namespace OmegaWarhead.Configs
{
    /// <summary>
    /// Centralized translations for all player-facing text.
    /// Language is selected via Config.Lang ("zh" or "en") at plugin enable.
    ///
    /// Item descriptions must stay SHORT (≤ 20 chars) — longer text overlaps
    /// in the SCP:SL item inspect UI.
    /// </summary>
    public static class Localization
    {
        /// <summary>Whether the current language is English (false = Chinese).</summary>
        public static bool IsEnglish { get; private set; }

        /// <summary>
        /// Initializes the translation language from config.
        /// </summary>
        public static void Init(Config config)
        {
            IsEnglish = config.Lang?.Trim().ToLowerInvariant() == "en";
        }

        // ------------------- Custom Items -------------------

        public static string ItemElementName => IsEnglish ? "Radioactive Element" : "放射性元素";

        public static string ItemElementDesc => IsEnglish
            ? "Radioactive fuel fragment. Harmful to hold."
            : "放射性燃料残块，持有有害。";

        public static string ItemControllerName => IsEnglish
            ? "OMEGA Warhead Launch Controller"
            : "OMEGA核弹发射控制器";

        public static string ItemControllerDesc => IsEnglish
            ? "Launch device. Activate twice to start the countdown."
            : "发射装置，确认两次启动倒计时。";

        // ------------------- Kill Reasons -------------------

        public static string KillReasonRadiation => IsEnglish ? "Radiation Poisoning" : "辐射中毒";

        public static string KillReasonWarhead => IsEnglish ? "OMEGA Warhead Detonation" : "OMEGA核弹引爆";

        // ------------------- Synthesis Broadcast -------------------

        public static string BroadcastSynthesisFailed => IsEnglish
            ? "Elements collected, but a controller already exists on the field. Synthesis failed."
            : "已集齐元素，但场上已存在控制器，无法合成。";

        public static string BroadcastControllerAssembled => IsEnglish
            ? "<color=#ff0000><b>OMEGA Warhead Launch Controller synthesized!</b></color>\n" +
              "Use the controller to confirm launch.\n<color=#888888><size=12>By DNT_OF</size></color>"
            : "<color=#ff0000><b>OMEGA核弹发射控制器已合成！</b></color>\n" +
              "使用控制器确认发射。\n<color=#888888><size=12>By DNT_OF</size></color>";

        // ------------------- Keybind -------------------

        public static string KeybindLabel => IsEnglish
            ? "OMEGA Warhead Launch Controller: Confirm"
            : "OMEGA核弹发射控制器：确认操作";

        public static string KeybindHint => IsEnglish
            ? "Press this key to confirm launch while holding the Warhead Launch Controller in your inventory."
            : "背包内持有核弹发射控制器时，按下此键进行发射确认。";

        // ------------------- Tracking Broadcasts -------------------

        public static string TrackingCounting(string nick, string location, string timeText, bool pointOfNoReturn)
        {
            string color = pointOfNoReturn ? "#ff0000" : "#ff4444";
            if (IsEnglish)
            {
                return $"<color=#ff0000><b>[OMEGA Warhead Tracking]</b></color> " +
                       $"Operator <color={color}>{nick}</color> is at {location}, {timeText} " +
                       $"<color=#ffaa00>Intercept immediately to abort the launch!</color> " +
                       "<color=#888888><size=10>By DNT_OF</size></color>";
            }
            return $"<color=#ff0000><b>[OMEGA核弹追踪]</b></color> " +
                   $"开核者 <color={color}>{nick}</color> 位于 {location}，{timeText} " +
                   $"<color=#ffaa00>立即拦截以中止发射！</color> " +
                   "<color=#888888><size=10>By DNT_OF</size></color>";
        }

        public static string TrackingTimeRemaining(float remaining) =>
            IsEnglish ? $"<color=#ffff00><b>{remaining:F0}</b></color> seconds until detonation"
                      : $"<color=#ffff00><b>{remaining:F0}</b></color> 秒后引爆";

        public static string TrackingReadingCountdown => IsEnglish ? "Reading countdown..." : "倒计时读取中...";

        public static string TrackingCollecting(string nick, string location) =>
            IsEnglish
                ? $"<color=#ffaa00>[Radiation Tracking]</color> Detected {nick} carrying radioactive elements. Current location: <color=#ff4444>{location}</color>"
                : $"<color=#ffaa00>[辐射追踪]</color> 检测到 {nick} 携带放射性元素，当前位置：<color=#ff4444>{location}</color>";

        // ------------------- CASSIE (spoken + subtitle) -------------------

        public static string CassieComponentAssembled => IsEnglish
            ? "WARNING . OMEGA WARHEAD COMPONENT ASSEMBLED"
            : "警告 . OMEGA核弹组件已组装";

        public static string CassieLaunchSequence => IsEnglish
            ? "WARNING . OMEGA WARHEAD LAUNCH SEQUENCE INITIATED . ALL PERSONNEL EVACUATE"
            : "警告 . OMEGA核弹发射程序已启动 . 全体人员撤离";

        public static string CassiePointOfNoReturn => IsEnglish
            ? "OMEGA WARHEAD POINT OF NO RETURN"
            : "OMEGA核弹已过不可逆点";

        public static string CassieAborted => IsEnglish
            ? "OMEGA WARHEAD LAUNCH ABORTED"
            : "OMEGA核弹发射已中止";

        // ------------------- HSM Panel -------------------

        public static string PanelTitle => IsEnglish ? "OMEGA Warhead Launch Control System" : "OMEGA核弹发射控制系统";

        public static string PanelIdleStatus => IsEnglish ? "Status: [ INACTIVE ]" : "状态：[ 未激活 ]";

        public static string PanelIdleHolder(string nick) =>
            IsEnglish ? $"Holder: {nick}" : $"持有者：{nick}";

        public static string PanelIdleStandby => IsEnglish ? "Standby" : "待机中";

        public static string PanelIdleFooter => IsEnglish
            ? "Activate the device to initiate launch sequence"
            : "激活装置以启动发射程序";

        public static string PanelConfirmHeader => IsEnglish
            ? "WARNING: Launch Sequence Pending"
            : "警告：发射程序待确认";

        public static string PanelConfirmInfo(float remaining) =>
            IsEnglish
                ? $"<color=#ffcc00>Activate again to confirm launch</color>\n" +
                  $"Timeout in {Math.Ceiling(remaining):F0}s will auto-cancel"
                : $"<color=#ffcc00>再次激活以确认发射</color>\n" +
                  $"逾时（{Math.Ceiling(remaining):F0}秒）将自动取消";

        public static string PanelConfirmFooter => IsEnglish
            ? "This action is irreversible — confirm with caution"
            : "操作不可逆，请谨慎确认";

        public static string PanelLockedHeader => IsEnglish ? "Identity Reset" : "身份已重置";

        public static string PanelLockedInfo => IsEnglish
            ? "You have been detached from your original team\nStatus: [ LONE OPERATIVE ]"
            : "你已脱离原有阵营\n状态：[ 孤立行动者 ]";

        public static string PanelLockedStatus => IsEnglish ? "Everyone is your enemy" : "所有人都是你的敌人";

        public static string PanelLockedFooter => IsEnglish
            ? "Survive to complete the launch sequence"
            : "达成目标以完成发射程序";

        public static string PanelCountingInfo(bool pointOfNoReturn) =>
            IsEnglish
                ? pointOfNoReturn
                    ? "<color=#ff3b3b><b>Point of no return crossed — cannot reverse</b></color>"
                    : "<color=#ffcc00>Launch sequence in progress</color>"
                : pointOfNoReturn
                    ? "<color=#ff3b3b><b>已越过不可逆点，无法逆转</b></color>"
                    : "<color=#ffcc00>发射程序执行中</color>";

        public static string PanelCountingStatus(float remaining) =>
            IsEnglish
                ? $"<color=#ffcc00><b>{remaining:F0}</b></color> seconds until detonation"
                : $"<color=#ffcc00><b>{remaining:F0}</b></color> 秒后引爆";

        public static string PanelCountingFooter => IsEnglish ? "Survive until detonation" : "存活至发射完成";

        public static string PanelTipPrefix => IsEnglish ? "TIP" : "操作提示";

        // ------------------- owinfo command -------------------

        public static string OwInfoDenied => IsEnglish
            ? "You do not have permission to use this command."
            : "您没有权限使用此命令。";

        public static string OwInfoHeader => IsEnglish
            ? "<color=#4aa3ff><b>[ OMEGA WARHEAD ]</b></color>"
            : "<color=#4aa3ff><b>[ OMEGA核弹 ]</b></color>";

        public static string OwInfoLastUpdate => IsEnglish ? "Last update:" : "上次更新：";

        public static string OwInfoTotalLaunches => IsEnglish ? "Total launches:" : "累计发射：";

        public static string OwInfoSha256 => IsEnglish ? "SHA-256:" : "SHA-256：";
    }
}

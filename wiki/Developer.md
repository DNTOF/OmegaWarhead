# 开发者文档

本页面向**希望了解或扩展 OmegaWarhead 的插件开发者**。

---

## 技术栈

| 组件 | 说明 |
|---|---|
| 语言 | C# (.NET Framework 4.8) |
| 框架 | EXILED 9.x（SCP:SL 14.2.x） |
| UI 系统 | HintServiceMeow (HSM) |
| 自定义物品 | Exiled.CustomItems |
| 协程 | MEC（Exiled 内置） |

---

## 项目结构

```
OmegaWarhead/
├── OmegaWarheadPlugin.cs      # 插件入口（Plugin<Config>）
├── Configs/
│   ├── Config.cs              # 插件配置（is_enabled / debug / lang）
│   ├── Constants.cs           # 硬编码游戏数值
│   └── Localization.cs        # 中英双语文本集中管理
├── Core/
│   ├── NukeSession.cs         # 会话数据载体 + 状态枚举
│   ├── NukeSessionManager.cs  # 状态机核心（单例）
│   ├── SpawnPointSelector.cs  # 元素随机房间刷新
│   ├── TrackingBroadcaster.cs # 全服位置追踪广播
│   ├── AutoUpdater.cs         # 自动更新
│   └── StatsTracker.cs        # 持久化统计
├── Items/
│   ├── RadioactiveElement.cs  # 放射性元素（CustomItem, ID 10001）
│   └── WarheadController.cs   # 核弹控制器（CustomItem, ID 10002）
├── UI/
│   └── NukePanel.cs           # HSM 四层面板
└── Commands/
    └── OwInfoCommand.cs       # 管理信息查询指令
```

---

## 核心架构：状态机

插件核心是一个五状态状态机，由 `NukeSessionManager`（单例）管理：

```
Idle ──(按K)──▶ Confirming ──(窗口内按K)──▶ Locked ──(角色重置)──▶ Counting ──(归零)──▶ Detonation
                  │
                  └──(超时)──▶ Idle
```

| 状态 | 含义 |
|---|---|
| `Idle` | 持有控制器未激活 |
| `Confirming` | 已按一次 K，等待窗口内二次确认 |
| `Locked` | 二次确认成功，角色重置为 Tutorial |
| `Counting` | 倒计时运行中（268s） |
| `Detonation` | 已引爆，执行全局击杀 |

**分支规则**：
- `Counting` + 未到不可逆点 + 开核者死亡 → 会话销毁，发射中止
- `Counting` + 已到不可逆点 + 开核者死亡 → 不拦截，继续引爆

---

## 事件订阅

插件订阅的 EXILED 事件：

| 事件 | 用途 |
|---|---|
| `Server.RoundStarted` | 启动元素刷新循环 |
| `Server.RoundEnded` / `RestartingRound` | 清理会话与协程 |
| `Player.Dying` | 开核者死亡判定 |
| `Player.Left` | 开核者掉线判定 |
| `Player.PickingUpItem` / `DroppingItem` | 维护元素持有计数 |
| `RadioactiveElement.CountChanged` | 合成阈值检测（自定义事件） |

> 🔧 插件内部通过 `RadioactiveElement.CountChanged` 自定义事件解耦"计数维护"与"合成判定"——扩展时优先复用该事件，不要重新扫描背包。

---

## 多语言机制

所有玩家可见文本集中在 `Configs/Localization.cs`，通过 `Localization.IsEnglish` 布尔值切换。

```csharp
// 添加新文本的示例
public static string NewMessage => IsEnglish ? "English text" : "中文文本";
```

**规则**：
- 新增玩家可见文本**必须**走 Localization，禁止硬编码字符串
- 物品描述**≤20 字**（中文）或 **20 词**（英文），过长会在检视界面重叠
- CASSIE 播报字符串同时是语音内容与字幕，保持短语化（如 `"WARNING . OMEGA WARHEAD COMPONENT ASSEMBLED"`）

---

## 硬编码数值

`Configs/Constants.cs` 集中管理所有游戏平衡数值（常量定义，不在此展示具体值）。

**为什么硬编码？** 防篡改——服务器管理员无法通过 config.yml 修改数值，避免不公平。需要可配置版本请联系作者。

---

## 自动更新机制

`Core/AutoUpdater.cs`：

1. 插件启用时启动**后台线程**（不阻塞加载）
2. 请求 GitHub Releases API 获取最新版
3. 对比 `OmegaWarheadPlugin.Instance.Version`
4. 发现新版本 → 下载 DLL 到临时文件 → 原子覆盖插件文件
5. 下次重启生效

**注意**：更新失败永远只记录日志，绝不中断插件运行。

---

## 本地构建

```bash
git clone https://github.com/DNTOF/OmegaWarhead.git
cd OmegaWarhead
dotnet restore
dotnet build -c Release
```

依赖说明：
- 游戏 DLL（`Assembly-CSharp-firstpass.dll`、`UnityEngine.CoreModule.dll`）位于 `lib/` 目录
- `Exiled.CustomItems.dll` 同样位于 `lib/`（可替换为你服务器的版本）
- 建议使用 Visual Studio 2022 或 Rider 开发

---

## 贡献指南

- 代码风格：与现有代码保持一致（XML 文档注释 + 中文注释风格）
- 所有玩家可见文本走 `Localization.cs`
- 提 PR 前确保 `dotnet build` 无错误

> ⚠️ **版权声明**：本项目为 **DNT_OF** 原创，采用 GPLv3 许可证。**仅允许 DNT_OF 本人及其授权的渠道发布**（授权渠道以作者 Bilibili 账号 [@DNT_OF](https://space.bilibili.com/3493125592975851) 发布为准），严禁非授权渠道倒卖本插件或修改版本（SCP:SL 圈子存在抄袭倒卖现象，作者保留追究权利）。官方源码：https://github.com/DNTOF/OmegaWarhead

---

## 相关页面

- [🏠 返回主页](Home)
- [📦 安装指南](Installation)
- [⚙️ 配置说明](Configuration)
- [🖥️ 管理员指南](Administration)
- [🎮 游戏玩法](Gameplay)
- [❓ 常见问题](FAQ)

---

*☢️ OmegaWarhead Wiki — © 2026 DNT_OF · 仅限作者本人及授权渠道发布 · [Bilibili](https://space.bilibili.com/3493125592975851) · [GitHub](https://github.com/DNTOF/OmegaWarhead)*

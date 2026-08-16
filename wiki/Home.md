# OmegaWarhead Wiki

欢迎来到 **OmegaWarhead**（OMEGA 核弹发射控制器）的官方 Wiki！

OmegaWarhead 是一个基于 [EXILED](https://github.com/ExMod-Team/EXILED) 框架的 SCP:SL 插件，将原版的 Alpha 核弹机制替换为一套**多阶段、玩家驱动的核弹发射系统**，灵感来自《使命召唤：战区》的 **Champions Quest（冠军任务）**。

---

## 快速导航

| 面向人群 | 页面 |
|---|---|
| 👤 **所有玩家** | [游戏玩法指南](Gameplay) |
| 🖥️ **服务器管理员** | [安装指南](Installation) · [配置说明](Configuration) · [管理员指南](Administration) |
| 🧑‍💻 **插件开发者** | [开发者文档](Developer) |
| ❓ **遇到问题？** | [常见问题 FAQ](FAQ) |

---

## 核心概念速览

OmegaWarhead 与普通核弹的最大区别：**核弹不是开关，而是一场任务**。

```
收集元素 → 合成控制器 → 双重确认 → 倒计时存活 → 全服引爆
```

- 玩家需要在地图各处**收集放射性元素**（同时存在上限 6 个）
- 集齐 5 个后自动**合成核弹发射控制器**
- 按下按键（默认 **K**）进入**双重确认流程**
- 确认后身份重置为 Tutorial（教程模式角色），**全服都能看到你的位置**，倒计时 268 秒
- 越过**不可逆点**（剩余 10 秒）后，即使你死了核弹也会照常引爆

---

## 语言

| 语言 | 配置值 | 说明 |
|---|---|---|
| 简体中文 | `zh` | 默认语言 |
| English | `en` | 英文界面 |

在 `config.yml` 中设置 `lang` 即可切换。所有游戏内文本（面板、广播、CASSIE 字幕、物品描述）都会跟随切换。

---

## 版本记录

| 版本 | 说明 |
|---|---|
| v1.0.1-trp | 翻译补丁：新增中英语言切换 + CASSIE 字幕 |
| v1.0.1 | 自动更新、管理数据查询、数值加固、DNT_OF 标识 |
| v1.0.0 | 首个正式版 |

---

## 相关页面

- [📦 安装指南](Installation)
- [🎮 游戏玩法](Gameplay)
- [⚙️ 配置说明](Configuration)
- [🖥️ 管理员指南](Administration)
- [🧑‍💻 开发者文档](Developer)
- [❓ 常见问题](FAQ)

---

> ⚠️ **防盗版声明**：本插件为 **DNT_OF** 的原创作品，采用 **GPLv3** 许可证。**仅允许 DNT_OF 本人及其授权的其他渠道发布本插件**（授权渠道以作者 Bilibili 账号 [@DNT_OF](https://space.bilibili.com/3493125592975851) 发布为准）。除此之外的任何渠道发布、倒卖本插件（或其修改版本）均为侵权，违者将被追究。SCP:SL 模组圈长期存在抄袭和倒卖现象——如果你看到本插件被非授权渠道售卖，请举报。官方源码：https://github.com/DNTOF/OmegaWarhead

*☢️ OmegaWarhead Wiki — © 2026 DNT_OF · 仅限作者本人及授权渠道发布 · [Bilibili](https://space.bilibili.com/3493125592975851) · [GitHub](https://github.com/DNTOF/OmegaWarhead)*

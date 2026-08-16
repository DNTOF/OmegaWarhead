# 安装指南

本页面向**服务器管理员**，介绍如何安装和部署 OmegaWarhead。

---

## 环境要求

| 依赖 | 版本 | 说明 |
|---|---|---|
| SCP: Secret Laboratory 专用服务器 | 14.2.x 及以上 | 游戏服务器本体 |
| [EXILED](https://github.com/ExMod-Team/EXILED/releases) | 9.0.0+ | SCP:SL 插件框架（必须） |
| [HintServiceMeow](https://github.com/MeowServer/HintServiceMeow) | 最新版 | 自定义 UI 提示系统（必须） |

> ⚠️ **HintServiceMeow 是硬性依赖**。没有它，插件的 UI 面板和追踪广播将无法显示，但插件仍会尝试运行。请务必先安装。

---

## 安装步骤

### 第 1 步：安装 EXILED

如果服务器还没有 EXILED：

1. 下载 [EXILED 安装器](https://github.com/ExMod-Team/EXILED/releases)
2. 按官方指引安装到服务器目录
3. 启动一次服务器，确认 `EXILED` 文件夹生成

### 第 2 步：安装 HintServiceMeow

1. 从 [HintServiceMeow Releases](https://github.com/MeowServer/HintServiceMeow/releases) 下载
2. 将 `HintServiceMeow.dll` 放入 `EXILED/Plugins` 文件夹
3. 同时需要安装其依赖库（见其发布页说明）

### 第 3 步：安装 OmegaWarhead

1. 从 [OmegaWarhead Releases](https://github.com/DNTOF/OmegaWarhead/releases) 下载最新版 `OmegaWarhead.dll`
2. 放入 `EXILED/Plugins` 文件夹
3. 重启服务器（或使用 EXILED 热重载）

### 第 4 步：验证安装

服务器启动后，检查控制台日志应出现：

```
OMEGA Warhead Launch Controller enabled.
[AutoUpdater] Checking for updates...
```

如果出现 `OMEGA Warhead Launch Controller enabling...` 但没有 `enabled`，说明初始化失败，检查依赖是否完整。

---

## 配置文件位置

```
EXILED/Configs/OmegaWarhead/config.yml
```

首次启动时自动生成。详细配置说明见 [配置说明](Configuration)。

---

## 更新插件

### 自动更新（推荐）

插件启用时会**自动检查新版本**并下载覆盖，下次重启生效。无需手动操作。

### 手动更新

1. 下载新版 `OmegaWarhead.dll`
2. 停止服务器（或卸载插件）
3. **覆盖替换** `EXILED/Plugins/OmegaWarhead.dll`
4. 重启服务器

> ⚠️ 自动更新已覆盖时，插件文件可能被锁定，请先完全停止服务器再手动替换。

---

## 卸载插件

1. 停止服务器
2. 删除 `EXILED/Plugins/OmegaWarhead.dll`
3. 可选：删除 `EXILED/Configs/OmegaWarhead/` 配置目录
4. 重启服务器

---

## 常见安装问题

| 问题 | 原因与解决 |
|---|---|
| 日志显示 `TypeInitializationException` | 依赖缺失，检查 HintServiceMeow 是否正确安装 |
| 插件没出现在 `EXILED/Plugins` 列表 | EXILED 版本过低，需 9.0.0+ |
| 重启后配置被重置 | 配置目录写入权限问题，检查 `EXILED/Configs/OmegaWarhead` 可写 |
| 面板不显示但日志正常 | HSM 版本不兼容，升级 HintServiceMeow |

---

## 相关页面

- [🏠 返回主页](Home)
- [⚙️ 配置说明](Configuration)
- [🖥️ 管理员指南](Administration)
- [🎮 游戏玩法](Gameplay)
- [❓ 常见问题](FAQ)

---

*☢️ OmegaWarhead Wiki — © 2026 DNT_OF · 仅限作者本人及授权渠道发布 · [Bilibili](https://space.bilibili.com/3493125592975851) · [GitHub](https://github.com/DNTOF/OmegaWarhead)*

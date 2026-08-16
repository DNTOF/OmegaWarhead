# 配置说明

本页面向**服务器管理员**，介绍 OmegaWarhead 的配置文件结构。

配置文件位置：`EXILED/Configs/OmegaWarhead/config.yml`

---

## 可配置项

OmegaWarhead 只暴露 **3 个**配置项。所有游戏平衡数值（元素数量、倒计时时长、伤害系数等）均**硬编码在程序集中**，无法通过配置文件修改——这是刻意的设计，防止服务器管理员随意改动数值导致不公平。

```yaml
omega_warhead:
  # 是否启用插件
  is_enabled: true
  # 是否输出调试日志
  debug: false
  # 插件语言：zh（简体中文）/ en（英语）
  lang: zh
```

| 选项 | 类型 | 默认值 | 说明 |
|---|---|---|---|
| `is_enabled` | bool | `true` | 是否启用插件 |
| `debug` | bool | `false` | 输出调试日志（排障时开启） |
| `lang` | string | `zh` | 插件语言。`zh` = 简体中文，`en` = 英语 |

---

## 语言切换

`lang` 影响**所有玩家可见文本**：

- ✅ 发射面板 UI（四阶段全部）
- ✅ 位置追踪广播（收集/倒计时）
- ✅ 物品名称与描述（放射性元素、核弹控制器）
- ✅ CASSIE 播报（语音 + 字幕）
- ✅ 击杀原因
- ✅ 合成广播

修改后**重载插件或重启服务器**生效。

---

## 硬编码数值（不可配置）

所有游戏平衡数值（元素数量、倒计时时长、伤害系数、追踪间隔等）均**编译进 DLL**，管理员无法通过配置修改——这是刻意的防篡改设计。需要定制数值请联系作者。

---

## 数据文件

### 统计文件

```
EXILED/Configs/OmegaWarhead/stats.json
```

记录：
- `LastUpdateTime`：插件最近更新时间
- `TotalLaunchCount`：累计 OMEGA 核弹发射次数

该文件由插件自动维护，**不要手动编辑**。

---

## 调试建议

排障时：

1. `debug: true` 开启调试日志
2. 观察日志中的 `[SpawnPointSelector]` 前缀消息（元素刷新详情）
3. 观察 `[NukeSession]` 前缀消息（会话状态流转）
4. 排查完成后**记得关掉 debug**（日志量较大）

---

## 相关页面

- [🏠 返回主页](Home)
- [📦 安装指南](Installation)
- [🖥️ 管理员指南](Administration)
- [🎮 游戏玩法](Gameplay)
- [❓ 常见问题](FAQ)

---

*☢️ OmegaWarhead Wiki — © 2026 DNT_OF · 仅限作者本人及授权渠道发布 · [Bilibili](https://space.bilibili.com/3493125592975851) · [GitHub](https://github.com/DNTOF/OmegaWarhead)*

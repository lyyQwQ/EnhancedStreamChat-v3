# EnhancedStream（弹幕板）Agent 指南

- 范围：适用于 `EnhancedStream_139/` 子树；与根 `AGENTS.md` 冲突时，本文件优先。
- 语言：永远使用中文回复。
- 术语：本模块 = “弹幕板”。

## 构建与检查
- 构建：`dotnet build EnhancedStream_139/EnhancedStreamChat.sln -c Debug`
- 快速语法检查：`./EnhancedStream_139/test-core-syntax.sh` 或 `./test-core-syntax.sh`
  
说明：当前以 `.csproj.user` 中的 `BeatSaberDir/ReferencePath` 为准，已指向 `C:\\Users\\m5689\\BSManager\\BSInstances\\1.40.8`。如需切换实例，请直接修改 `EnhancedStream_139/EnhancedStreamChat.csproj.user` 中的路径。

## 代码风格
- 遵循仓库 `.editorconfig`（4 空格缩进，UTF-8）。
- 启用 nullable，优先依赖注入（Zenject）。
- 新核心类型放在 `Core/<Area>/`，并保持文件名与类型名一致。

## 测试
- 测试或样例位于 `EnhancedStream_139/Tests/`；需要时可创建测试项目并使用 `dotnet test`。

## Archive 约定
- `EnhancedStream_139/archive/` 为参考代码与快照，仅供查阅。
- 不要修改、构建或将其导入活动代码，除非明确要求。

更多通用规范、提交与安全约定，请参见仓库根目录的 `AGENTS.md`。

## 依赖与版本
- 目标游戏：Beat Saber 1.40.8（本分支 `_139` 面向 1.39+，当前以 1.40.8 实例构建/测试）。
- 主要依赖：BSIPA、SiraUtil、BSML、BS_Utils、ChatCore、SongCore。
- manifest：`manifest.json` 的 `gameVersion` 仍为 1.29.1（仅描述用途，不影响运行）。如需生态工具识别，后续可更新为 1.40.8。

## 构建与部署（补充）
- 推荐使用 Release 构建进行日常游玩：
  - `dotnet build EnhancedStream_139/EnhancedStreamChat.sln -c Release --no-restore`
- 避免 Zip 打包占用冲突（可选）：
  - `dotnet build ... -c Release --no-restore /p:DisableZipRelease=True`
- 复制到游戏目录：构建后会尝试复制到 `BeatSaberDir\\Plugins`；若游戏运行中，则写入 `IPA\\Pending\\Plugins`，需重启生效。

## 已知问题与规避
- 启动崩溃（1.40.8）
  - 症状：加载到 `ChatCoreInstance.Create()` 时调用系统浏览器导致 Mono 原生崩溃。
  - 规避：`C:\\Users\\<user>\\AppData\\Local\\.chatcore\\settings.ini` 中设置 `[WebApp] LaunchWebAppOnStartup=False`（必要时 `DisableWebApp=True`）。
  - 日志：
    - BS 实例日志：`<实例>\\Logs\\_latest.log`
    - Unity Player：`%LocalAppData%\\..\\LocalLow\\Hyperbolic Magnetism\\Beat Saber\\Player.log`

## 性能与实现要点
- 布局与重绘
  - 位置更新：已将每次 `OrderBy()+Reverse()` 改为“快照+反转”，降低 O(n log n) 排序开销。
  - 重绘：`SetAllDirty()/ForceMeshUpdate()` 保持谨慎使用，后续可按需节流。
- 图像缓存
  - 已加入上限并修剪：单图缓存最大 800、雪碧图缓存最大 200，防止长会话内存增长。
  - 纹理/精灵创建在主线程执行；加载期设备未就绪时会重试并回落。
- 组件职责
  - `Plugin.cs`：BSIPA 生命周期、Harmony、Zenject 安装、软重启处理。
  - `Installers/ESCInstaller.cs`：核心服务与对象池绑定；`ESCMenuInstaller.cs`：ChatDisplay 作为 ViewController 绑定。
  - `Chat/ChatManager.cs`：ChatCore 启动/事件接入、队列派发。
  - `Chat/ChatDisplay*.cs`：消息构建/渲染、屏幕与容器、队列清理策略。
  - `Chat/ChatImageProvider.cs`：图片下载/解码/缓存/动画注册。
  - `Chat/ESCFontManager.cs`：字体资产加载、Fallback、TMP 字符注册。

## 调试建议
- 推荐使用 Release 构建（Debug 模式包含测试日志横幅）。
- 若遇“菜单加载期卡顿/图片失败显示为文字”，建议在主菜单稳定后再开始渲染（见“后续改进”）。

## 后续改进（规划）
- UiReady 门控：在 Menu 场景就绪后再出队/渲染消息，避免加载期竞争与图片创建失败。
- 图片回填：图片缓存/注册成功后，对屏幕内相关消息进行轻量重建，修正“文字固化”。
- 调度统一：整合主线程调度为单一通道，并增加队列长度监控与降级策略。
- 软重启历史弹幕保留（可配置）：当前软重启会清理屏幕与队列中的历史弹幕，后续增加配置项以选择是否保留并在 UI 就绪后回放。

## 参考与资料
- Deepwiki（已索引）：SiraUtil `Auros/SiraUtil`、BSML `monkeymanboy/BeatSaberMarkupLanguage`、BSML 文档 `monkeymanboy/BSML-Docs`、Harmony `pardeike/Harmony`。
- Deepwiki（索引完成/进行中）：BSIPA `nike4613/BeatSaber-IPA-Reloaded`（已索引）、BS Utils `Kylemc1413/Beat-Saber-Utils`（已索引）、SongCore `Kylemc1413/SongCore`（索引进行中）。

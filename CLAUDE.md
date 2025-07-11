# CLAUDE.md

本文件为 Claude Code (claude.ai/code) 在此代码库中工作时提供指导。

## 代码约定

- **代码提交约定**:
  - 提交代码不要加 "Generated with Claude Code" 相关署名
  - 不要将文档文件（如 CLAUDE.md、README.md 等）添加到 git，除非用户明确要求
  - 只提交与当前任务直接相关的代码文件

- **构建验证约定**:
  - 修改代码后必须执行 `dotnet.exe build` 验证编译通过
  - 如果有编译错误，必须先修复错误再继续其他任务

## 项目概述

EnhancedStreamChat 是一个 Beat Saber 模组，提供富文本聊天集成，支持表情、emoji、徽章和图片。该模组专门针对日文和中文显示进行了优化，通过自定义字体处理来防止文字大小不一致的问题。

### 项目历史
本模组基于 [EnhancedStreamChat v3](./archive/EnhancedStreamChat-v3/) 开发，在原版基础上增加了对 Bilibili 平台的支持。原版 v3 使用了更现代的架构（依赖注入、接口抽象），而当前版本采用了更简化的实现。下一步计划参考 v3 的架构优势对本模组进行重构。

## 核心架构与组件

### 核心组件

1. **Plugin.cs** - 使用 BSIPA 框架的主入口点
   - 初始化 ChatManager 和 ESCFontManager 单例
   - 处理 Harmony 补丁以集成模组
   - 管理插件生命周期 (OnStart, OnEnable, OnDisable)

2. **ChatManager.cs** - 中央聊天协调器
   - 集成 ChatCore 库以支持多平台聊天 (Twitch, Bilibili)
   - 管理消息队列和溢出处理
   - 在 ChatCore 和 ChatDisplay 之间路由消息

3. **ChatDisplay.cs** - UI 渲染和管理
   - 使用 BeatSaberMarkupLanguage (BSML) 构建 UI
   - 管理浮动屏幕定位和渲染
   - 处理消息池化以提升性能
   - 菜单和游戏内状态分别定位

4. **ESCFontManager.cs** - 字体资源管理
   - 从 UserData/FontAssets 加载自定义 TMP 字体资源
   - 管理多语言支持的字体回退链
   - 对日文/中文渲染至关重要

5. **ChatMessageBuilder.cs** - 消息解析和格式化
   - 将原始聊天消息转换为格式化的 TextMeshPro 文本
   - 处理表情/图片替换为精灵图覆盖
   - 管理徽章渲染

### 图形系统

- **EnhancedTextMeshProUGUI.cs** - 扩展的 TextMeshPro 组件
  - 使用字符定位在文本上覆盖图片/表情
  - 管理图片池化以提升性能
  - 处理 emoji 支持的 Unicode 代理对

- **EnhancedImage.cs** - 表情/徽章的自定义图片组件
  - 通过 AnimationStateUpdater 支持动画图片
  - 与 ChatImageProvider 集成实现缓存

### 工具系统

- **MainThreadInvoker.cs** - 线程安全的 Unity 主线程执行
  - 提供同步 Invoke 和异步 InvokeAsync 方法
  - InvokeAsync 返回 Task，支持 await 语法
  - 确保消息按序处理，避免竞态条件
- **ObjectMemoryPool.cs** - 性能优化的对象池
- **ChatImageProvider.cs** - 图片缓存和加载系统

## 关键实现细节

### 消息渲染流程
1. ChatCore 接收消息 → ChatManager 将其排队
2. ChatMessageBuilder 解析并格式化消息
3. ChatDisplay 分配池化的文本组件
4. EnhancedTextMeshProUGUI 渲染文本并覆盖图片
5. 旧消息滚出屏幕时回收到池中

### 字体系统
- 自定义 Unity TextMeshPro SDF 字体必须放置在 `UserData/FontAssets/Main/`
- 字体需要预生成的字符集 (Chinese7000+JP.txt) 以支持 CJK
- 如果缺少字符，系统会通过字体链进行回退

### 线程模型
- ChatCore 事件在后台线程到达
- MainThreadInvoker 将操作排队到 Unity 主线程
- 通过异步队列处理消息溢出

## 当前重构状态（2025-06-16）

### 已完成
- ✅ 核心抽象层：所有接口和数据模型已定义
- ✅ 基础服务层：5个核心服务已实现（配置、解析、渲染、图片、字体）
- ✅ 编译错误修复：项目可以成功构建
- ✅ Zenject 迁移：已完成 75% (ESCFontManager、ChatImageProvider、SharedCoroutineStarter、ChatDisplay)
- ✅ 表情渲染功能：已完全修复，支持 Bilibili 和 Twitch 表情
- ✅ 徽章显示功能：已恢复，包括粉丝牌、荣耀等级等

### 进行中
- 🔄 消息渲染管道集成：新架构的消息传递尚未完全实现
- 🔄 ChatConfig 迁移：从 StreamCore 单例迁移到 Zenject

### 关键改进
- 更清晰的职责分离（每个服务专注单一职责）
- 现代化异步支持（async/await + MainThreadInvoker.InvokeAsync）
- 可扩展的消息解析（ICustomParser + BuildMessageTarget）
- 性能优化（对象池、缓存统计）
- 混合架构支持（保持向后兼容性）

## 常见开发任务

### 测试聊天消息
模组与 ChatCore 集成，因此需要运行 ChatCore 并连接聊天服务。消息流程：
`ChatCore → ChatManager.OnTextMessageReceived → ChatDisplay.CreateMessage`

### 调试字体问题
检查 ESCFontManager 初始化和字体资源加载。字体问题通常表现为：
- 缺失字符（□ 方框）
- 不一致的文字大小
- 文本渲染峰值

### 修改聊天 UI
- 布局配置在 ChatConfig.cs 中
- UI 定位在 ChatDisplay.SetupScreens() 中处理
- BSML 模板在 Chat/ChatDisplay.bsml 中

## 依赖项

- BSIPA 4.2.2+（Beat Saber 模组框架）
- ChatCore 3.0.0-alpha（聊天服务集成）
- BeatSaberMarkupLanguage 1.6.10+（UI 框架）
- BS Utils 1.12.2+（Beat Saber 工具）

## 重要说明

- 模组使用反射访问 Unity 内部（FloatingScreen handle）
- 性能至关重要 - 避免在消息渲染路径中分配内存
- 广泛使用对象池以防止游戏过程中的 GC 峰值
- 模组支持仅 HMD 和桌面+HMD 渲染层

## 相关文档

- [架构重构计划](./ARCHITECTURE_REFACTORING_PLAN.md) - 详细的重构方案和设计
- [重构任务跟踪](./REFACTORING_TASKS.md) - 重构进度和任务清单
- [重构进度文档](./REFACTORING_PROGRESS.md) - 当前重构进度和已完成工作
- [开发工具指南](./DEVELOPMENT_TOOLS.md) - WSL2 环境下的构建和测试工具
- [原版v3参考](./archive/EnhancedStreamChat-v3/CLAUDE.md) - 原版架构参考
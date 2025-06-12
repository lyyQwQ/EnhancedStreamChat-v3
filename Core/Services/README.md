# Core Services

这个目录将包含以下服务的实现：

1. **EventAggregator** - 事件聚合器，用于组件间的解耦通信
2. **ChatConfiguration** - 配置管理器，支持运行时配置更新
3. **MessageParser** - 消息解析器，将ChatCore消息转换为可渲染格式
4. **MessageRenderer** - 消息渲染器，负责消息的异步渲染
5. **ImageProvider** - 图片提供者，管理图片加载和缓存
6. **FontProvider** - 字体提供者，管理字体资源

## 实现策略

由于接口定义较为复杂，我们将采用渐进式实现：

1. 先创建最小可行的存根实现（stub implementations）
2. 确保项目能够编译通过
3. 逐步实现每个服务的具体功能
4. 最后进行集成测试

## 注意事项

- 所有服务都应该是可测试的
- 避免使用单例模式
- 通过依赖注入传递依赖
- 使用事件聚合器进行组件通信
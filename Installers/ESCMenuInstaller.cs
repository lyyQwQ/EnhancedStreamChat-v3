using EnhancedStreamChat.Chat;
using Zenject;

namespace EnhancedStreamChat.Installers
{
    internal class ESCMenuInstaller : MonoInstaller
    {
        public override void InstallBindings()
        {
            // 绑定 ChatDisplay（参考 v3 实现）
            // 使用 BindInterfacesAndSelfTo 确保接口和类本身都被绑定
            // FromNewComponentAsViewController 确保作为 ViewController 正确创建
            // AsSingle 确保单例
            // NonLazy 确保立即创建
            Container.BindInterfacesAndSelfTo<ChatDisplay>()
                .FromNewComponentAsViewController()
                .AsSingle()
                .NonLazy();
                
            Logger.Log.Info("[ESCMenuInstaller] Bound ChatDisplay as ViewController");
        }
    }
}

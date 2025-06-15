using EnhancedStreamChat.Chat;
using EnhancedStreamChat.Core.Interfaces;
using EnhancedStreamChat.Core.Models;
using EnhancedStreamChat.Core.Services;
using EnhancedStreamChat.Graphics;
using EnhancedStreamChat.Utilities;
using UnityEngine;
using Zenject;

namespace EnhancedStreamChat.Installers
{
    /// <summary>
    /// Zenject installer for EnhancedStreamChat core services
    /// </summary>
    public class ESCInstaller : Installer
    {
        // Prefab for EnhancedImage memory pool (参考 v3 实现)
        private GameObject _imagePrefab;
        
        public override void InstallBindings()
        {
            Logger.Log.Info("[ESCInstaller] Starting service bindings...");
            
            // 在 InstallBindings 方法内创建 prefab，确保在正确的时机
            _imagePrefab = new GameObject(
                nameof(EnhancedImage), 
                typeof(RectTransform), 
                typeof(BeatSaberMarkupLanguage.Animations.AnimationStateUpdater), 
                typeof(EnhancedImage)
            );

            // Note: IChatConfiguration will be bound by ChatConfigurationAdapter below
            // Container.Bind<IChatConfiguration>()
            //     .To<ChatConfiguration>()
            //     .AsSingle()
            //     .NonLazy();
            // Logger.Log.Info("[ESCInstaller] Bound IChatConfiguration to ChatConfiguration");

            // Bind core services
            Container.Bind<IMessageParser>()
                .To<MessageParser>()
                .AsSingle();
            Logger.Log.Info("[ESCInstaller] Bound IMessageParser to MessageParser");

            Container.Bind<IMessageRenderer>()
                .To<MessageRenderer>()
                .AsSingle();
            Logger.Log.Info("[ESCInstaller] Bound IMessageRenderer to MessageRenderer");

            Container.Bind<IImageProvider>()
                .To<ImageProvider>()
                .AsSingle();
            Logger.Log.Info("[ESCInstaller] Bound IImageProvider to ImageProvider");

            Container.Bind<IFontProvider>()
                .To<FontProvider>()
                .AsSingle();
            Logger.Log.Info("[ESCInstaller] Bound IFontProvider to FontProvider");

            // Bind ESCFontManager as a Zenject service
            Container.BindInterfacesAndSelfTo<ESCFontManager>()
                .FromNewComponentOnNewGameObject()
                .AsSingle()
                .NonLazy();
            Logger.Log.Info("[ESCInstaller] Bound ESCFontManager as Zenject service");
            
            // Bind ChatImageProvider as a Zenject service (迁移自 PersistentSingleton)
            Container.BindInterfacesAndSelfTo<ChatImageProvider>()
                .FromNewComponentOnNewGameObject()
                .AsSingle()
                .NonLazy();
            Logger.Log.Info("[ESCInstaller] Bound ChatImageProvider as Zenject service");

            // Bind memory pool for RenderableMessage (使用内部 Pool 类)
            Container.BindMemoryPool<RenderableMessage, RenderableMessage.Pool>()
                .WithInitialSize(50)
                .WithMaxSize(200)
                .ExpandByDoubling();
            Logger.Log.Info("[ESCInstaller] Bound IMemoryPool<RenderableMessage> with initial size 50, max size 200");

            // Bind ObjectMemoryPool for general object pooling
            Container.Bind(typeof(ObjectMemoryPool<>))
                .To(typeof(ObjectMemoryPool<>))
                .AsTransient();
            Logger.Log.Info("[ESCInstaller] Bound ObjectMemoryPool<T> as transient");

            // Bind memory pool for EnhancedImage (使用 prefab，参考 v3)
            Container.BindMemoryPool<EnhancedImage, EnhancedImage.Pool>()
                .WithInitialSize(50)
                .WithMaxSize(500)
                .ExpandByDoubling()
                .FromComponentInNewPrefab(_imagePrefab);
            Logger.Log.Info("[ESCInstaller] Bound IMemoryPool<EnhancedImage> with initial size 50, max size 500");
            
            // TODO: Add more memory pools from v3 architecture when needed
            // Container.BindMemoryPool<EnhancedImageInfo, EnhancedImageInfo.Pool>()
            //     .WithInitialSize(20);
            // Container.BindMemoryPool<EnhancedTextMeshProUGUIWithBackground, EnhancedTextMeshProUGUIWithBackground.Pool>()
            //     .WithInitialSize(64);

            // Bind adapters to bridge legacy and new architecture
            Container.BindInterfacesAndSelfTo<Chat.Adapters.ChatManagerAdapter>()
                .AsSingle()
                .NonLazy();
            Logger.Log.Info("[ESCInstaller] Bound ChatManagerAdapter");
            
            Container.BindInterfacesAndSelfTo<Adapters.ChatDisplayAdapter>()
                .AsSingle()
                .NonLazy();
            Logger.Log.Info("[ESCInstaller] Bound ChatDisplayAdapter");
            
            // ChatConfigurationAdapter removed - using ChatConfig singleton directly
            
            // Bind rendering adapters
            Container.BindInterfacesAndSelfTo<Chat.Adapters.Rendering.ChatMessageBuilderAdapter>()
                .AsSingle()
                .NonLazy();
            Logger.Log.Info("[ESCInstaller] Bound ChatMessageBuilderAdapter");
            
            // 只绑定类本身，不绑定接口，因为 IImageProvider 已经由 ImageProvider 实现
            Container.Bind<Chat.Adapters.Rendering.ChatImageProviderAdapter>()
                .AsSingle()
                .NonLazy();
            Logger.Log.Info("[ESCInstaller] Bound ChatImageProviderAdapter (self only)");

#if DEBUG
            // Bind test services
            Container.BindInterfacesAndSelfTo<Tests.GameplayTest>()
                .FromNewComponentOnNewGameObject()
                .AsSingle()
                .NonLazy();
            Logger.Log.Info("[ESCInstaller] Bound GameplayTest (DEBUG mode)");
#endif

            Logger.Log.Info("[ESCInstaller] All services bound successfully");
        }
    }
}
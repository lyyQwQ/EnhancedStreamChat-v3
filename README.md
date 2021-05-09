# 增强直播聊天 EnhancedStreamChat v3

增强直播聊天 Enhanced Stream Chat 是节奏光剑Beat Saber中一个富文本聊天集成mod。支持表情，emojis，勋章，和其他聊天信息中包含的受支持第三方图片服务。

# 关于该MOD  

增强直播聊天 Enhanced Stream Chat对日语与中文进行特殊优化，使得文字大小更加稳定。

造成该问题的原因是字体缓存问题，因此可以在Unity中提前创建字体资产来避免这个问题。

如果不在`UserData/FontAsset`中放置字体资产就会调用系统字体并可能造成字体大小不一的问题。



# 基础配置Basic Configuration (for users)

1. 按照[该说明](https://github.com/baoziii/ChatCore-v2#%E5%9F%BA%E6%9C%AC%E8%AE%BE%E7%BD%AE-%E8%8A%82%E5%A5%8F%E5%85%89%E5%89%91mod%E7%94%A8%E6%88%B7)安装聊天核心Chat Core **其他版本的聊天核心Chat Core可能不兼容**
2. 在[这里](https://github.com/baoziii/EnhancedStreamChat-v3/releases)下载增强直播聊天 EnhancedStreamChat v3 **体积较小的zip文件为纯插件，体积较大的zip文件包含插件+思源黑体字体库**
3. 解压压缩包到你的`Beat Saber` 游戏根目录
4. 一旦启动游戏，聊天核心Chat Core设置项网页将会在启动游戏后在你的默认浏览器中打开。你可以在里面配置你的Twitch和Bilibili设置项。

# 其他重要事项

- 这还是个半成品

- 这意味着你可能会发现一些东西漏掉了或者不完整，这可能就是因为没有做完的原因吧。

- 如果你发现任何的bugs (崩溃/卡住) 请提交issue，但是别忘了这插件真没做完

# 如何创建字体资产

1. 在Unity新建一个3D项目。
2. 将你要创建的字体文件(例如`*.ttf`,`*.otf`等)以及[Chinese7000+JP.txt](https://github.com/baoziii/EnhancedStreamChat-v3/blob/denpadokei-dev/Chinese7000+JP.txt)拖入Assets文件夹中
3. 创建SDF
   1. 找到`顶栏`-`Windows`-`TextMeshPro`-`Font Asset Creator`。在弹出的窗口中选择`Import TMP Essential Resources  `(如果没有弹出的话找到`顶栏`-`Windows`-`TextMeshPro`-`Import TMP Essential Resources`)
   2. 将字体文件拖入`Source Font File`，`Sampling Point Size`设置为`Custom Size` `60`，`Padding`设置为6，`Packing Method`设置为`Fast`，`Atlas Resolution`设置为`8192` `8192`，`Character Set`设置为`Characters from File`，将`Chinese7000+JP.txt`文件拖入`Character File`，`Render Mode`设置为`SDFAA`，点击`Generate Font Atlas`
   3. 等进度条跑完后点击底部的`Save`(找不到的话旁边有滑动条)
4. 打包资产
   1. 找到`顶栏`-`Windows`-`Package Manager`，在左侧栏中找到`Asset Bundle Browser`(找不到的话左上角加号右侧的按钮选择`Unity Registry`或者使用右上角搜索框搜索)，点击右下角的`Install`
   2. 安装成功后找到`顶栏`-`Windows`-`AssetBundle Browser`，在弹出窗口的`Configure`选项卡下，将刚才生成的`SDF`结尾文件拖入空白区域。你应该可以看到右侧会自动添加一些文件。
   3. 点击`Build`选项卡-点击`Build`按钮。*等待一小会儿*
   4. 打开`项目目录/AssetBundles/StandaloneWindows`中找到对应`sdf`结尾文件**不是`.manifest`文件**并重命名添加文件扩展名`.assets`
5. 安放字体资产
   1. 安放字体资产的文件夹为`Beat Saber根目录\UserData\FontAssets\Main`(找不到的话自行创建)
   2. 如果该文件内有其他`.assets`文件，将这些文件的文件扩展名更改为其他(比如`.assets1`或者`.assets.bak`)，不然可能无法调用刚生成的字体资产



# EnhancedStreamChat v3

Enhanced Stream Chat is a rich text chat integration mod for Beat Saber. It supports emotes, emojis, badges, and any other image that may come inline in a chat message through any of the supported services.  

# このMODについて  
このEnhancedStreamChatは日本語を受信したときにスパイクを起こさないように日本人向けに改良されたEnhancedStreamChatです。  
スパイクの原因はフォントをキャッシュする動作にあるためあらかじめUnityでFont Assetを作成することで回避しています。  
そのため同梱しているフォント情報をUserDataに置かないとスパイクが発生してしまうので必ずフォントを配置してください。  

また、各自でオリジナルのフォントアセットを作成し読み込むことも可能です。  
詳しくは[こちら](https://github.com/denpadokei/ESCFontProject)  
# Basic Configuration (for users)
1. Follow the instructions at https://github.com/baoziii/ChatCore-v2#%E5%9F%BA%E6%9C%AC%E8%AE%BE%E7%BD%AE-%E8%8A%82%E5%A5%8F%E5%85%89%E5%89%91mod%E7%94%A8%E6%88%B7 **Other edition of Chat Core may not compatible with this mod**
2. Grab the latest EnhancedStreamChat release from https://github.com/baoziii/EnhancedStreamChat-v3/releases **There are two size of zip files: one is only plugin file include(small size), the other one have large file size includes both plugin and font asset(Source Han Sans CN)**
3. Extract the latest EnhancedStreamChat zip to your `Beat Saber` directory
4. Once you launch the game, the ChatCore settings web app will open in your default browser. Use this to login, join/leave channels, and configure various settings.

# Important Stuff
- This is still a WIP
- That means if you find something is missing or incomplete, it's probably because it's not done.
- If you find any bugs (crashes/freezes) make an issue, but again keep in mind this isn't done.
# 3dtest - Unity 多人网络游戏项目

## 项目概述

这是一个基于 Unity 6 的第三人称多人网络游戏项目，使用 KCP 协议进行网络通信，支持多玩家同步。

**项目名称**: 3dtest  
**公司**: DefaultCompany  
**Unity 版本**: Unity 6 (6000.x)  
**渲染管线**: URP (Universal Render Pipeline) 17.3.0

---

## 目录结构

```
Assets/
├── Animations/
│   └── player/
│       └── PlayerController.controller    # 玩家动画控制器
├── Editor/
│   └── NightmareSceneSetup.cs             # 编辑器工具脚本
├── Plugins/
│   ├── kcp/                               # KCP 网络协议库
│   │   └── KCP/
│   │       ├── ByteBuffer.cs
│   │       ├── KCP.cs
│   │       └── UDPSession.cs
│   ├── kcp2k-master/                      # KCP2K 备用库
│   └── Google.Protobuf.dll                # Protobuf 序列化库
├── Prefabs/
│   └── NetworkPlayer.prefab               # 网络玩家预制体
├── Scenes/
│   ├── SampleScene.unity
│   └── NightmareScene.unity
├── Scripts/
│   ├── Protocol/
│   │   └── Game.cs                        # Protobuf 生成的协议定义
│   ├── SimpleThirdPersonController.cs     # 本地玩家控制器
│   ├── NanoKcpClient.cs                   # KCP 网络客户端
│   ├── NetworkPlayerManager.cs            # 远程玩家管理器
│   ├── RemotePlayerController.cs          # 远程玩家控制器
│   ├── NetworkPlayerSpawner.cs            # 玩家生成器
│   ├── NetworkConnectUI.cs                # 网络连接 UI
│   ├── KcpGoClient.cs                     # Go 服务器 KCP 客户端
│   ├── ClientNetworkTransform.cs          # 网络 Transform 同步
│   ├── EnemyAI.cs                         # 敌人 AI
│   ├── GameMenu.cs                        # 游戏菜单
│   ├── MobileUIHandler.cs                 # 移动端 UI 处理
│   └── PlayerAttack.cs                    # 玩家攻击
├── SimpleNaturePack/                      # 自然场景资源包
│   ├── Scenes/
│   │   ├── SimpleNaturePack_Demo.unity    # 主演示场景 (当前活动场景)
│   │   └── SimpleNaturePack_Overview.unity
│   └── Prefabs/                           # 环境预制体 (树、石头、草等)
├── 3d-character_animeGirlAkane/           # 动漫女孩角色资源
├── AnimeGirls/                            # 动漫角色资源
├── True_Horror/                           # 恐怖主题资源
└── Dnk_Dev/                               # 其他资源
```

---

## 核心架构

### 网络层 (KCP Protocol)

项目使用自定义 KCP 网络客户端与 Go 后端服务器通信。

#### NanoKcpClient.cs - 网络核心

```csharp
// 连接配置
host = "127.0.0.1"
port = 3250

// 消息类型
MSG_TYPE_HANDSHAKE = 1      // 握手
MSG_TYPE_HANDSHAKE_ACK = 2  // 握手确认
MSG_TYPE_HEARTBEAT = 3      // 心跳
MSG_TYPE_DATA = 4           // 数据包
MSG_TYPE_KICK = 5           // 踢出

// 主要功能
- Connect()                 // 连接服务器
- SendRequest(route, msg)   // 发送请求 (期望响应)
- SendNotify(route, msg)    // 发送通知 (无响应)
- RegisterHandler(route)    // 注册消息处理器
```

#### 消息路由

**发送方向 (Client → Server)**:
- `room.join` - 加入房间
- `room.move` - 移动同步
- `room.message` - 聊天消息

**接收方向 (Server → Client Push)**:
- `OnSelfJoin` - 自己加入成功 (含 PlayerState, 用于获取自身 ID)
- `OnPlayerMove` - 玩家移动推送
- `OnPlayerJoin` - 玩家加入推送
- `OnPlayerLeave` - 玩家离开推送
- `OnPlayerEnterAOI` - 玩家进入视野
- `OnPlayerLeaveAOI` - 玩家离开视野

### 玩家控制

#### SimpleThirdPersonController.cs - 本地玩家

- **移动**: WASD / 方向键 / 手柄左摇杆
- **奔跑**: Left Shift / 手柄摇杆按下
- **跳跃**: Space / 手柄 A 键
- **调试**: 按 8 播放死亡动画, 按 9 播放跳舞动画
- **同步频率**: 20Hz (每 50ms 发送一次位置)

```csharp
// 移动参数
moveSpeed = 5f
runSpeed = 8f
rotationSpeed = 0.12f
gravity = 20f
jumpForce = 1.5f

// 安全出生点
safePos = Vector3(82f, 15f, -50f)
```

#### RemotePlayerController.cs - 远程玩家

- 接收服务器推送的位置/旋转
- 使用 Lerp/Slerp 平滑插值
- 插值速度: `Time.deltaTime * 10f`

### 玩家管理

#### NetworkPlayerManager.cs

```csharp
// 配置
roomId = "lobby"          // 自动加入的房间 ID
playerName = "UnityPlayer" // 玩家名称
autoJoinOnConnect = true   // 连接后自动加入房间

// 职责
- KCP 连接后自动加入房间
- 通过 OnSelfJoin 获取自身玩家 ID
- 监听 AOI 事件，生成/销毁远程玩家
- 管理远程玩家字典 _remotePlayers

// 完整流程
1. KCP 连接成功 → 自动发送 JoinRoom 请求
2. 收到 OnSelfJoin → 保存 myPlayerId
3. 收到 OnPlayerEnterAOI / OnPlayerMove → 生成远程玩家
4. 实例化 playerPrefab → 移除本地控制和 Netcode 组件
5. 添加 RemotePlayerController (Lerp 插值)
6. 收到 OnPlayerLeave / OnPlayerLeaveAOI → 销毁远程玩家
```

---

## 依赖包

### Unity Package Manager

```json
{
  "com.unity.inputsystem": "1.18.0",           // 新输入系统
  "com.unity.netcode.gameobjects": "1.11.0",   // Netcode (可选网络方案)
  "com.unity.ai.navigation": "2.0.10",         // AI 导航
  "com.unity.render-pipelines.universal": "17.3.0", // URP
  "com.coplaydev.unity-mcp": "..."             // Unity MCP 集成
}
```

### 外部依赖

- **Google.Protobuf.dll** - Protocol Buffers 序列化
- **KCP** - 可靠 UDP 协议实现

---

## 场景列表

| 场景 | 路径 | 用途 |
|------|------|------|
| SimpleNaturePack_Demo | `SimpleNaturePack/Scenes/` | **主场景** - 自然环境演示 |
| NightmareScene | `Scenes/` | 恐怖场景 |
| SampleScene | `Scenes/` | 示例场景 |

---

## 协议定义 (Protobuf)

```protobuf
// Assets/Scripts/Protocol/Game.cs (由 .proto 生成)

message JoinRequest { string name = 1; }
message JoinResponse { int32 code = 1; string room_id = 2; }
message MoveRequest { Vector3 position = 1; Quaternion rotation = 2; }
message ChatMessage { string sender_id = 1; string content = 2; }
message PlayerMovePush { string id = 1; Vector3 position = 2; Quaternion rotation = 3; }
message PlayerJoinPush { string id = 1; string name = 2; }
message PlayerLeavePush { string id = 1; }
message PlayerState { string id = 1; Vector3 position = 2; Quaternion rotation = 3; }
```

---

## 动画系统

### PlayerController.controller

支持的动画参数:
- `Speed` (float) - 移动速度 (0-1)
- `isWalk` (bool) - 是否行走 (兼容旧版)
- `Jump` (trigger) - 跳跃触发
- `Dead` (trigger) - 死亡触发
- `Dance` (trigger) - 跳舞触发

---

## 编辑器工具

### NightmareSceneSetup.cs

位于 `Assets/Editor/` 的场景初始化工具，通过菜单 **TraeTools > Setup Nightmare Scene** 调用。

#### 功能概述

一键设置完整的游戏场景，包括玩家、相机、网络、移动端控制等。

#### 执行流程

```
1. 场景管理
   └── 打开 SimpleNaturePack_Demo.unity

2. 材质修复
   └── 将所有材质升级到 URP/Lit

3. 清理旧对象
   └── 删除: Akane_Player, FPSController, Main Camera, EventSystem 等

4. 环境设置
   ├── 调暗光照 (ambientIntensity = 0.2)
   └── 缩放世界 (WorldRoot x20)

5. 玩家设置
   ├── 实例化 Casual1.prefab
   ├── 添加 CharacterController (center=0.75, height=1.5, radius=0.3)
   ├── 添加 SimpleThirdPersonController
   └── 出生点: Vector3(82, 15, -50)

6. 动画控制器
   ├── 创建 PlayerController.controller
   ├── 参数: Speed, Jump, Attack, Dead, Dance, isWalk
   ├── 状态: Idle, Run, Jump, Attack, Dead, Dance
   └── 动画文件: idle.fbx, run.fbx, jump.fbx, attack.fbx, dead0.fbx, dance1.fbx

7. 相机设置
   ├── 创建 Main Camera
   └── 添加 ThirdPersonCamera (distance=3.5, height=1.6)

8. 武器设置
   ├── 加载 P_KitchenRustyKnife.prefab
   ├── 创建拾取物 Rusty_Knife_Pickup
   └── 添加 WeaponPickup 脚本

9. 敌人设置
   ├── 实例化 pumpkin_king.fbx
   ├── 添加 NavMeshAgent (speed=3.5)
   └── 添加 EnemyAI

10. 网络设置
    ├── 添加 NetworkObject, NetworkTransform
    ├── 创建 NetworkPlayer.prefab
    ├── 创建 NetworkManager + UnityTransport
    ├── 创建 PlayerSpawner
    ├── 创建 NanoKcpClient
    └── 创建 NetworkPlayerManager (远程玩家管理, 自动加入房间)

11. 移动端控制
    ├── 创建 MobileControlsCanvas
    ├── OnScreenStick (左下角虚拟摇杆)
    ├── OnScreenTouchpad (右侧触控区)
    └── 按钮: Attack(红), Jump(绿), Run(蓝)

12. 保存 & 构建设置
    └── 添加场景到 Build Settings
```

#### 关键方法

| 方法 | 功能 |
|------|------|
| `SetupScene()` | 主入口，执行全部设置 |
| `FixMaterials()` | 修复所有渲染器材质 |
| `UpgradeMaterialToURP()` | 将材质升级到 URP/Lit |
| `ResizeWorld()` | 创建 WorldRoot 并缩放 x20 |
| `SetupNetwork()` | 配置网络组件、预制体和 NetworkPlayerManager |
| `SetupMobileControls()` | 创建移动端虚拟控制 |
| `CreateSpawnerScript()` | 生成 NetworkPlayerSpawner.cs |
| `CreateNanoKcpClientScript()` | 生成 NanoKcpClient.cs (如不存在) |
| `SetFBXLoopTime()` | 设置 FBX 动画循环属性 |
| `LoadAnimationClip()` | 从 FBX 加载动画剪辑 |

#### 动画文件配置

| 文件 | 循环 | 用途 |
|------|------|------|
| idle.fbx | ✓ | 待机动画 |
| run.fbx | ✓ | 奔跑动画 |
| jump.fbx | ✗ | 跳跃动画 |
| attack.fbx | ✗ | 攻击动画 |
| dead0.fbx | ✗ | 死亡动画 |
| dance1.fbx | ✓ | 跳舞动画 |

#### 使用方式

```
Unity Editor 菜单栏 → TraeTools → Setup Nightmare Scene
```

---

## 开发指南

### 添加新的网络消息

1. 在 `.proto` 文件中定义消息
2. 生成 C# 代码到 `Assets/Scripts/Protocol/Game.cs`
3. 在 `NanoKcpClient` 中注册处理器:

```csharp
kcpClient.RegisterHandler("OnNewMessage", (data) => {
    var msg = NewMessage.Parser.ParseFrom(data);
    // 处理消息
});
```

### 调试技巧

- 游戏内按 K 键可向上飞行 (调试用)
- GUI 显示玩家调试信息 (位置、是否落地)
- 点击 "传送回地面" 按钮可重置位置

### 已知问题

- Time.timeScale 可能被意外设为 0，控制器会自动修复
- 远程玩家需要移除 AudioListener 和 Camera 避免冲突

---

## 服务器配套

此项目需要配套的 Go KCP 服务器运行在 `kcp-server` 目录。

服务器地址: `127.0.0.1:3250` (KCP UDP)

---

## 文件统计

- **总文件数**: ~571 (Assets 目录)
- **脚本数**: 54 个 C# 文件
- **场景数**: 10 个
- **预制体数**: 29 个
- **动画控制器**: 4 个

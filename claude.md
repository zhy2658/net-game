# 3dtest - Unity 多人网络游戏项目

## 项目概述

基于 Unity 6 的第三人称多人网络游戏，使用 KCP 协议通信，Protobuf 序列化，支持多玩家位置/旋转/动画同步。

**Unity 版本**: Unity 6 (6000.x)  
**渲染管线**: URP 17.3.0  
**服务器**: Go KCP Server (`kcp-server` 目录, 端口 3250)

---

## 目录结构

```
Assets/
├── Animations/player/
│   └── PlayerController.controller      # 玩家动画状态机
├── Plugins/
│   ├── kcp/KCP/                         # KCP 可靠 UDP 协议库
│   │   ├── ByteBuffer.cs
│   │   ├── KCP.cs
│   │   └── UDPSession.cs
│   ├── kcp2k-master/                    # KCP2K 备用库
│   └── Google.Protobuf.dll              # Protobuf 序列化
├── Prefabs/
│   └── NetworkPlayer.prefab             # 网络玩家预制体
├── Scripts/
│   ├── Protocol/Game.cs                 # Protobuf 生成的协议定义
│   ├── SimpleThirdPersonController.cs   # 本地玩家控制器
│   ├── RemotePlayerController.cs        # 远程玩家控制器 (动画同步)
│   ├── NetworkPlayerManager.cs          # 远程玩家生命周期管理
│   ├── NanoKcpClient.cs                 # KCP 网络客户端
│   ├── NetworkConnectUI.cs              # 连接按钮 UI
│   ├── ThirdPersonCamera.cs             # 第三人称相机
│   ├── GameMenu.cs                      # ESC 暂停菜单
│   ├── GameConstants.cs                 # 全局常量
│   ├── AnimatorUtils.cs                 # Animator 参数安全检查
│   ├── MobileUIHandler.cs              # 移动端控件开关
│   ├── EnemyAI.cs                       # 敌人 AI (NavMesh)
│   ├── PlayerAttack.cs                  # 空壳 (待实现)
│   └── WeaponPickup.cs                  # 空壳 (待实现)
└── SimpleNaturePack/
    └── Scenes/
        └── SimpleNaturePack_Demo.unity  # 主场景
```

---

## 核心架构

### 网络层 (KCP)

#### NanoKcpClient.cs

```
连接: 127.0.0.1:3250 (可配置)
心跳: 5s 间隔
重连: 指数退避, 最多 10 次, 最大延迟 30s

消息类型:
  1 = Handshake      2 = HandshakeAck
  3 = Heartbeat      4 = Data
  5 = Kick

API:
  Connect()                         → 连接服务器
  SendRequest(route, msg, cb?)      → 发送请求 (带可选回调)
  SendNotify(route, msg)            → 发送通知 (无回调)
  RegisterHandler(route, handler)   → 注册推送处理器
  UnregisterHandler(route)          → 取消注册
```

#### 消息路由

| 方向 | 路由 | 说明 |
|------|------|------|
| C→S | `room.join` | 加入房间 |
| C→S | `room.move` | 移动同步 (位置+旋转+速度+着地) |
| S→C | `OnSelfJoin` | 自己加入成功，获取玩家 ID |
| S→C | `OnPlayerMove` | 远程玩家移动推送 |
| S→C | `OnPlayerJoin` | 玩家加入通知 |
| S→C | `OnPlayerLeave` | 玩家离开通知 |
| S→C | `OnPlayerEnterAOI` | 玩家进入视野 |
| S→C | `OnPlayerLeaveAOI` | 玩家离开视野 |

### 协议定义 (Protobuf)

```protobuf
message MoveRequest {
  Vector3    position         = 1;
  Quaternion rotation         = 2;
  float      speed            = 3;   // 归一化速度 0~1
  bool       is_grounded      = 4;   // 是否在地面
  int64      client_timestamp = 5;   // 客户端时间戳 ms
}

message PlayerMovePush {
  string     id          = 1;
  Vector3    position    = 2;
  Quaternion rotation    = 3;
  float      speed       = 4;
  bool       is_grounded = 5;
}

message JoinRequest    { string name = 1; }
message JoinResponse   { int32 code = 1; string room_id = 2; }
message ChatMessage    { string sender_id = 1; string content = 2; }
message PlayerJoinPush { string id = 1; string name = 2; }
message PlayerLeavePush{ string id = 1; }
message PlayerState    { string id = 1; Vector3 position = 2; Quaternion rotation = 3; }
```

---

## 玩家系统

### SimpleThirdPersonController.cs - 本地玩家

| 参数 | 值 | 说明 |
|------|----|------|
| moveSpeed | 5 | 行走速度 |
| runSpeed | 8 | 奔跑速度 |
| rotationSpeed | 0.12 | 转向平滑时间 |
| gravity | 20 | 重力 |
| jumpForce | 1.5 | 跳跃力度 |
| 同步频率 | 20Hz | 每 50ms 发送一次 |
| 出生点 | (82, 15, -50) | 安全出生位置 |

**操作**: WASD 移动, Shift 奔跑, Space 跳跃, ESC 暂停  
**调试**: K 飞行, 8 死亡动画, 9 跳舞动画  
**着地检测**: Raycast + CharacterController.isGrounded 双重检测  
**网络同步**: 发送归一化速度 (`currentSpeed / runSpeed`)、旋转、着地状态

### RemotePlayerController.cs - 远程玩家

- 位置: `Vector3.Lerp` (速度 10)
- 旋转: `Quaternion.Slerp` (速度 10)
- 动画: 使用服务器推送的 `speed` 值驱动 Animator
- 超时: 2 秒无更新自动归零速度
- 速度平滑: `SmoothDamp` (0.15s)
- 延迟初始化: 每帧检查 Animator 就绪，兼容异步加载

### NetworkPlayerManager.cs - 远程玩家管理

```
流程:
1. Start → 缓存本地玩家的 AnimatorController
2. KCP 连接 → 自动 JoinRoom
3. OnSelfJoin → 保存 myPlayerId
4. OnPlayerEnterAOI / OnPlayerMove → SpawnPlayer
   - 实例化 playerPrefab
   - 销毁 SimpleThirdPersonController, AudioListener, Camera
   - Force-assign AnimatorController (prefab variant 不可靠)
   - 添加 RemotePlayerController
5. OnPlayerLeave / OnPlayerLeaveAOI → 销毁远程玩家
```

---

## 动画系统

### PlayerController.controller

| 参数 | 类型 | 说明 |
|------|------|------|
| Speed | float | 移动速度 0~1 |
| isWalk | bool | 是否行走 (兼容旧版) |
| Jump | trigger | 跳跃 |
| Dead | trigger | 死亡 |
| Dance | trigger | 跳舞 |

### AnimatorUtils.cs

安全检查 Animator 参数是否存在，避免运行时异常。内部检查 `runtimeAnimatorController` 非空。

---

## 其他脚本

| 脚本 | 说明 |
|------|------|
| ThirdPersonCamera.cs | 第三人称相机，鼠标/手柄控制旋转，自动碰撞回避 |
| GameMenu.cs | ESC 暂停/恢复，重启/退出 |
| NetworkConnectUI.cs | 连接按钮，点击后创建/连接 KcpClient |
| MobileUIHandler.cs | 移动平台自动启用触控控件 |
| EnemyAI.cs | NavMesh 敌人 AI (Idle/Patrol/Chase/Attack/Dead)，支持网络同步 |
| GameConstants.cs | 全局常量: 出生点、速度、同步频率、服务器地址 |

---

## 依赖

```json
{
  "com.unity.inputsystem": "1.18.0",
  "com.unity.netcode.gameobjects": "1.11.0",
  "com.unity.ai.navigation": "2.0.10",
  "com.unity.render-pipelines.universal": "17.3.0"
}
```

外部: Google.Protobuf.dll, KCP 协议库

---

## 开发指南

### 添加新网络消息

1. 在 `kcp-server/protocol/game.proto` 定义消息
2. 运行 `protoc` 生成 Go 和 C# 代码
3. 服务器 `room.go` 添加处理逻辑
4. 客户端注册 handler:

```csharp
kcpClient.RegisterHandler("OnNewPush", (data) => {
    var msg = NewPush.Parser.ParseFrom(data);
});
```

### 已知注意事项

- NetworkPlayer prefab variant 的 AnimatorController 在实例化后可能丢失，`NetworkPlayerManager` 通过 force-assign 本地玩家的 controller 解决
- `Time.timeScale` 可能被 GameMenu 暂停设为 0，控制器在 Start 中自动恢复
- 远程玩家需销毁 AudioListener 和 Camera 避免冲突

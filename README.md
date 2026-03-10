# 3dtest - Unity 多人网络游戏

基于 Unity 6 的第三人称多人网络游戏，KCP 协议 + Protobuf 序列化，多玩家位置/旋转/动画同步。

## 快速开始

### 前置条件
- Unity 6 (6000.x)
- Go kcp-server 运行中 (127.0.0.1:3250)

### 运行步骤
1. 打开 Unity 项目，加载 SimpleNaturePack/Scenes/SimpleNaturePack_Demo.unity
2. 启动服务器: `cd ../kcp-server && go run .`
3. 进入 Play 模式 - 自动连接服务器并加入 lobby 房间
4. (可选) 启动机器人: `cd ../kcp-server && go run cmd/bot2/bot2.go`

### 操作说明

| 按键 | 功能 |
|------|------|
| WASD / 方向键 | 移动 |
| Left Shift | 奔跑 |
| Space | 跳跃 |
| ESC | 暂停菜单 |
| K | 飞行 (调试) |
| 8 / 9 | 死亡/跳舞动画 (调试) |

## 架构

- **本地玩家**: SimpleThirdPersonController - 输入/移动/动画/KCP 同步 (20Hz)
- **远程玩家**: RemotePlayerController - 服务器推送位置/旋转/速度插值 + 动画驱动
- **网络层**: NanoKcpClient -> NetworkPlayerManager 路由消息，生成/销毁远程玩家
- **协议**: Protobuf (源文件: ../kcp-server/protocol/game.proto)

### 同步数据

| 字段 | 说明 |
|------|------|
| Position | 世界坐标 (x, y, z) |
| Rotation | 四元数旋转 |
| Speed | 归一化速度 0-1 (驱动动画) |
| IsGrounded | 是否在地面 |

## Bot2 机器人

服务端拟人机器人 (kcp-server/cmd/bot2/bot2.go)，状态机 AI 模拟真人漫游。

```
go run cmd/bot2/bot2.go -name "TestBot" -room "lobby" -addr "127.0.0.1:3250"
```

## Proto 代码生成

在 kcp-server 目录执行:

```
protoc --go_out=. --go_opt=paths=source_relative \
       --csharp_out=../3dtest/Assets/Scripts/Protocol \
       protocol/game.proto
```

## 完整文档

详细架构文档见 claude.md

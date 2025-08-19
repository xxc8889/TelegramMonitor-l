# TelegramMonitor 一键部署指南

## 🚀 超级简单一键部署

### 域名: a.dp888.dpdns.org （已配置A记录）
### 用户: root （您的要求）
### API: 已配置 （您已填写）

## 📋 部署步骤

### 第一步：上传项目
将整个 `TelegramMonitor-l` 文件夹上传到服务器 `/root/` 目录

**上传方式（任选一种）：**
- **WinSCP**: 连接服务器，上传到 `/root/`
- **FileZilla**: FTP上传到 `/root/`
- **scp命令**: `scp -r TelegramMonitor-l root@your-server:/root/`

### 第二步：一键部署
```bash
# SSH连接服务器
ssh root@your-server

# 进入项目目录并执行安装
cd /root/TelegramMonitor-l
chmod +x install.sh
./install.sh
```

### 第三步：完成！
访问：`https://a.dp888.dpdns.org`

## ✨ 一键脚本功能

`install.sh` 脚本将自动完成：
- ✅ 系统更新和基础工具安装
- ✅ Docker 和 .NET SDK 安装
- ✅ 防火墙配置
- ✅ 应用程序构建
- ✅ Nginx 反向代理配置
- ✅ SSL 证书自动申请
- ✅ 系统服务创建（开机自启）
- ✅ 管理脚本创建

---

## 📁 文件说明

| 文件 | 用途 |
|------|------|
| `deploy-server.sh` | 服务器环境一键安装脚本 |
| `build-and-run.sh` | 应用构建和启动脚本 |
| `DEPLOYMENT_GUIDE.md` | 详细部署文档 |
| `src/` | 应用程序源代码 |
| `Dockerfile` | Docker 镜像构建配置 |

---

## 🛠️ 常用命令

### 服务管理
```bash
# 查看状态
docker compose ps

# 查看日志
docker compose logs -f

# 重启服务
docker compose restart

# 停止服务
docker compose down

# 启动服务
docker compose up -d
```

### 备份数据
```bash
# 运行备份脚本
./backup.sh

# 手动备份
cp -r data session ~/backup/
```

### 更新应用
```bash
# 停止服务
docker compose down

# 上传新代码
# 重新构建
./build-and-run.sh
```

---

## 🔧 问题排查

### 构建失败
```bash
# 清理并重新构建
cd src
dotnet clean
dotnet restore
cd ..
./build-and-run.sh
```

### 端口被占用
```bash
# 查看端口占用
sudo netstat -tlnp | grep :5005

# 更改端口（修改 docker-compose.yml）
# 将 "5005:5005" 改为 "8080:5005"
```

### 权限问题
```bash
# 修复权限
sudo chown -R $USER:$USER ~/telegrammonitor
chmod +x ~/telegrammonitor/*.sh
```

---

## 📞 使用说明

1. **Telegram 登录**
   - 访问 Web 界面
   - 输入手机号（格式：+8613812345678）
   - 输入验证码和密码

2. **配置关键词**
   - 点击"关键词管理"
   - 添加要监控的关键词
   - 设置匹配类型和动作

3. **设置目标群组**
   - 获取对话列表
   - 选择接收监控消息的群组
   - 设置为目标

4. **启动监控**
   - 点击"启动监控"
   - 确认状态为"运行中"

---

## 🔐 安全建议

1. **服务器安全**
   - 使用密钥登录
   - 定期更新系统
   - 配置防火墙

2. **应用安全**
   - 不在公网暴露管理端口
   - 定期备份数据
   - 使用 HTTPS（配置 SSL 证书）

3. **API 安全**
   - 妥善保管 API 密钥
   - 不要在公共代码库中暴露密钥

---

## 📖 详细文档

查看 `DEPLOYMENT_GUIDE.md` 获取完整的部署文档，包含：
- 详细的环境配置
- Nginx 反向代理设置
- SSL 证书配置
- 系统服务配置
- 监控和维护

---

## ❓ 常见问题

**Q: 为什么要重新登录 SSH？**
A: 添加用户到 docker 组后需要重新登录才能生效。

**Q: 可以不使用 Docker 部署吗？**
A: 可以，但 Docker 部署更简单且隔离性更好。

**Q: 忘记修改 API 配置怎么办？**
A: 修改后重新运行 `./build-and-run.sh` 即可。

**Q: 如何设置域名访问？**
A: 参考 `DEPLOYMENT_GUIDE.md` 中的 Nginx 配置章节。

---

## 🆘 获取帮助

1. 查看日志：`docker compose logs -f`
2. 检查服务状态：`docker compose ps`
3. 查看详细文档：`DEPLOYMENT_GUIDE.md`
4. 系统日志：`sudo journalctl -f`
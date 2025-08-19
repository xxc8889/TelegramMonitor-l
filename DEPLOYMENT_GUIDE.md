# TelegramMonitor 一键部署指南

## 🚀 超简单部署（3步完成）

### 域名: a.dp888.dpdns.org
### 用户: root

1. **上传项目**：将整个 TelegramMonitor-l 文件夹上传到服务器 `/root/` 目录
2. **执行安装**：`cd /root/TelegramMonitor-l && chmod +x install.sh && ./install.sh`
3. **完成！** 访问 `https://a.dp888.dpdns.org` 开始使用

✅ **全自动部署**：环境安装、应用构建、SSL证书、Nginx配置一键完成

---

## 目录
- [部署前准备](#部署前准备)
- [本地配置编辑](#本地配置编辑)
- [服务器环境准备](#服务器环境准备)
- [项目部署](#项目部署)
- [域名和SSL配置](#域名和ssl配置)
- [使用说明](#使用说明)
- [快速部署脚本](#快速部署脚本)
- [故障排除](#故障排除)

---

## 部署前准备

### 1. 申请 Telegram API 密钥
1. 访问 https://my.telegram.org/apps
2. 使用您的 Telegram 账号登录
3. 创建新应用，获取 `api_id` 和 `api_hash`
4. 记录这两个值，稍后需要配置

### 2. 准备域名（可选）
- 如果要使用域名访问，请准备一个域名
- 将域名 A 记录指向您的服务器 IP

### 3. 服务器要求
- Ubuntu 22.04 64位系统
- 最少 2GB 内存
- 最少 10GB 磁盘空间
- 开放端口：22(SSH)、80(HTTP)、443(HTTPS)、5005(应用)

---

## 本地配置编辑

### 1. 修改 API 配置
编辑 `src/Models/TelegramMonitorConstants.cs` 文件：

```csharp
namespace TelegramMonitor;

public static class TelegramMonitorConstants
{
    public const int ApiId = 您的API_ID;  // 替换为您申请的 API ID
    public const string ApiHash = "您的API_HASH";  // 替换为您申请的 API Hash
    public static readonly string SessionPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "session");
}
```

### 2. 打包源代码
将整个项目文件夹打包上传到服务器：

```bash
# 在项目根目录执行（如果在Windows，可以使用7zip等工具打包）
tar -czf telegrammonitor-source.tar.gz src/ Dockerfile LICENSE

# 或者直接上传整个文件夹到服务器
```

**注意**：只需要修改 API 配置即可，其他配置使用默认值。所有构建工作都在服务器上完成。

---

## 服务器环境准备

### 1. 连接服务器
```bash
ssh root@your-server-ip
# 或
ssh ubuntu@your-server-ip
```

### 2. 更新系统
```bash
sudo apt update && sudo apt upgrade -y
```

### 3. 安装必要工具
```bash
sudo apt install -y curl wget git vim unzip htop
```

### 4. 配置防火墙
```bash
# 安装并配置 UFW
sudo apt install -y ufw

# 配置防火墙规则
sudo ufw allow ssh
sudo ufw allow 80/tcp
sudo ufw allow 443/tcp
sudo ufw allow 5005/tcp
sudo ufw --force enable

# 查看状态
sudo ufw status
```

### 5. 安装 Docker
```bash
# 安装 Docker
curl -fsSL https://get.docker.com -o get-docker.sh
sudo sh get-docker.sh

# 启动 Docker 服务
sudo systemctl start docker
sudo systemctl enable docker

# 将用户添加到 docker 组
sudo usermod -aG docker $USER

# 重新登录使组权限生效
exit
# 重新 SSH 连接
```

### 6. 安装 Docker Compose
```bash
# 安装 Docker Compose
sudo apt install -y docker-compose-plugin

# 验证安装
docker --version
docker compose version
```

### 7. 安装 .NET 9.0 SDK
```bash
# 添加 Microsoft 包仓库
wget https://packages.microsoft.com/config/ubuntu/22.04/packages-microsoft-prod.deb -O packages-microsoft-prod.deb
sudo dpkg -i packages-microsoft-prod.deb
rm packages-microsoft-prod.deb

# 安装 .NET SDK
sudo apt update
sudo apt install -y dotnet-sdk-9.0

# 验证安装
dotnet --version
dotnet --list-sdks

# 设置环境变量
echo 'export DOTNET_ROOT=/usr/lib/dotnet' >> ~/.bashrc
echo 'export PATH=$PATH:/usr/lib/dotnet' >> ~/.bashrc
source ~/.bashrc
```

---

## 项目部署

### 1. 上传项目源代码
```bash
# 在服务器上创建目录
mkdir -p ~/telegrammonitor
cd ~/telegrammonitor

# 上传您的项目源代码（方式任选其一）：

# 方式1：使用 scp 上传打包文件
# scp telegrammonitor-source.tar.gz ubuntu@your-server-ip:~/telegrammonitor/
# tar -xzf telegrammonitor-source.tar.gz

# 方式2：使用 git 克隆（如果代码在git仓库）
# git clone your-repository-url .

# 方式3：直接上传文件夹
# 使用 WinSCP、FileZilla 等工具上传整个项目文件夹
```

### 2. 构建应用程序
```bash
# 确保在项目根目录
cd ~/telegrammonitor

# 构建项目
cd src
dotnet restore
dotnet publish -c Release -r linux-x64 --self-contained -o ../out/linux-x64/

# 验证构建结果
ls -la ../out/linux-x64/
```

### 3. 创建优化的 Dockerfile
```bash
# 回到项目根目录
cd ~/telegrammonitor

# 创建或更新 Dockerfile
cat > Dockerfile << 'EOF'
FROM mcr.microsoft.com/dotnet/runtime-deps:9.0-bookworm-slim

# 安装时区数据
RUN apt-get update && apt-get install -y tzdata && rm -rf /var/lib/apt/lists/*
ENV TZ=Asia/Shanghai

# 创建应用用户
RUN groupadd -r appuser && useradd -r -g appuser appuser

# 设置工作目录
WORKDIR /app

# 复制应用文件
COPY out/linux-x64/ /app/

# 设置权限
RUN chmod +x /app/TelegramMonitor && \
    chown -R appuser:appuser /app && \
    mkdir -p /app/data /app/session /app/logs && \
    chown -R appuser:appuser /app/data /app/session /app/logs

# 切换到应用用户
USER appuser

# 暴露端口
EXPOSE 5005

# 启动应用
ENTRYPOINT ["/app/TelegramMonitor"]
EOF
```

### 4. 创建 Docker Compose 配置
```bash
cat > docker-compose.yml << 'EOF'
version: '3.8'

services:
  telegrammonitor:
    build: .
    container_name: telegrammonitor
    restart: unless-stopped
    ports:
      - "5005:5005"
    volumes:
      - ./data:/app/data
      - ./session:/app/session
      - ./logs:/app/logs
    environment:
      - ASPNETCORE_ENVIRONMENT=Production
      - ASPNETCORE_URLS=http://+:5005
      - TZ=Asia/Shanghai
    networks:
      - telegram-network
    healthcheck:
      test: ["CMD", "curl", "-f", "http://localhost:5005/health"]
      interval: 30s
      timeout: 10s
      retries: 3
      start_period: 40s

networks:
  telegram-network:
    driver: bridge
EOF
```

### 5. 创建必要目录和启动服务
```bash
# 创建数据目录
mkdir -p data session logs
chmod 755 data session logs

# 构建 Docker 镜像
docker compose build

# 启动服务
docker compose up -d

# 查看启动状态
docker compose ps

# 查看服务日志
docker compose logs -f telegrammonitor

# 测试服务是否正常
curl http://localhost:5005
```

### 6. 验证部署
```bash
# 检查容器状态
docker compose ps

# 检查端口监听
sudo netstat -tlnp | grep :5005

# 检查服务响应
curl -I http://localhost:5005

# 如果一切正常，您应该看到HTTP响应
```

---

## 域名和SSL配置

### 1. 安装 Nginx
```bash
sudo apt install -y nginx
```

### 2. 配置 Nginx 反向代理
创建站点配置文件：

```bash
# 替换 your-domain.com 为您的实际域名
sudo tee /etc/nginx/sites-available/telegrammonitor << 'EOF'
server {
    listen 80;
    server_name your-domain.com www.your-domain.com;
    
    # 重定向 HTTP 到 HTTPS
    return 301 https://$server_name$request_uri;
}

server {
    listen 443 ssl http2;
    server_name your-domain.com www.your-domain.com;
    
    # SSL 证书配置（稍后配置）
    # ssl_certificate /etc/letsencrypt/live/your-domain.com/fullchain.pem;
    # ssl_certificate_key /etc/letsencrypt/live/your-domain.com/privkey.pem;
    
    # SSL 安全配置
    ssl_protocols TLSv1.2 TLSv1.3;
    ssl_ciphers ECDHE-RSA-AES128-GCM-SHA256:ECDHE-RSA-AES256-GCM-SHA384;
    ssl_prefer_server_ciphers off;
    ssl_session_cache shared:SSL:10m;
    ssl_session_timeout 10m;
    
    # 安全头
    add_header X-Frame-Options DENY;
    add_header X-Content-Type-Options nosniff;
    add_header X-XSS-Protection "1; mode=block";
    add_header Strict-Transport-Security "max-age=31536000; includeSubDomains" always;
    
    # 代理配置
    location / {
        proxy_pass http://127.0.0.1:5005;
        proxy_http_version 1.1;
        proxy_set_header Upgrade $http_upgrade;
        proxy_set_header Connection keep-alive;
        proxy_set_header Host $host;
        proxy_cache_bypass $http_upgrade;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;
        proxy_set_header X-Real-IP $remote_addr;
        
        # 超时配置
        proxy_connect_timeout 60s;
        proxy_send_timeout 60s;
        proxy_read_timeout 60s;
    }
    
    # 静态文件缓存
    location ~* \.(js|css|png|jpg|jpeg|gif|ico|svg|woff|woff2|ttf|eot)$ {
        proxy_pass http://127.0.0.1:5005;
        expires 1y;
        add_header Cache-Control "public, immutable";
    }
    
    # 禁止访问敏感文件
    location ~ /\. {
        deny all;
    }
}
EOF

# 启用站点
sudo ln -s /etc/nginx/sites-available/telegrammonitor /etc/nginx/sites-enabled/

# 删除默认站点
sudo rm -f /etc/nginx/sites-enabled/default

# 测试配置
sudo nginx -t

# 重启 Nginx
sudo systemctl restart nginx
sudo systemctl enable nginx
```

### 3. 安装 SSL 证书
```bash
# 安装 Certbot
sudo apt install -y certbot python3-certbot-nginx

# 获取 SSL 证书（替换为您的域名）
sudo certbot --nginx -d your-domain.com -d www.your-domain.com

# 设置自动续期
sudo crontab -e
# 添加以下行：
0 12 * * * /usr/bin/certbot renew --quiet && systemctl reload nginx
```

### 4. 如果没有域名，直接使用 IP 访问
修改 Nginx 配置：

```bash
sudo tee /etc/nginx/sites-available/telegrammonitor << 'EOF'
server {
    listen 80;
    server_name _;
    
    location / {
        proxy_pass http://127.0.0.1:5005;
        proxy_http_version 1.1;
        proxy_set_header Upgrade $http_upgrade;
        proxy_set_header Connection keep-alive;
        proxy_set_header Host $host;
        proxy_cache_bypass $http_upgrade;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;
        
        proxy_connect_timeout 60s;
        proxy_send_timeout 60s;
        proxy_read_timeout 60s;
    }
}
EOF

sudo systemctl restart nginx
```

---

## 使用说明

### 1. 首次访问
- 浏览器访问：`https://your-domain.com` 或 `http://your-server-ip`
- 您将看到 TelegramMonitor 的管理界面

### 2. Telegram 登录配置
1. 点击 "Telegram 登录" 或 访问 `/telegram.html`
2. 输入您的 Telegram 手机号码（包含国家代码，如：+8613812345678）
3. 输入收到的验证码
4. 如果有两步验证，输入密码
5. 登录成功后，可以看到登录状态

### 3. 配置监控目标
1. 点击 "获取对话列表" 查看您可以发送消息的群组/频道
2. 选择一个作为监控消息的接收目标
3. 点击 "设置目标" 确认

### 4. 配置关键词
1. 访问 `/keywords.html` 或点击关键词管理
2. 添加关键词配置：
   - **关键词内容**：要监控的关键词
   - **匹配类型**：
     - 全字匹配：完全匹配整个词
     - 包含匹配：消息中包含该词即匹配
     - 正则表达式：使用正则表达式匹配
     - 模糊匹配：用 ? 分隔多个关键词，全部包含才匹配
     - 用户匹配：监控特定用户的消息
   - **执行动作**：监控或排除
   - **文本样式**：匹配时的格式化样式

### 5. 启动监控
1. 返回主页面
2. 点击 "启动监控"
3. 确认监控状态显示为 "运行中"

### 6. 代理配置（可选）
如果需要使用代理访问 Telegram：
1. 在主页面找到代理设置
2. 选择代理类型（SOCKS5 或 MTProxy）
3. 输入代理地址
4. 应用配置

---

## 管理和维护

### 1. 服务管理命令
```bash
# 查看服务状态
docker compose ps

# 查看日志
docker compose logs -f telegrammonitor

# 重启服务
docker compose restart

# 停止服务
docker compose down

# 启动服务
docker compose up -d
```

### 2. 系统服务配置
创建 systemd 服务以确保开机自启：

```bash
sudo tee /etc/systemd/system/telegrammonitor.service << 'EOF'
[Unit]
Description=TelegramMonitor Docker Service
After=docker.service
Requires=docker.service

[Service]
Type=oneshot
RemainAfterExit=yes
User=ubuntu
WorkingDirectory=/home/ubuntu/telegrammonitor
ExecStart=/usr/bin/docker compose up -d
ExecStop=/usr/bin/docker compose down
TimeoutStartSec=0

[Install]
WantedBy=multi-user.target
EOF

# 启用服务
sudo systemctl daemon-reload
sudo systemctl enable telegrammonitor
sudo systemctl start telegrammonitor
```

### 3. 数据备份
创建备份脚本：

```bash
tee ~/backup-telegram.sh << 'EOF'
#!/bin/bash
BACKUP_DIR="/home/ubuntu/backups/telegrammonitor"
DATE=$(date +%Y%m%d_%H%M%S)

mkdir -p "$BACKUP_DIR"

# 停止服务（可选）
# cd /home/ubuntu/telegrammonitor && docker compose down

# 备份数据
cp -r ~/telegrammonitor/data "$BACKUP_DIR/data_$DATE"
cp -r ~/telegrammonitor/session "$BACKUP_DIR/session_$DATE"

# 创建压缩备份
tar -czf "$BACKUP_DIR/telegrammonitor_backup_$DATE.tar.gz" -C ~/telegrammonitor data session

# 清理 30 天前的备份
find "$BACKUP_DIR" -type f -mtime +30 -delete
find "$BACKUP_DIR" -type d -mtime +30 -empty -delete

# 重启服务（可选）
# cd /home/ubuntu/telegrammonitor && docker compose up -d

echo "备份完成: $DATE"
EOF

chmod +x ~/backup-telegram.sh

# 设置定时备份（每天凌晨 2 点）
(crontab -l 2>/dev/null; echo "0 2 * * * /home/ubuntu/backup-telegram.sh") | crontab -
```

### 4. 监控脚本
创建监控脚本：

```bash
tee ~/monitor-telegram.sh << 'EOF'
#!/bin/bash

# 检查服务是否运行
if ! docker compose -f /home/ubuntu/telegrammonitor/docker-compose.yml ps | grep -q "Up"; then
    echo "$(date): TelegramMonitor 服务未运行，正在重启..."
    cd /home/ubuntu/telegrammonitor
    docker compose restart
    
    # 发送通知（可选，需要配置邮件或其他通知方式）
    # echo "TelegramMonitor 服务已重启" | mail -s "服务重启通知" your-email@example.com
fi

# 检查磁盘空间
DISK_USAGE=$(df -h /home | awk 'NR==2 {print $5}' | sed 's/%//')
if [ "$DISK_USAGE" -gt 80 ]; then
    echo "$(date): 磁盘空间不足，使用率: ${DISK_USAGE}%"
fi

# 检查内存使用
MEM_USAGE=$(free | awk 'NR==2{printf "%.0f", $3*100/$2}')
if [ "$MEM_USAGE" -gt 80 ]; then
    echo "$(date): 内存使用率过高: ${MEM_USAGE}%"
fi
EOF

chmod +x ~/monitor-telegram.sh

# 设置定时检查（每 5 分钟）
(crontab -l 2>/dev/null; echo "*/5 * * * * /home/ubuntu/monitor-telegram.sh >> /home/ubuntu/monitor.log 2>&1") | crontab -
```

---

## 快速部署脚本

### 一键部署脚本
创建自动化部署脚本：

```bash
# 创建部署脚本
tee ~/deploy-telegram.sh << 'EOF'
#!/bin/bash
set -e

echo "=== TelegramMonitor 一键部署脚本 ==="

# 检查是否为root用户
if [ "$EUID" -eq 0 ]; then
  echo "请不要使用root用户运行此脚本"
  exit 1
fi

# 更新系统
echo "1. 更新系统..."
sudo apt update && sudo apt upgrade -y

# 安装基础工具
echo "2. 安装基础工具..."
sudo apt install -y curl wget git vim unzip htop

# 安装Docker
echo "3. 安装Docker..."
if ! command -v docker &> /dev/null; then
    curl -fsSL https://get.docker.com -o get-docker.sh
    sudo sh get-docker.sh
    sudo usermod -aG docker $USER
    rm get-docker.sh
fi

# 安装.NET SDK
echo "4. 安装.NET SDK..."
if ! command -v dotnet &> /dev/null; then
    wget https://packages.microsoft.com/config/ubuntu/22.04/packages-microsoft-prod.deb -O packages-microsoft-prod.deb
    sudo dpkg -i packages-microsoft-prod.deb
    rm packages-microsoft-prod.deb
    sudo apt update
    sudo apt install -y dotnet-sdk-9.0
fi

# 配置防火墙
echo "5. 配置防火墙..."
sudo ufw allow ssh
sudo ufw allow 80/tcp
sudo ufw allow 443/tcp
sudo ufw allow 5005/tcp
sudo ufw --force enable

echo "=== 环境准备完成 ==="
echo "请将您的项目源代码上传到 ~/telegrammonitor 目录"
echo "然后运行: cd ~/telegrammonitor && ./build-and-run.sh"
EOF

chmod +x ~/deploy-telegram.sh
```

### 构建和运行脚本
在项目目录中创建：

```bash
# 在项目根目录创建构建脚本
tee build-and-run.sh << 'EOF'
#!/bin/bash
set -e

echo "=== 构建和启动 TelegramMonitor ==="

# 检查必要文件
if [ ! -d "src" ]; then
    echo "错误：未找到src目录，请确保在项目根目录运行"
    exit 1
fi

# 构建应用
echo "1. 构建应用程序..."
cd src
dotnet restore
dotnet publish -c Release -r linux-x64 --self-contained -o ../out/linux-x64/
cd ..

# 创建目录
echo "2. 创建必要目录..."
mkdir -p data session logs

# 创建Docker配置
echo "3. 创建Docker配置..."
cat > docker-compose.yml << 'COMPOSE_EOF'
version: '3.8'
services:
  telegrammonitor:
    build: .
    container_name: telegrammonitor
    restart: unless-stopped
    ports:
      - "5005:5005"
    volumes:
      - ./data:/app/data
      - ./session:/app/session
      - ./logs:/app/logs
    environment:
      - ASPNETCORE_ENVIRONMENT=Production
      - ASPNETCORE_URLS=http://+:5005
      - TZ=Asia/Shanghai
COMPOSE_EOF

# 启动服务
echo "4. 启动服务..."
docker compose build
docker compose up -d

# 检查状态
echo "5. 检查服务状态..."
sleep 10
docker compose ps

echo "=== 部署完成 ==="
echo "访问地址: http://$(curl -s ifconfig.me):5005"
echo "或者: http://localhost:5005"
EOF

chmod +x build-and-run.sh
```

---

## 故障排除

### 1. .NET SDK 安装问题
```bash
# 如果安装失败，尝试手动安装
wget https://dot.net/v1/dotnet-install.sh -O dotnet-install.sh
chmod +x dotnet-install.sh
./dotnet-install.sh --version latest --channel 9.0

# 设置环境变量
export DOTNET_ROOT=$HOME/.dotnet
export PATH=$PATH:$HOME/.dotnet:$HOME/.dotnet/tools

# 验证安装
dotnet --version
```

### 2. 构建失败
```bash
# 清理构建缓存
cd src
dotnet clean
dotnet restore --force
dotnet publish -c Release -r linux-x64 --self-contained -o ../out/linux-x64/

# 如果遇到权限问题
sudo chown -R $USER:$USER ~/telegrammonitor
```

### 3. 服务无法启动
```bash
# 查看详细错误日志
docker compose logs telegrammonitor

# 检查端口占用
sudo netstat -tlnp | grep :5005

# 检查 Docker 服务
sudo systemctl status docker

# 重新构建镜像
docker compose build --no-cache
```

### 2. 无法访问 Web 界面
```bash
# 检查 Nginx 状态
sudo systemctl status nginx

# 检查 Nginx 配置
sudo nginx -t

# 查看 Nginx 错误日志
sudo tail -f /var/log/nginx/error.log

# 检查防火墙
sudo ufw status
```

### 3. Telegram 登录失败
- 确认 API ID 和 API Hash 配置正确
- 检查网络连接是否正常
- 确认手机号格式正确（+8613812345678）
- 检查是否需要代理配置

### 4. 关键词不匹配
- 检查关键词配置是否正确
- 确认匹配类型设置
- 查看应用日志了解详细信息

### 5. 消息转发失败
- 确认目标群组/频道设置正确
- 检查 Bot 是否有发送权限
- 确认登录状态正常

### 6. 数据库问题
```bash
# 检查数据库文件权限
ls -la ~/telegrammonitor/data/

# 重新创建数据库目录
mkdir -p ~/telegrammonitor/data
chmod 755 ~/telegrammonitor/data
```

### 7. SSL 证书问题
```bash
# 检查证书状态
sudo certbot certificates

# 强制续期证书
sudo certbot renew --force-renewal

# 重新获取证书
sudo certbot --nginx -d your-domain.com --force-renewal
```

---

## 性能优化

### 1. 系统级优化
```bash
# 增加文件描述符限制
echo "* soft nofile 65535" | sudo tee -a /etc/security/limits.conf
echo "* hard nofile 65535" | sudo tee -a /etc/security/limits.conf

# 优化网络参数
echo "net.core.somaxconn = 65535" | sudo tee -a /etc/sysctl.conf
echo "net.ipv4.tcp_max_syn_backlog = 65535" | sudo tee -a /etc/sysctl.conf
sudo sysctl -p
```

### 2. Docker 优化
修改 `docker-compose.yml` 添加资源限制：

```yaml
services:
  telegrammonitor:
    # ... 其他配置
    deploy:
      resources:
        limits:
          memory: 512M
        reservations:
          memory: 256M
    logging:
      driver: "json-file"
      options:
        max-size: "10m"
        max-file: "3"
```

---

## 安全建议

### 1. 系统安全
- 定期更新系统和依赖包
- 使用密钥认证而非密码登录
- 配置 fail2ban 防止暴力破解
- 定期检查系统日志

### 2. 应用安全
- 不要在公网暴露管理端口
- 使用强密码和双因素认证
- 定期备份重要数据
- 监控异常访问

### 3. 网络安全
- 使用 HTTPS 加密传输
- 配置 Web 应用防火墙
- 限制访问来源 IP
- 定期检查 SSL 证书

---

## 更新升级

### 1. 应用程序更新
```bash
# 备份当前版本
cd ~/telegrammonitor
docker compose down
cp -r data session ~/backup-before-update/

# 上传新版本文件
# 解压新版本文件

# 重新构建和启动
docker compose build --no-cache
docker compose up -d
```

### 2. 系统更新
```bash
# 更新系统包
sudo apt update && sudo apt upgrade -y

# 更新 Docker
sudo apt update && sudo apt install docker-ce docker-ce-cli containerd.io

# 重启服务
sudo systemctl restart docker
```

---

## 联系支持

如果遇到问题，请检查：
1. 服务日志：`docker compose logs -f`
2. 系统日志：`sudo journalctl -f`
3. Nginx 日志：`sudo tail -f /var/log/nginx/error.log`

确保提供详细的错误信息和日志内容以便诊断问题。
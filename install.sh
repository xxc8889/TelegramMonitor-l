#!/bin/bash
set -e

echo "=== TelegramMonitor 多账号版一键部署脚本 ==="
echo "域名: aa.dp888.dpdns.org"
echo "用户: root"
echo "版本: 多账号版 (支持多账号智能去重转发)"
echo ""

# 检查是否为root用户
if [ "$EUID" -ne 0 ]; then
  echo "❌ 请使用 root 用户运行此脚本"
  echo "执行: sudo su - 切换到 root 用户"
  exit 1
fi

# 更新系统
echo "📦 1. 更新系统..."
apt update && apt upgrade -y

# 安装基础工具
echo "🔧 2. 安装基础工具..."
apt install -y \
    curl \
    wget \
    git \
    vim \
    unzip \
    htop \
    net-tools \
    ca-certificates \
    gnupg \
    lsb-release \
    ufw \
    dos2unix

# 配置防火墙
echo "🛡️ 3. 配置防火墙..."
ufw allow ssh
ufw allow 80/tcp
ufw allow 443/tcp
ufw allow 5005/tcp
echo "y" | ufw enable

# 安装 Docker
echo "🐳 4. 安装 Docker..."
if ! command -v docker &> /dev/null; then
    # 卸载旧版本
    apt remove -y docker docker-engine docker.io containerd runc 2>/dev/null || true
    
    # 添加 Docker 官方 GPG 密钥
    mkdir -p /etc/apt/keyrings
    curl -fsSL https://download.docker.com/linux/ubuntu/gpg | gpg --dearmor -o /etc/apt/keyrings/docker.gpg
    
    # 添加 Docker 仓库
    echo "deb [arch=$(dpkg --print-architecture) signed-by=/etc/apt/keyrings/docker.gpg] https://download.docker.com/linux/ubuntu $(lsb_release -cs) stable" | tee /etc/apt/sources.list.d/docker.list > /dev/null
    
    # 安装 Docker
    apt update
    apt install -y docker-ce docker-ce-cli containerd.io docker-buildx-plugin docker-compose-plugin
    
    # 启动 Docker
    systemctl start docker
    systemctl enable docker
    
    echo "✅ Docker 安装完成"
else
    echo "✅ Docker 已安装"
fi

# 安装 .NET 8.0 SDK
echo "⚙️ 5. 安装 .NET 8.0 SDK..."
if ! command -v dotnet &> /dev/null; then
    # 添加 Microsoft 包仓库
    wget https://packages.microsoft.com/config/ubuntu/22.04/packages-microsoft-prod.deb -O packages-microsoft-prod.deb
    dpkg -i packages-microsoft-prod.deb
    rm packages-microsoft-prod.deb
    
    # 安装 .NET SDK
    apt update
    apt install -y dotnet-sdk-8.0
    
    echo "✅ .NET SDK 安装完成"
else
    echo "✅ .NET SDK 已安装"
fi

# 安装 Nginx
echo "🌐 6. 安装 Nginx..."
if ! command -v nginx &> /dev/null; then
    apt install -y nginx
    systemctl start nginx
    systemctl enable nginx
    echo "✅ Nginx 安装完成"
else
    echo "✅ Nginx 已安装"
fi

# 安装 Certbot
echo "🔒 7. 安装 Certbot..."
apt install -y certbot python3-certbot-nginx

# 获取当前脚本目录
SCRIPT_DIR=$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)
cd "$SCRIPT_DIR"

# 检查项目文件
echo "📁 8. 检查项目文件..."
if [ ! -d "src" ]; then
    echo "❌ 错误：未找到 src 目录"
    echo "请确保在项目根目录运行此脚本"
    exit 1
fi

# 检查必要的文件是否存在
if [ ! -f "src/wwwroot/index.html" ]; then
    echo "❌ 错误：未找到首页文件"
    exit 1
fi

if [ ! -f "src/wwwroot/accounts.html" ]; then
    echo "❌ 错误：未找到多账号管理页面"
    exit 1
fi

echo "✅ 项目文件检查通过"

# 构建应用
echo "🔨 9. 构建应用程序..."
cd src
dotnet clean
dotnet restore
dotnet publish -c Release -r linux-x64 --self-contained -o ../out/linux-x64/
cd ..

# 创建目录
echo "📂 10. 创建必要目录..."
mkdir -p data session logs sessions
chmod 755 data session logs sessions

# 创建 Dockerfile
echo "🐳 11. 创建 Dockerfile..."
cat > Dockerfile << 'EOF'
FROM mcr.microsoft.com/dotnet/runtime-deps:8.0-bookworm-slim

# 安装必要工具和时区数据
RUN apt-get update && apt-get install -y \
    tzdata \
    curl \
    && rm -rf /var/lib/apt/lists/*

# 设置时区
ENV TZ=Asia/Shanghai

# 设置工作目录
WORKDIR /app

# 复制应用文件
COPY out/linux-x64/ /app/

# 设置权限
RUN chmod +x /app/TelegramMonitor

# 暴露端口
EXPOSE 5005

# 健康检查
HEALTHCHECK --interval=30s --timeout=10s --start-period=40s --retries=3 \
    CMD curl -f http://localhost:5005/api/health || exit 1

# 启动应用
ENTRYPOINT ["/app/TelegramMonitor"]
EOF

# 创建 Docker Compose 配置
echo "🐳 12. 创建 Docker Compose 配置..."
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
      - ./sessions:/app/sessions
      - ./logs:/app/logs
    environment:
      - ASPNETCORE_ENVIRONMENT=Production
      - ASPNETCORE_URLS=http://+:5005
      - TZ=Asia/Shanghai
    networks:
      - telegram-network
    healthcheck:
      test: ["CMD", "curl", "-f", "http://localhost:5005/api/account/status"]
      interval: 30s
      timeout: 10s
      retries: 3
      start_period: 40s

networks:
  telegram-network:
    driver: bridge
EOF

# 配置 Nginx 静态文件和反向代理
echo "🌐 13. 配置 Nginx..."

# 创建静态文件目录
mkdir -p /var/www/html

# 复制静态文件到 Nginx 目录
cp -r src/wwwroot/* /var/www/html/
chown -R www-data:www-data /var/www/html/
chmod -R 755 /var/www/html/

# 配置 Nginx 反向代理
cat > /etc/nginx/sites-available/telegrammonitor << 'EOF'
server {
    listen 80;
    server_name aa.dp888.dpdns.org;
    
    root /var/www/html;
    index index.html index.htm;
    
    # 静态文件处理
    location / {
        try_files $uri $uri/ @backend;
    }
    
    # 后端API代理
    location @backend {
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
    
    # API路由直接代理
    location /api/ {
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
        expires 1y;
        add_header Cache-Control "public, immutable";
        try_files $uri @backend;
    }
    
    # 禁止访问敏感文件
    location ~ /\. {
        deny all;
    }
    
    # Let's Encrypt 验证路径
    location /.well-known/acme-challenge/ {
        root /var/www/html;
    }
}
EOF

# 启用站点
ln -sf /etc/nginx/sites-available/telegrammonitor /etc/nginx/sites-enabled/
rm -f /etc/nginx/sites-enabled/default

# 测试 Nginx 配置
nginx -t
systemctl restart nginx

# 构建和启动 Docker 服务
echo "🚀 14. 构建和启动服务..."
docker compose build --no-cache
docker compose up -d

# 等待服务启动
echo "⏳ 15. 等待服务启动..."
sleep 30

# 检查服务状态
echo "🔍 16. 检查服务状态..."
docker compose ps

# 获取 SSL 证书
echo "🔒 17. 获取 SSL 证书..."
echo "正在为域名 aa.dp888.dpdns.org 申请 SSL 证书..."

# 测试服务是否正常运行
if curl -s -o /dev/null -w "%{http_code}" http://localhost:5005/api/account/status | grep -q "200\|400\|404"; then
    echo "✅ 服务运行正常，开始申请 SSL 证书..."
    
    # 申请 SSL 证书
    certbot --nginx -d aa.dp888.dpdns.org --non-interactive --agree-tos --email admin@dp888.dpdns.org --redirect
    
    # 设置自动续期
    crontab -l 2>/dev/null | { cat; echo "0 12 * * * /usr/bin/certbot renew --quiet && systemctl reload nginx"; } | crontab -
    
    echo "✅ SSL 证书配置完成"
else
    echo "⚠️ 服务启动可能有问题，跳过 SSL 证书申请"
    echo "您可以稍后手动执行："
    echo "certbot --nginx -d aa.dp888.dpdns.org"
fi

# 创建系统服务
echo "🔧 18. 创建系统服务..."
cat > /etc/systemd/system/telegrammonitor.service << 'EOF'
[Unit]
Description=TelegramMonitor Multi-Account Docker Service
After=docker.service
Requires=docker.service

[Service]
Type=oneshot
RemainAfterExit=yes
User=root
WorkingDirectory=WORKING_DIR_PLACEHOLDER
ExecStart=/usr/bin/docker compose up -d
ExecStop=/usr/bin/docker compose down
TimeoutStartSec=0

[Install]
WantedBy=multi-user.target
EOF

# 替换工作目录
sed -i "s|WORKING_DIR_PLACEHOLDER|$SCRIPT_DIR|g" /etc/systemd/system/telegrammonitor.service

# 启用系统服务
systemctl daemon-reload
systemctl enable telegrammonitor

# 创建管理脚本
echo "📝 19. 创建管理脚本..."

# 重启脚本
cat > restart.sh << 'EOF'
#!/bin/bash
cd "$(dirname "$0")"
echo "正在重启 TelegramMonitor 多账号服务..."
docker compose restart
echo "服务已重启"
docker compose ps
EOF
chmod +x restart.sh

# 查看日志脚本
cat > logs.sh << 'EOF'
#!/bin/bash
cd "$(dirname "$0")"
docker compose logs -f telegrammonitor
EOF
chmod +x logs.sh

# 更新脚本
cat > update.sh << 'EOF'
#!/bin/bash
cd "$(dirname "$0")"
echo "正在更新 TelegramMonitor..."
docker compose down
docker compose build --no-cache
docker compose up -d
echo "更新完成"
docker compose ps
EOF
chmod +x update.sh

# 备份脚本
cat > backup.sh << 'EOF'
#!/bin/bash
SCRIPT_DIR=$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)
BACKUP_DIR="/root/backups/telegrammonitor"
DATE=$(date +%Y%m%d_%H%M%S)

mkdir -p "$BACKUP_DIR"

cd "$SCRIPT_DIR"

# 停止服务
docker compose down

# 备份数据
cp -r data "$BACKUP_DIR/data_$DATE" 2>/dev/null || true
cp -r session "$BACKUP_DIR/session_$DATE" 2>/dev/null || true
cp -r sessions "$BACKUP_DIR/sessions_$DATE" 2>/dev/null || true

# 创建压缩备份
tar -czf "$BACKUP_DIR/telegrammonitor_backup_$DATE.tar.gz" data session sessions 2>/dev/null || true

# 重启服务
docker compose up -d

# 清理 30 天前的备份
find "$BACKUP_DIR" -type f -mtime +30 -delete 2>/dev/null || true

echo "备份完成: $DATE"
echo "备份位置: $BACKUP_DIR"
EOF
chmod +x backup.sh

# 最终检查
echo "🎯 20. 最终检查..."
sleep 5

# 检查容器状态
CONTAINER_STATUS=$(docker compose ps --format "table {{.Service}}\t{{.Status}}" | grep telegrammonitor | awk '{print $2}')

if [[ "$CONTAINER_STATUS" == "Up" ]]; then
    echo ""
    echo "🎉 ========================================="
    echo "🎉 TelegramMonitor 多账号版部署成功！"
    echo "🎉 ========================================="
    echo ""
    echo "🌐 访问地址："
    echo "  主页: https://aa.dp888.dpdns.org/"
    echo "  多账号管理: https://aa.dp888.dpdns.org/accounts.html"
    echo "  关键词配置: https://aa.dp888.dpdns.org/keywords.html"
    echo ""
    echo "🔧 管理命令："
    echo "  查看状态: docker compose ps"
    echo "  查看日志: ./logs.sh"
    echo "  重启服务: ./restart.sh"
    echo "  更新服务: ./update.sh"
    echo "  备份数据: ./backup.sh"
    echo "  停止服务: docker compose down"
    echo "  启动服务: docker compose up -d"
    echo ""
    echo "⚙️ 系统服务："
    echo "  开机自启: systemctl enable telegrammonitor"
    echo "  服务状态: systemctl status telegrammonitor"
    echo ""
    echo "🚀 多账号使用步骤："
    echo "1. 访问 https://aa.dp888.dpdns.org/"
    echo "2. 点击 '多账号管理' → accounts.html"
    echo "3. 添加账号：填写 API ID/Hash 和手机号"
    echo "4. 登录验证：为每个账号完成验证码登录"
    echo "5. 设置目标群组：输入 Bot 要转发到的群组ID"
    echo "6. 配置关键词：在 keywords.html 设置监控规则"
    echo "7. 启动监控：点击 '启动监控' 开始智能转发"
    echo ""
    echo "✨ 新功能特性："
    echo "  ✅ 多账号支持 - 每个账号独立API配置"
    echo "  ✅ 智能去重 - 避免重复消息转发"
    echo "  ✅ 用户名屏蔽 - 根据用户名关键词屏蔽"
    echo "  ✅ 数据持久化 - 账号和配置永久保存"
    echo "  ✅ Bot转发 - 完整的消息转发功能"
    echo ""
    echo "🔒 SSL 证书已配置，访问将使用 HTTPS"
    echo "📁 项目目录: $SCRIPT_DIR"
    echo ""
    echo "⚠️ 重要提醒："
    echo "- 需要在 appsettings.json 中配置 Bot Token"
    echo "- Bot 需要加入目标群组并有发送权限"
    echo "- 监控账号不需要在目标群组"
    echo ""
else
    echo ""
    echo "⚠️ ========================================="
    echo "⚠️ 部署完成但服务可能有问题"
    echo "⚠️ ========================================="
    echo ""
    echo "请检查："
    echo "1. 查看日志: docker compose logs telegrammonitor"
    echo "2. 检查状态: docker compose ps"
    echo "3. 重启服务: docker compose restart"
    echo ""
fi

echo "部署完成时间: $(date)"
echo "感谢使用 TelegramMonitor 多账号版！"
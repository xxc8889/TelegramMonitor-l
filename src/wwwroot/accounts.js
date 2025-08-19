const API_BASE = '/api/account';

let currentLoginAccountId = 0;

// Toast 通知
const toastBox = document.getElementById('toast');
toastBox.style.zIndex = '9999';

function toast(message, isSuccess = true) {
    const div = document.createElement('div');
    div.setAttribute('role', 'alert');
    div.className = `alert alert-${isSuccess ? 'success' : 'error'} alert-horizontal shadow-lg`;
    div.innerHTML = `<span>${message}</span>`;
    toastBox.appendChild(div);
    setTimeout(() => div.remove(), 4000);
}

// API 请求
async function api(url, options = {}) {
    const response = await fetch(url, {
        headers: { 'Content-Type': 'application/json' },
        ...options
    });
    
    const text = await response.text();
    let data = {};
    
    try {
        data = text ? JSON.parse(text) : {};
    } catch {}
    
    if (!response.ok || data.succeeded === false) {
        const error = data.message || '操作失败';
        toast(error, false);
        throw new Error(error);
    }
    
    return data;
}

// 刷新状态
async function refreshStatus() {
    try {
        const { data } = await api(`${API_BASE}/status`);
        
        // 更新监控状态
        const statusEl = document.getElementById('monitorStatus');
        const startBtn = document.getElementById('btnStartMonitor');
        const stopBtn = document.getElementById('btnStopMonitor');
        
        if (data.IsMonitoring) {
            statusEl.textContent = '监控中';
            statusEl.className = 'badge badge-success';
            startBtn.disabled = true;
            stopBtn.disabled = false;
        } else {
            statusEl.textContent = '已停止';
            statusEl.className = 'badge badge-error';
            startBtn.disabled = false;
            stopBtn.disabled = true;
        }
        
        // 更新账号列表
        updateAccountsTable(data.Accounts);
    } catch (error) {
        console.error('刷新状态失败:', error);
    }
}

// 更新账号表格
function updateAccountsTable(accounts) {
    const tbody = document.getElementById('accountsTable');
    tbody.innerHTML = '';
    
    accounts.forEach(account => {
        const tr = document.createElement('tr');
        tr.innerHTML = `
            <td>${account.AccountId}</td>
            <td>${account.Name}</td>
            <td>${account.PhoneNumber}</td>
            <td>
                <div class="badge ${account.IsLoggedIn ? 'badge-success' : 'badge-error'}">
                    ${account.IsLoggedIn ? '已登录' : '未登录'}
                </div>
            </td>
            <td>
                <div class="badge ${account.IsEnabled ? 'badge-success' : 'badge-warning'}">
                    ${account.IsEnabled ? '启用' : '禁用'}
                </div>
            </td>
            <td>${account.LastLoginTime ? new Date(account.LastLoginTime).toLocaleString() : '从未登录'}</td>
            <td>
                <div class="flex gap-1">
                    <button class="btn btn-sm" onclick="loginAccount(${account.AccountId})">登录</button>
                    <button class="btn btn-sm btn-info" onclick="getDialogs(${account.AccountId})">会话</button>
                    <button class="btn btn-sm btn-error" onclick="deleteAccount(${account.AccountId})">删除</button>
                </div>
            </td>
        `;
        tbody.appendChild(tr);
    });
}

// 打开添加账号模态框
function openAddAccountModal() {
    document.getElementById('accountName').value = '';
    document.getElementById('accountApiId').value = '';
    document.getElementById('accountApiHash').value = '';
    document.getElementById('accountPhone').value = '';
    document.getElementById('addAccountModal').showModal();
}

// 添加账号
async function addAccount() {
    try {
        const data = {
            Name: document.getElementById('accountName').value.trim(),
            ApiId: parseInt(document.getElementById('accountApiId').value),
            ApiHash: document.getElementById('accountApiHash').value.trim(),
            PhoneNumber: document.getElementById('accountPhone').value.trim()
        };
        
        if (!data.Name || !data.ApiId || !data.ApiHash || !data.PhoneNumber) {
            toast('请填写完整信息', false);
            return;
        }
        
        await api(`${API_BASE}/add`, {
            method: 'POST',
            body: JSON.stringify(data)
        });
        
        toast('账号添加成功');
        refreshStatus();
    } catch (error) {
        console.error('添加账号失败:', error);
    }
}

// 删除账号
async function deleteAccount(accountId) {
    if (!confirm('确定要删除这个账号吗？')) return;
    
    try {
        await api(`${API_BASE}/delete/${accountId}`, {
            method: 'DELETE'
        });
        
        toast('账号删除成功');
        refreshStatus();
    } catch (error) {
        console.error('删除账号失败:', error);
    }
}

// 登录账号
async function loginAccount(accountId) {
    try {
        currentLoginAccountId = accountId;
        
        const response = await api(`${API_BASE}/login`, {
            method: 'POST',
            body: JSON.stringify({
                AccountId: accountId,
                LoginInfo: ''
            })
        });
        
        handleLoginResponse(response.data);
    } catch (error) {
        console.error('登录失败:', error);
    }
}

// 处理登录响应
function handleLoginResponse(data) {
    const modal = document.getElementById('loginModal');
    const title = document.getElementById('loginTitle');
    const input = document.getElementById('loginInput');
    const message = document.getElementById('loginMessage');
    
    switch (data.loginState) {
        case 1: // WaitingForVerificationCode
            title.textContent = '输入验证码';
            input.placeholder = '请输入收到的验证码';
            message.textContent = '验证码已发送到您的手机';
            modal.showModal();
            break;
            
        case 2: // WaitingForPassword
            title.textContent = '输入两步验证密码';
            input.placeholder = '请输入两步验证密码';
            message.textContent = '请输入您的两步验证密码';
            modal.showModal();
            break;
            
        case 4: // LoggedIn
            toast('登录成功');
            refreshStatus();
            break;
            
        default:
            toast('登录失败: ' + data.message, false);
            break;
    }
}

// 提交登录信息
async function submitLogin() {
    try {
        const loginInfo = document.getElementById('loginInput').value.trim();
        if (!loginInfo) {
            toast('请输入内容', false);
            return;
        }
        
        const response = await api(`${API_BASE}/login`, {
            method: 'POST',
            body: JSON.stringify({
                AccountId: currentLoginAccountId,
                LoginInfo: loginInfo
            })
        });
        
        document.getElementById('loginInput').value = '';
        handleLoginResponse(response.data);
    } catch (error) {
        console.error('提交登录信息失败:', error);
    }
}

// 获取会话列表
async function getDialogs(accountId) {
    try {
        const { data } = await api(`${API_BASE}/${accountId}/dialogs`);
        
        // 创建临时模态框显示会话列表
        const modal = document.createElement('dialog');
        modal.className = 'modal';
        modal.innerHTML = `
            <div class="modal-box max-w-2xl">
                <h3 class="font-bold text-lg mb-4">选择目标群组</h3>
                <div class="max-h-96 overflow-y-auto">
                    <table class="table w-full">
                        <thead>
                            <tr><th>群组/频道</th><th>操作</th></tr>
                        </thead>
                        <tbody>
                            ${data.map(dialog => `
                                <tr>
                                    <td>${dialog.DisplayTitle}</td>
                                    <td>
                                        <button class="btn btn-sm" onclick="selectTargetFromDialog(${dialog.Id})">
                                            设为目标
                                        </button>
                                    </td>
                                </tr>
                            `).join('')}
                        </tbody>
                    </table>
                </div>
                <div class="modal-action">
                    <form method="dialog">
                        <button class="btn">关闭</button>
                    </form>
                </div>
            </div>
        `;
        
        document.body.appendChild(modal);
        modal.showModal();
        
        // 关闭时移除模态框
        modal.addEventListener('close', () => {
            document.body.removeChild(modal);
        });
    } catch (error) {
        console.error('获取会话列表失败:', error);
    }
}

// 从会话中选择目标群组
function selectTargetFromDialog(chatId) {
    document.getElementById('targetChatId').value = chatId;
    setTargetChat();
}

// 设置目标群组
async function setTargetChat() {
    try {
        const chatId = document.getElementById('targetChatId').value.trim();
        if (!chatId) {
            toast('请输入群组ID', false);
            return;
        }
        
        await api(`${API_BASE}/target`, {
            method: 'POST',
            body: JSON.stringify({
                ChatId: parseInt(chatId)
            })
        });
        
        toast('目标群组设置成功');
    } catch (error) {
        console.error('设置目标群组失败:', error);
    }
}

// 启动监控
async function startMonitoring() {
    try {
        const response = await api(`${API_BASE}/start`, {
            method: 'POST'
        });
        
        if (response.succeeded) {
            toast('监控启动成功');
        } else {
            toast(response.data.message || '启动失败', false);
        }
        
        refreshStatus();
    } catch (error) {
        console.error('启动监控失败:', error);
    }
}

// 停止监控
async function stopMonitoring() {
    try {
        await api(`${API_BASE}/stop`, {
            method: 'POST'
        });
        
        toast('监控已停止');
        refreshStatus();
    } catch (error) {
        console.error('停止监控失败:', error);
    }
}

// 初始化
refreshStatus();
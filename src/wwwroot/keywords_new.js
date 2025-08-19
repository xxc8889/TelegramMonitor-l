const API = '/api/keyword';

const typeMap = {
    FullWord: '全字',
    Contains: '包含',
    Regex: '正则',
    Fuzzy: '模糊',
    User: '用户',
    UserName: '用户名'
};

const actionMap = {
    Exclude: '排除',
    Monitor: '监控'
};

const styleMap = {
    isCaseSensitive: '大小写',
    isBold: '粗体',
    isItalic: '斜体',
    isUnderline: '下划线',
    isStrikeThrough: '删除线',
    isQuote: '引用',
    isMonospace: '等宽',
    isSpoiler: '剧透'
};

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
async function api(url, options) {
    const response = await fetch(url, Object.assign({
        headers: { 'Content-Type': 'application/json' }
    }, options));
    
    const text = await response.text();
    let data = {};
    
    try {
        data = text ? JSON.parse(text) : {};
    } catch {}
    
    if (!response.ok || data.succeeded === false) {
        const error = data.errors && (typeof data.errors === 'string' ? data.errors : Object.values(data.errors)[0][0]) || data.message || text || '操作失败';
        toast(error, false);
        throw new Error(error);
    }
    
    return data;
}

// 表格渲染
const tbody = document.getElementById('kwBody');

function styleString(item) {
    return Object.entries(styleMap)
        .filter(([key]) => item[key])
        .map(([, value]) => value)
        .join(' ');
}

function renderRow(item) {
    const tr = document.createElement('tr');
    tr.innerHTML = `
        <td><input type="checkbox" class="row-check" value="${item.id}"></td>
        <td>${item.id}</td>
        <td>${item.keywordContent}</td>
        <td>${typeMap[item.keywordType] ?? item.keywordType}</td>
        <td>${actionMap[item.keywordAction] ?? item.keywordAction}</td>
        <td>${styleString(item)}</td>
        <td>
            <button class="btn" onclick='openEdit(${JSON.stringify(item)})'>编辑</button>
            <button class="btn btn-error ml-1" onclick="del(${item.id})">删</button>
        </td>
    `;
    return tr;
}

// 刷新列表
async function refresh() {
    const { data } = await api(`${API}/list`);
    tbody.innerHTML = '';
    data.forEach(item => tbody.appendChild(renderRow(item)));
}

// 删除单个
async function del(id) {
    await api(`${API}/delete/${id}`, { method: 'DELETE' });
    toast('删除成功');
    refresh();
}

// 全选/取消全选
function toggleAll(checkbox) {
    document.querySelectorAll('.row-check').forEach(cb => cb.checked = checkbox.checked);
}

// 批量删除
async function deleteSelected() {
    const ids = [...document.querySelectorAll('.row-check')]
        .filter(cb => cb.checked)
        .map(cb => +cb.value);
    
    if (!ids.length) {
        toast('未选中', false);
        return;
    }
    
    await api(`${API}/batchdelete`, {
        method: 'DELETE',
        body: JSON.stringify(ids)
    });
    
    toast('批量删除成功');
    refresh();
}

// 单个添加
async function addSingle() {
    const data = {
        keywordContent: sContent.value.trim(),
        keywordType: +sType.value,
        keywordAction: +sAction.value,
        isCaseSensitive: sCase.checked,
        isBold: sBold.checked,
        isItalic: sItalic.checked,
        isUnderline: sUnder.checked,
        isStrikeThrough: sStrike.checked,
        isQuote: sQuote.checked,
        isMonospace: sMono.checked,
        isSpoiler: sSpoil.checked
    };
    
    if (!data.keywordContent) {
        toast('内容不能为空', false);
        return;
    }
    
    await api(`${API}/add`, {
        method: 'POST',
        body: JSON.stringify(data)
    });
    
    toast('添加成功');
    refresh();
}

// 动态行批量添加
const dynamicRows = document.getElementById('dynamicRows');

function rowTpl(id) {
    return `
        <div class="flex flex-wrap gap-2 items-center border p-2 rounded" id="row-${id}">
            <input class="input input-bordered w-40" placeholder="关键词">
            <select class="select select-bordered">
                <option value="0">全字</option>
                <option value="1">包含</option>
                <option value="2">正则</option>
                <option value="3">模糊</option>
                <option value="4">用户</option>
                <option value="5">用户名</option>
            </select>
            <select class="select select-bordered">
                <option value="1">监控</option>
                <option value="0">排除</option>
            </select>
            ${Object.entries(styleMap).map(([key, name]) => `
                <label class="label gap-1 text-xs"><span>${name}</span>
                    <input type="checkbox" data-flag="${key}" class="checkbox">
                </label>
            `).join('')}
            <button class="btn btn-error" onclick="this.parentNode.remove()">x</button>
        </div>
    `;
}

function addRow() {
    dynamicRows.insertAdjacentHTML('beforeend', rowTpl(Date.now()));
}

async function uploadRows() {
    const rows = [...dynamicRows.children].map(row => {
        const input = row.querySelector('input.input');
        if (!input || !input.value.trim()) return null;
        
        const [typeSelect, actionSelect] = row.querySelectorAll('select');
        const data = {
            keywordContent: input.value.trim(),
            keywordType: +typeSelect.value,
            keywordAction: +actionSelect.value
        };
        
        row.querySelectorAll('input[data-flag]').forEach(cb => {
            data[cb.dataset.flag] = cb.checked;
        });
        
        return data;
    }).filter(Boolean);
    
    if (!rows.length) {
        toast('无有效行', false);
        return;
    }
    
    await api(`${API}/batchadd`, {
        method: 'POST',
        body: JSON.stringify(rows)
    });
    
    toast('批量添加成功');
    refresh();
}

// 文本批量添加
async function uploadText() {
    const keywords = txtKeywords.value.split('\n')
        .map(line => line.trim())
        .filter(Boolean);
    
    if (!keywords.length) {
        toast('文本为空', false);
        return;
    }
    
    const commonProps = {
        isCaseSensitive: tCase.checked,
        isBold: tBold.checked,
        isItalic: tItalic.checked,
        isUnderline: tUnder.checked,
        isStrikeThrough: tStrike.checked,
        isQuote: tQuote.checked,
        isMonospace: tMono.checked,
        isSpoiler: tSpoil.checked
    };
    
    const data = keywords.map(keyword => ({
        keywordContent: keyword,
        keywordType: +tType.value,
        keywordAction: +tAction.value,
        ...commonProps
    }));
    
    await api(`${API}/batchadd`, {
        method: 'POST',
        body: JSON.stringify(data)
    });
    
    toast('批量添加成功');
    refresh();
}

// 编辑功能
function fillEdit(item) {
    eId.value = item.id;
    eContent.value = item.keywordContent;
    
    const types = Object.keys(typeMap);
    const actions = Object.keys(actionMap);
    
    eType.value = types.indexOf(item.keywordType);
    eAction.value = actions.indexOf(item.keywordAction);
    
    eCase.checked = item.isCaseSensitive;
    eBold.checked = item.isBold;
    eItalic.checked = item.isItalic;
    eUnder.checked = item.isUnderline;
    eStrike.checked = item.isStrikeThrough;
    eQuote.checked = item.isQuote;
    eMono.checked = item.isMonospace;
    eSpoil.checked = item.isSpoiler;
}

function openEdit(item) {
    fillEdit(item);
    editModal.showModal();
}

async function saveEdit() {
    const data = {
        id: +eId.value,
        keywordContent: eContent.value.trim(),
        keywordType: +eType.value,
        keywordAction: +eAction.value,
        isCaseSensitive: eCase.checked,
        isBold: eBold.checked,
        isItalic: eItalic.checked,
        isUnderline: eUnder.checked,
        isStrikeThrough: eStrike.checked,
        isQuote: eQuote.checked,
        isMonospace: eMono.checked,
        isSpoiler: eSpoil.checked
    };
    
    await api(`${API}/update`, {
        method: 'PUT',
        body: JSON.stringify(data)
    });
    
    toast('修改成功');
    refresh();
}

// 标签页切换
document.querySelectorAll('[role=tab]').forEach(tab => {
    tab.onclick = () => {
        document.querySelectorAll('[role=tab]').forEach(t => t.classList.remove('tab-active'));
        tab.classList.add('tab-active');
        
        ['list', 'single', 'batch', 'text'].forEach(panel => {
            document.getElementById('panel-' + panel).classList.toggle('hidden', !tab.id.endsWith(panel));
        });
    };
});

// 版权信息
function addCopyright() {
    const div = document.createElement('div');
    div.style.cssText = 'position:fixed;top:0;left:0;width:100%;background-color:#f0f0f0;padding:10px;text-align:center;z-index:1000;box-shadow:0 2px 4px rgba(0,0,0,0.1);';
    div.innerHTML = `
        <span style="margin-right:15px;">作者 <a href="https://t.me/riniba" target="_blank" style="text-decoration:none;color:#0088cc;font-weight:bold;">@riniba</a></span>
        <span style="margin-right:15px;">开源地址 <a href="https://github.com/Riniba/TelegramMonitor" target="_blank" style="text-decoration:none;color:#0088cc;font-weight:bold;">GitHub</a></span>
        <span style="margin-right:15px;">交流群 <a href="https://t.me/RinibaGroup" target="_blank" style="text-decoration:none;color:#0088cc;font-weight:bold;">Telegram</a></span>
        <span><a href="https://github.com/Riniba/TelegramMonitor/wiki/%E5%85%B3%E9%94%AE%E8%AF%8D%E9%85%8D%E7%BD%AE%E4%BD%BF%E7%94%A8%E6%95%99%E7%A8%8B" target="_blank" style="text-decoration:none;color:#0088cc;font-weight:bold;">关键词配置说明</a></span>
    `;
    document.body.insertBefore(div, document.body.firstChild);
    document.body.style.paddingTop = div.offsetHeight + 10 + 'px';
}

document.addEventListener('DOMContentLoaded', addCopyright);

// 初始化
refresh();
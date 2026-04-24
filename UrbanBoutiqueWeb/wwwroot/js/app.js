// Urban Boutique - Shared client-side app
const API = '/api';
const cart = [];
let availableProducts = [];
let storefrontProducts = [];
let serverCategories = [];

// Common boutique-friendly color palette (name → hex)
const PRESET_COLORS = [
    { name: 'Black',     hex: '#0f172a', light: false },
    { name: 'White',     hex: '#ffffff', light: true  },
    { name: 'Gray',      hex: '#94a3b8', light: false },
    { name: 'Navy Blue', hex: '#1e3a8a', light: false },
    { name: 'Blue',      hex: '#3b82f6', light: false },
    { name: 'Sky',       hex: '#0ea5e9', light: false },
    { name: 'Red',       hex: '#ef4444', light: false },
    { name: 'Burgundy',  hex: '#991b1b', light: false },
    { name: 'Pink',      hex: '#ec4899', light: false },
    { name: 'Purple',    hex: '#8b5cf6', light: false },
    { name: 'Green',     hex: '#10b981', light: false },
    { name: 'Olive',     hex: '#65a30d', light: false },
    { name: 'Yellow',    hex: '#facc15', light: true  },
    { name: 'Orange',    hex: '#f97316', light: false },
    { name: 'Beige',     hex: '#e7d5b8', light: true  },
    { name: 'Brown',     hex: '#78350f', light: false }
];

function colorHex(name) {
    const preset = PRESET_COLORS.find(c => c.name.toLowerCase() === (name || '').toLowerCase());
    return preset ? preset.hex : '#94a3b8';
}

// ============ FETCH HELPER ============
async function api(path, options = {}) {
    const opts = {
        credentials: 'same-origin',
        headers: { 'Content-Type': 'application/json' },
        ...options
    };
    const res = await fetch(`${API}${path}`, opts);
    let data = null;
    try { data = await res.json(); } catch { /* ignore */ }
    if (res.status === 401) {
        // session lost — redirect to login (except on public pages)
        if (!location.pathname.endsWith('/login.html') && location.pathname !== '/') {
            location.href = '/login';
        }
    }
    return { ok: res.ok, status: res.status, data };
}

// ============ TOAST ============
function toast(message, type = 'success') {
    let wrap = document.getElementById('toast-wrap');
    if (!wrap) {
        wrap = document.createElement('div');
        wrap.id = 'toast-wrap';
        document.body.appendChild(wrap);
    }
    const el = document.createElement('div');
    el.className = `toast toast-${type}`;
    el.textContent = message;
    wrap.appendChild(el);
    setTimeout(() => el.classList.add('show'), 20);
    setTimeout(() => { el.classList.remove('show'); setTimeout(() => el.remove(), 300); }, 2800);
}

// ============ AUTH ============
function redirectByRole(role) {
    if (role === 'Admin') location.href = '/admin';
    else if (role === 'Sales Staff') location.href = '/cashier';
    else location.href = '/'; // Customer or unknown
}

function showAuthError(message) {
    const err = document.getElementById('error-message');
    if (err) {
        err.textContent = message || 'Request failed';
        err.style.display = 'block';
    } else {
        toast(message || 'Request failed', 'error');
    }
}

async function login(username, password) {
    const { ok, data } = await api('/auth/login', {
        method: 'POST', body: JSON.stringify({ username, password })
    });
    if (ok && data?.success) {
        localStorage.setItem('userRole', data.role);
        localStorage.setItem('userName', data.username);
        redirectByRole(data.role);
    } else {
        showAuthError(data?.message || 'Login failed');
    }
}

async function register(username, password) {
    const { ok, data } = await api('/auth/register', {
        method: 'POST', body: JSON.stringify({ username, password })
    });
    if (ok && data?.success) {
        localStorage.setItem('userRole', data.role);
        localStorage.setItem('userName', data.username);
        toast('Welcome to Urban Boutique!');
        setTimeout(() => redirectByRole(data.role), 600);
    } else {
        showAuthError(data?.message || 'Registration failed');
    }
}

async function logout() {
    await api('/auth/logout', { method: 'POST' });
    localStorage.removeItem('userRole');
    localStorage.removeItem('userName');
    // Stay on current page (storefront) or go to login for protected pages
    const protectedPaths = ['/admin', '/cashier', '/admin.html', '/cashier.html'];
    if (protectedPaths.some(p => location.pathname.startsWith(p))) {
        location.href = '/login';
    } else {
        location.reload();
    }
}

async function checkAuth(requiredRole) {
    const { ok, data } = await api('/auth/me');
    if (!ok || !data?.authenticated) { location.href = '/login'; return; }
    if (requiredRole && data.role !== requiredRole) { location.href = '/login'; return; }
    const nameEl = document.getElementById('display-user-name');
    if (nameEl) nameEl.textContent = data.username;
}

// Returns current user info without redirecting (used by storefront)
async function whoAmI() {
    const { ok, data } = await api('/auth/me');
    return (ok && data?.authenticated) ? data : null;
}

// ============ MODALS ============
function openModal(id) { document.getElementById(id)?.classList.add('active'); }
function closeModal(id) { document.getElementById(id)?.classList.remove('active'); }

// ============ ADMIN: TABS ============
function switchTab(tabId, el) {
    document.querySelectorAll('.tab-pane').forEach(t => t.style.display = 'none');
    document.querySelectorAll('.nav-menu li').forEach(l => l.classList.remove('active'));
    const pane = document.getElementById(`tab-${tabId}`);
    if (pane) pane.style.display = 'block';
    (el || event?.currentTarget)?.classList.add('active');

    if (tabId === 'products') loadProducts();
    if (tabId === 'categories') loadCategories();
    if (tabId === 'users') loadUsers();
    if (tabId === 'reports') loadReports();
}

async function loadAdminData() {
    await loadCategories();
    await loadProducts();
    initColorPicker();
}

// ============ COLOR PICKER ============
function initColorPicker() {
    const grid = document.getElementById('color-swatches');
    if (!grid || grid.dataset.initialized) return;
    grid.dataset.initialized = 'true';

    grid.innerHTML = '';
    PRESET_COLORS.forEach(c => {
        const sw = document.createElement('div');
        sw.className = 'swatch';
        sw.title = c.name;
        sw.style.background = c.hex;
        sw.dataset.name = c.name;
        sw.dataset.hex = c.hex;
        if (c.light) sw.dataset.light = 'true';
        sw.addEventListener('click', () => selectColor(c.name, c.hex));
        grid.appendChild(sw);
    });

    const custom = document.getElementById('p-color-custom');
    custom?.addEventListener('input', e => {
        selectColor('Custom', e.target.value, /*fromCustom*/ true);
    });

    const nameInput = document.getElementById('p-color');
    nameInput?.addEventListener('input', e => {
        const val = e.target.value.trim();
        // Try to match preset name
        const preset = PRESET_COLORS.find(p => p.name.toLowerCase() === val.toLowerCase());
        if (preset) {
            highlightSwatch(preset.name);
            document.getElementById('color-preview').style.background = preset.hex;
            document.getElementById('p-color-custom').value = preset.hex;
        } else {
            highlightSwatch(null);
        }
    });
}

function selectColor(name, hex, fromCustom = false) {
    const nameInput = document.getElementById('p-color');
    if (nameInput && !fromCustom) nameInput.value = name;
    if (nameInput && fromCustom && (!nameInput.value || PRESET_COLORS.some(p => p.name === nameInput.value))) {
        nameInput.value = '';
        nameInput.placeholder = 'Enter a name for this custom color...';
        nameInput.focus();
    }
    const preview = document.getElementById('color-preview');
    if (preview) preview.style.background = hex;
    const custom = document.getElementById('p-color-custom');
    if (custom && !fromCustom) custom.value = hex;
    highlightSwatch(fromCustom ? null : name);
}

function highlightSwatch(name) {
    document.querySelectorAll('#color-swatches .swatch').forEach(sw => {
        sw.classList.toggle('active', sw.dataset.name === name);
    });
}

function resetColorPicker() {
    document.getElementById('p-color').value = '';
    document.getElementById('p-color-custom').value = '#3b82f6';
    document.getElementById('color-preview').style.background = '#3b82f6';
    highlightSwatch(null);
}

// ============ ADMIN: PRODUCTS ============
async function loadProducts() {
    const { ok, data } = await api('/admin/products');
    if (!ok) return;
    const tbody = document.getElementById('products-table');
    if (!tbody) return;
    tbody.innerHTML = '';
    if (data.length === 0) {
        tbody.innerHTML = '<tr><td colspan="6" class="empty-state">No products yet. Add your first product.</td></tr>';
        return;
    }
    data.forEach(p => {
        const badge = p.isLowStock
            ? '<span class="badge badge-danger">Low Stock</span>'
            : '<span class="badge badge-success">In Stock</span>';
        const colorChip = `<span class="color-chip" style="--chip-color:${colorHex(p.color)}">${escapeHtml(p.color)}</span>`;
        tbody.innerHTML += `
            <tr>
                <td><strong>${escapeHtml(p.productName)}</strong></td>
                <td>${escapeHtml(p.category)}</td>
                <td>${escapeHtml(p.size)} · ${colorChip}</td>
                <td><strong>$${Number(p.price).toFixed(2)}</strong></td>
                <td>${p.stockQuantity}</td>
                <td>${badge}</td>
            </tr>`;
    });
}

// ============ ADMIN: CATEGORIES ============
async function loadCategories() {
    const { ok, data } = await api('/admin/categories');
    if (!ok) return;
    serverCategories = data;
    updateCategoryDropdowns();
    const tbody = document.getElementById('categories-table');
    if (tbody) {
        tbody.innerHTML = '';
        serverCategories.forEach(c => {
            tbody.innerHTML += `<tr><td><i class="fas fa-tag text-accent"></i> <strong>${escapeHtml(c)}</strong></td></tr>`;
        });
        if (serverCategories.length === 0)
            tbody.innerHTML = '<tr><td class="empty-state">No categories yet.</td></tr>';
    }
}

function updateCategoryDropdowns() {
    const pCat = document.getElementById('p-cat');
    if (!pCat) return;
    pCat.innerHTML = '';
    serverCategories.forEach(c => {
        pCat.innerHTML += `<option value="${escapeHtml(c)}">${escapeHtml(c)}</option>`;
    });
}

async function submitCategory(e) {
    e.preventDefault();
    const input = document.getElementById('new-cat-name');
    const name = input.value.trim();
    if (!name) return;
    const { ok, data } = await api('/admin/categories', { method: 'POST', body: JSON.stringify({ name }) });
    if (ok) { toast('Category added'); input.value = ''; loadCategories(); }
    else toast(data?.message || 'Failed', 'error');
}

async function submitProduct(e) {
    e.preventDefault();
    const body = {
        name: document.getElementById('p-name').value.trim(),
        price: parseFloat(document.getElementById('p-price').value),
        category: document.getElementById('p-cat').value,
        size: document.getElementById('p-size').value,
        color: document.getElementById('p-color').value.trim(),
        stockQuantity: parseInt(document.getElementById('p-stock').value, 10)
    };
    if (!body.name || !body.price || body.stockQuantity < 0) {
        toast('Please fill all fields correctly', 'error'); return;
    }
    const { ok, data } = await api('/admin/products', { method: 'POST', body: JSON.stringify(body) });
    if (ok) {
        toast('Product saved');
        e.target.reset();
        resetColorPicker();
        closeModal('productModal');
        loadProducts();
    } else toast(data?.message || 'Failed', 'error');
}

// ============ ADMIN: USERS ============
async function loadUsers() {
    const { ok, data } = await api('/admin/users');
    if (!ok) return;
    const tbody = document.getElementById('users-table');
    if (!tbody) return;
    tbody.innerHTML = '';
    data.forEach(u => {
        const roleBadge = u.role === 'Admin' ? 'badge-danger' : 'badge-success';
        tbody.innerHTML += `
            <tr>
                <td>#${u.userID}</td>
                <td><strong>${escapeHtml(u.username)}</strong></td>
                <td><span class="badge ${roleBadge}">${escapeHtml(u.role)}</span></td>
            </tr>`;
    });
    if (data.length === 0)
        tbody.innerHTML = '<tr><td colspan="3" class="empty-state">No users.</td></tr>';
}

async function addUser() {
    const username = document.getElementById('u-name').value.trim();
    const password = document.getElementById('u-pass').value;
    const role = document.getElementById('u-role').value;
    if (!username || !password) return toast('Fill all fields', 'error');
    const { ok, data } = await api('/admin/users', {
        method: 'POST', body: JSON.stringify({ username, password, role })
    });
    if (ok) { toast('User added'); closeModal('userModal'); loadUsers(); }
    else toast(data?.message || 'Failed', 'error');
}

async function resetUser() {
    const username = document.getElementById('u-name').value.trim();
    const password = document.getElementById('u-pass').value;
    if (!username || !password) return toast('Fill all fields', 'error');
    const { ok, data } = await api('/admin/users/reset', {
        method: 'POST', body: JSON.stringify({ username, password })
    });
    if (ok) { toast('Password reset'); closeModal('userModal'); }
    else toast(data?.message || 'Failed', 'error');
}

// ============ ADMIN: REPORTS ============
async function loadReports() {
    const { ok: okToday, data: today } = await api('/admin/reports/today');
    if (okToday) {
        const el = document.getElementById('today-sales');
        if (el) el.textContent = `$${Number(today.total).toFixed(2)}`;
        const cntEl = document.getElementById('today-count');
        if (cntEl) cntEl.textContent = today.count;
    }

    const { ok: okDead, data: dead } = await api('/admin/reports/deadstock');
    if (okDead) {
        const tbody = document.getElementById('deadstock-table');
        if (!tbody) return;
        tbody.innerHTML = '';
        if (dead.length === 0) {
            tbody.innerHTML = '<tr><td colspan="5" class="empty-state"><i class="fas fa-check-circle text-success"></i> Great — no dead stock.</td></tr>';
            return;
        }
        dead.forEach(d => {
            tbody.innerHTML += `
                <tr>
                    <td><strong>${escapeHtml(d.name)}</strong></td>
                    <td>${escapeHtml(d.category)}</td>
                    <td>${escapeHtml(d.size)} / ${escapeHtml(d.color)}</td>
                    <td class="text-danger"><strong>${d.stockQuantity}</strong></td>
                    <td>$${Number(d.price).toFixed(2)}</td>
                </tr>`;
        });
    }
}

// ============ CASHIER ============
async function loadCashierProducts(search = '') {
    const q = search ? `?search=${encodeURIComponent(search)}` : '';
    const { ok, data } = await api(`/cashier/products${q}`);
    if (!ok) return;
    availableProducts = data;
    renderCashierProducts();
}

function searchProducts() {
    loadCashierProducts(document.getElementById('search-input').value);
}

function renderCashierProducts() {
    const tbody = document.getElementById('cashier-products');
    if (!tbody) return;
    tbody.innerHTML = '';
    if (availableProducts.length === 0) {
        tbody.innerHTML = '<tr><td colspan="6" class="empty-state">No products found.</td></tr>';
        return;
    }
    availableProducts.forEach(p => {
        const lowStock = p.stockQuantity < 5 ? 'text-danger' : '';
        tbody.innerHTML += `
            <tr>
                <td><strong>${escapeHtml(p.productName)}</strong><br><small class="text-muted">${escapeHtml(p.category)}</small></td>
                <td>${escapeHtml(p.size)}</td>
                <td>${escapeHtml(p.color)}</td>
                <td><strong>$${Number(p.price).toFixed(2)}</strong></td>
                <td class="${lowStock}"><strong>${p.stockQuantity}</strong></td>
                <td><button class="btn btn-success btn-sm" onclick="addToCart(${p.variantID})"><i class="fas fa-plus"></i> Add</button></td>
            </tr>`;
    });
}

function addToCart(variantID) {
    const product = availableProducts.find(p => p.variantID === variantID);
    if (!product) return;
    const existing = cart.find(c => c.variantID === variantID);
    if (existing) {
        if (existing.quantity >= product.stockQuantity) {
            toast('Not enough stock', 'error'); return;
        }
        existing.quantity++;
    } else {
        cart.push({
            variantID: product.variantID,
            name: `${product.productName} (${product.size}/${product.color})`,
            price: product.price,
            quantity: 1,
            maxStock: product.stockQuantity
        });
    }
    renderCart();
}

function changeQty(variantID, delta) {
    const item = cart.find(c => c.variantID === variantID);
    if (!item) return;
    if (delta > 0 && item.quantity >= item.maxStock) { toast('Not enough stock', 'error'); return; }
    item.quantity += delta;
    if (item.quantity <= 0) {
        const i = cart.findIndex(c => c.variantID === variantID);
        cart.splice(i, 1);
    }
    renderCart();
}

function removeFromCart(variantID) {
    const i = cart.findIndex(c => c.variantID === variantID);
    if (i >= 0) { cart.splice(i, 1); renderCart(); }
}

function renderCart() {
    const tbody = document.getElementById('cart-table');
    if (!tbody) return;
    tbody.innerHTML = '';
    if (cart.length === 0) {
        tbody.innerHTML = '<tr><td colspan="4" class="empty-state">Cart is empty</td></tr>';
    } else {
        cart.forEach(c => {
            tbody.innerHTML += `
                <tr>
                    <td><strong>${escapeHtml(c.name)}</strong><br><small class="text-muted">$${Number(c.price).toFixed(2)}</small></td>
                    <td>
                        <div class="qty-controls">
                            <button onclick="changeQty(${c.variantID},-1)">−</button>
                            <span>${c.quantity}</span>
                            <button onclick="changeQty(${c.variantID},1)">+</button>
                        </div>
                    </td>
                    <td><strong>$${(c.price * c.quantity).toFixed(2)}</strong></td>
                    <td><button class="btn-icon btn-icon-danger" onclick="removeFromCart(${c.variantID})"><i class="fas fa-times"></i></button></td>
                </tr>`;
        });
    }
    const total = cart.reduce((s, c) => s + c.price * c.quantity, 0);
    const totalEl = document.getElementById('cart-total');
    if (totalEl) totalEl.textContent = `$${total.toFixed(2)}`;
}

async function checkout() {
    if (cart.length === 0) { toast('Cart is empty', 'error'); return; }
    const items = cart.map(c => ({ variantID: c.variantID, quantity: c.quantity }));
    const { ok, data } = await api('/cashier/checkout', {
        method: 'POST', body: JSON.stringify({ items })
    });
    if (ok) {
        toast(`Sale #${data.saleId} — $${Number(data.total).toFixed(2)}`);
        cart.length = 0;
        renderCart();
        loadCashierProducts(document.getElementById('search-input')?.value || '');
    } else {
        toast(data?.message || 'Checkout failed', 'error');
    }
}

// ============ STOREFRONT USER AREA ============
async function renderStorefrontUserArea() {
    const el = document.getElementById('storefront-user-area');
    if (!el) return;
    const me = await whoAmI();
    if (!me) {
        el.innerHTML = `
            <a href="/login" class="btn btn-secondary" style="margin-right:8px;">
                <i class="fas fa-sign-in-alt"></i> Sign In
            </a>
            <a href="/login" class="btn btn-primary" onclick="sessionStorage.setItem('auth_mode','register')">
                <i class="fas fa-user-plus"></i> Sign Up
            </a>`;
        return;
    }

    // Logged in — show user + role-appropriate shortcut
    const roleColor = me.role === 'Admin' ? 'badge-danger' : (me.role === 'Sales Staff' ? 'badge-warning' : 'badge-success');
    let shortcut = '';
    if (me.role === 'Admin')       shortcut = '<a href="/admin" class="btn btn-secondary"><i class="fas fa-user-shield"></i> Admin</a>';
    else if (me.role === 'Sales Staff') shortcut = '<a href="/cashier" class="btn btn-secondary"><i class="fas fa-cash-register"></i> Cashier</a>';

    el.innerHTML = `
        ${shortcut}
        <div class="avatar" style="margin-left:10px;"><i class="fas fa-user"></i></div>
        <div class="user-info">
            <span class="user-name">${escapeHtml(me.username)}</span>
            <span class="user-role"><span class="badge ${roleColor}" style="font-size:0.65rem;">${escapeHtml(me.role)}</span></span>
        </div>
        <button class="btn-logout" onclick="logout()" title="Sign out"><i class="fas fa-sign-out-alt"></i></button>
    `;
}

// ============ STOREFRONT (public) ============
async function loadStorefront() {
    const { ok, data } = await api('/cashier/products');
    if (!ok) return;
    storefrontProducts = data;
    renderStorefront(storefrontProducts);

    // populate sidebar categories from data
    const cats = [...new Set(data.map(p => p.category).filter(Boolean))];
    const list = document.getElementById('storefront-categories');
    if (list) {
        list.innerHTML = `<li class="active" onclick="filterStorefront('', this)">All Products</li>`;
        cats.forEach(c => {
            list.innerHTML += `<li onclick="filterStorefront('${escapeAttr(c)}', this)">${escapeHtml(c)}</li>`;
        });
    }
}

function renderStorefront(products) {
    const grid = document.getElementById('storefront-products');
    if (!grid) return;
    grid.innerHTML = '';
    if (products.length === 0) {
        grid.innerHTML = '<div class="empty-state-box"><i class="fas fa-box-open"></i><p>No products found</p></div>';
        return;
    }
    products.forEach(p => {
        const icon = pickIcon(p.category);
        const cHex = colorHex(p.color);
        grid.innerHTML += `
            <a class="product-card" href="/product.html?id=${p.variantID}">
                <div class="product-card-img"><i class="fas ${icon}"></i></div>
                <div class="product-card-body">
                    <div class="product-card-title">${escapeHtml(p.productName)}</div>
                    <div class="product-card-cat">
                        ${escapeHtml(p.category)} • ${escapeHtml(p.size)} •
                        <span class="color-chip" style="--chip-color:${cHex}">${escapeHtml(p.color)}</span>
                    </div>
                    <div class="product-card-price">$${Number(p.price).toFixed(2)}</div>
                </div>
            </a>`;
    });
}

function pickIcon(cat) {
    const c = (cat || '').toLowerCase();
    if (c.includes('cloth')) return 'fa-tshirt';
    if (c.includes('foot') || c.includes('shoe')) return 'fa-shoe-prints';
    if (c.includes('access')) return 'fa-glasses';
    return 'fa-box';
}

function filterStorefront(category, el) {
    document.querySelectorAll('#storefront-categories li').forEach(li => li.classList.remove('active'));
    (el || event?.currentTarget)?.classList.add('active');
    const filtered = !category ? storefrontProducts : storefrontProducts.filter(p => p.category === category);
    renderStorefront(filtered);
}

function searchStorefront() {
    const term = document.getElementById('store-search-input').value.toLowerCase();
    const filtered = storefrontProducts.filter(p =>
        p.productName.toLowerCase().includes(term) ||
        (p.category || '').toLowerCase().includes(term));
    renderStorefront(filtered);
}

function viewProduct(p) {
    // Navigate to dedicated product page
    location.href = `/product.html?id=${p.variantID}`;
}

// Single product page loader
async function loadSingleProduct(variantId) {
    const { ok, data } = await api(`/cashier/products/${variantId}`);
    const wrap = document.getElementById('product-content');
    if (!ok) {
        wrap.innerHTML = `<div class="empty-state-box">
            <i class="fas fa-exclamation-triangle"></i>
            <p>${escapeHtml(data?.message || 'Product not found')}</p>
            <a href="/" class="btn btn-primary" style="margin-top:20px; display:inline-flex;"><i class="fas fa-arrow-left"></i> Back</a>
        </div>`;
        return;
    }

    document.title = `${data.productName} — Urban Boutique`;
    const icon = pickIcon(data.category);
    const cHex = colorHex(data.color);
    const stockBadge = data.stockQuantity > 0
        ? `<span class="stock-pill in"><i class="fas fa-check-circle"></i> In Stock — ${data.stockQuantity} available</span>`
        : `<span class="stock-pill out"><i class="fas fa-times-circle"></i> Out of Stock</span>`;

    wrap.innerHTML = `
        <div class="product-detail">
            <div class="product-hero-img">
                <div class="product-hero-badges">
                    <span class="badge badge-success">${escapeHtml(data.category)}</span>
                    ${data.stockQuantity < 5 && data.stockQuantity > 0 ? '<span class="badge badge-warning">Only few left</span>' : ''}
                </div>
                <i class="fas ${icon}" style="position:relative; z-index:1;"></i>
            </div>
            <div class="product-detail-info">
                <div class="breadcrumbs">
                    <a href="/">Catalog</a>
                    <span> / ${escapeHtml(data.category)}</span>
                </div>
                <h1>${escapeHtml(data.productName)}</h1>
                ${stockBadge}
                <div class="product-detail-price">$${Number(data.price).toFixed(2)}</div>

                <div class="product-meta-grid">
                    <div class="meta-item">
                        <small>Size</small>
                        <strong>${escapeHtml(data.size)}</strong>
                    </div>
                    <div class="meta-item">
                        <small>Color</small>
                        <strong><span class="color-chip" style="--chip-color:${cHex}">${escapeHtml(data.color)}</span></strong>
                    </div>
                    <div class="meta-item">
                        <small>SKU</small>
                        <strong>#${data.variantID}</strong>
                    </div>
                </div>

                <div class="product-description">
                    <strong><i class="fas fa-info-circle"></i> About this item</strong><br>
                    Premium quality ${escapeHtml((data.category || '').toLowerCase())} from Urban Boutique's latest collection.
                    Designed for everyday urban style — comfortable, durable and on-trend.
                </div>

                <div class="product-cta">
                    <button class="btn btn-secondary" disabled>
                        <i class="fas fa-shopping-bag"></i> Available In-Store
                    </button>
                    <a href="/" class="btn btn-primary">
                        <i class="fas fa-th"></i> More Products
                    </a>
                </div>
            </div>
        </div>
    `;
}

// ============ UTILITIES ============
function escapeHtml(s) {
    if (s == null) return '';
    return String(s).replace(/[&<>"']/g, ch => ({
        '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;'
    }[ch]));
}
function escapeAttr(s) { return escapeHtml(s).replace(/'/g, "&#39;"); }

// Enter-key search on storefront
document.addEventListener('DOMContentLoaded', () => {
    document.getElementById('store-search-input')?.addEventListener('keyup', e => {
        if (e.key === 'Enter') searchStorefront();
    });
    document.getElementById('search-input')?.addEventListener('keyup', e => {
        if (e.key === 'Enter') searchProducts();
    });
    document.getElementById('category-form')?.addEventListener('submit', submitCategory);
    document.getElementById('product-form')?.addEventListener('submit', submitProduct);
});

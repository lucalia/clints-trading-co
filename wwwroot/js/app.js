window.initScrollEffects = function () {
    const header = document.querySelector('.app-header');
    if (!header) return;

    const updateHeaderHeight = () => {
        document.documentElement.style.setProperty('--header-h', header.getBoundingClientRect().height + 'px');
    };

    const onScroll = () => {
        header.classList.toggle('scrolled', window.scrollY > 30);
        updateHeaderHeight();
    };

    header.addEventListener('transitionend', updateHeaderHeight);
    window.addEventListener('scroll', onScroll, { passive: true });
    updateHeaderHeight();
    onScroll();
};

window.initSetHero = function () {
    const hero = document.getElementById('set-hero');
    if (!hero) return;
    const onScroll = () => hero.classList.toggle('compact', window.scrollY > 80);
    window.addEventListener('scroll', onScroll, { passive: true });
    onScroll();
};

window.lockScroll = function () {
    document.body.style.overflow = 'hidden';
};

window.unlockScroll = function () {
    document.body.style.overflow = '';
};

// ── Toast system ─────────────────────────────────────────────────────────
window.showToast = function (message, type) {
    type = type || 'info';
    var container = document.getElementById('toast-container');
    if (!container || !message) return;
    var el = document.createElement('div');
    el.className = 'toast toast-' + type;
    el.textContent = message;
    container.appendChild(el);
    setTimeout(function () {
        el.classList.add('toast-out');
        setTimeout(function () { el.remove(); }, 280);
    }, 4000);
};

document.addEventListener('showToast', function (evt) {
    var d = evt.detail || {};
    window.showToast(d.message, d.type);
});

window.closeAddCollection = function () {
    document.getElementById('add-collection-host').innerHTML = '';
    window.unlockScroll();
};

window.closeModal = function () {
    document.getElementById('modal-host').innerHTML = '';
    window.unlockScroll();
};

window.openZoom = function (src) {
    const overlay = document.getElementById('zoom-overlay');
    const img     = document.getElementById('zoom-img');
    if (!overlay || !img) return;
    img.src = src;
    overlay.style.display = 'flex';
};

window.closeZoom = function () {
    const overlay = document.getElementById('zoom-overlay');
    if (overlay) overlay.style.display = 'none';
};

window.activateTab = function (btn) {
    btn.closest('.modal-tabs')
        .querySelectorAll('.modal-tab')
        .forEach(b => b.classList.remove('active'));
    btn.classList.add('active');
};

window.setFilter = function (btn) {
    btn.closest('.filter-chips')
        .querySelectorAll('.filter-chip')
        .forEach(b => b.classList.remove('active'));
    btn.classList.add('active');
};

window.setCollectionVariant = function (btn, variant) {
    btn.closest('.variant-selector')
        .querySelectorAll('.variant-btn')
        .forEach(b => b.classList.remove('active'));
    btn.classList.add('active');
    const input = document.getElementById('col-variant');
    if (input) input.value = variant;
};

document.addEventListener('htmx:afterSwap', function (evt) {
    if (evt.target && evt.target.id === 'modal-host' && evt.target.innerHTML.trim()) {
        window.lockScroll();
    }
    if (evt.target && evt.target.id === 'add-collection-host' && evt.target.innerHTML.trim()) {
        window.lockScroll();
    }
});

document.addEventListener('htmx:configRequest', function (evt) {
    const token = document.querySelector('meta[name="__RequestVerificationToken"]')?.content;
    if (token) evt.detail.headers['RequestVerificationToken'] = token;
});

window.copyToClipboard = function (text) {
    navigator.clipboard.writeText(text).catch(() => {
        const el = document.createElement('textarea');
        el.value = text;
        document.body.appendChild(el);
        el.select();
        document.execCommand('copy');
        document.body.removeChild(el);
    });
};

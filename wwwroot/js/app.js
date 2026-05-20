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

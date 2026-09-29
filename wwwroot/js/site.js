// Please see documentation at https://learn.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

(() => {
    const loader = document.getElementById('purchasePageLoader');
    const message = document.getElementById('purchaseLoaderMessage');
    if (!loader) return;
    const show = (text = 'Procesando') => {
        if (message) message.textContent = text;
        loader.classList.add('is-visible');
        loader.setAttribute('aria-hidden', 'false');
    };
    const hide = () => {
        loader.classList.remove('is-visible');
        loader.setAttribute('aria-hidden', 'true');
    };
    window.PurchaseFlowLoader = { show, hide };
    window.addEventListener('pageshow', hide);
    document.addEventListener('submit', event => {
        const form = event.target;
        if (!(form instanceof HTMLFormElement) || !form.matches('[data-purchase-loading]') || !form.checkValidity()) return;
        show(form.dataset.purchaseLoading || 'Procesando');
    });
    document.addEventListener('click', event => {
        const link = event.target.closest('a[data-purchase-loading]');
        if (!link || event.defaultPrevented || event.ctrlKey || event.metaKey || event.shiftKey || link.target) return;
        show(link.dataset.purchaseLoading || 'Procesando');
    });
})();

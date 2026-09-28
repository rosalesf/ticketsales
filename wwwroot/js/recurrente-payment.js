(() => {
    const container = document.getElementById('recurrente-checkout-container');
    const message = document.getElementById('payment-message');
    const confirmation = document.getElementById('payment-confirmation');
    const tracker = document.getElementById('payment-tracker');
    if (!tracker) return;
    if (container && window.RecurrenteCheckout) {
        window.RecurrenteCheckout.load({
            url: container.dataset.checkoutUrl,
            onSuccess: () => { message.textContent = 'Estamos verificando tu pago. No necesitas pagar nuevamente.'; },
            onFailure: () => { message.textContent = 'El pago no se completó. Puedes intentar nuevamente en el formulario.'; },
            onPaymentInProgress: () => { message.textContent = 'Tu pago está en proceso. Espera la confirmación.'; }
        });
        const frame = container.querySelector('iframe');
        if (frame) frame.title = 'Formulario seguro de pago de Recurrente';
    } else if (container) {
        message.textContent = 'No se pudo cargar el formulario. Utiliza el enlace para abrirlo en Recurrente.';
    }
    let attempts = 0;
    async function poll() {
        try {
            const response = await fetch(tracker.dataset.statusUrl, { cache: 'no-store', credentials: 'same-origin' });
            if (response.status === 404 || response.status === 401) return;
            if (response.ok) {
                const data = await response.json();
                if (['Approved', 'Expired', 'Review'].includes(data.status)) {
                    window.location.replace(confirmation.href);
                    return;
                }
            }
        } catch { /* Keep the checkout usable during a temporary network outage. */ }
        if (++attempts < 300) window.setTimeout(poll, 5000);
        else message.textContent = 'La confirmación está tardando. Consulta tu compra antes de intentar otro pago.';
    }
    window.setTimeout(poll, 3000);
})();

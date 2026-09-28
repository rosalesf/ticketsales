namespace TicketSales.Services;

public static class PurchaseConfirmationEmailTemplateDefaults
{
    public const string Html = """
        <html><body style="margin:0;background:#f8f8f3;color:#17201c;font-family:Arial,sans-serif"><div style="max-width:620px;margin:0 auto;padding:32px 20px"><div style="background:#17201c;color:#fff;padding:26px 30px;border-radius:14px 14px 0 0"><div style="color:#d9fb55;font-size:12px;letter-spacing:1.5px">VENTATICKETS</div><h1 style="margin:14px 0 0;font-size:29px">Tu compra está confirmada</h1></div><div style="background:#fff;border:1px solid #dfe3da;border-top:0;border-radius:0 0 14px 14px;padding:28px 30px"><p>Hola {{CustomerName}},</p><p>Tu pago fue aprobado. Conserva esta información para identificar tu compra.</p><div style="background:#f4f5f0;border-radius:8px;padding:15px 17px;margin:22px 0"><strong>Orden: {{OrderNumber}}</strong><br><span style="color:#69716d;font-size:13px">Referencia: {{PaymentReference}}</span></div><table style="width:100%;border-collapse:collapse;font-size:14px">{{OrderItems}}</table><div style="padding-top:19px;font-size:17px"><strong>Total pagado: {{Total}}</strong></div>{{QrCode}}<p style="margin:20px 0 0;color:#69716d;font-size:13px">Gracias por comprar con VentaTickets.</p></div></div></body></html>
        """;
}

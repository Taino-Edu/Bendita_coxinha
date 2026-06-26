// =============================================================================
// EmailService.cs â€” Envio de emails via SMTP
//
// ConfiguraÃ§Ã£o (appsettings.json ou variÃ¡veis de ambiente):
//   EmailSettings__Host     â†’ smtp.gmail.com  (ou smtp.sendgrid.net etc.)
//   EmailSettings__Port     â†’ 587
//   EmailSettings__User     â†’ seu@email.com
//   EmailSettings__Password â†’ senha-de-app ou api-key
//   EmailSettings__From     â†’ noreply@softnerd.com.br
//   EmailSettings__AppUrl   â†’ https://softnerd.com.br (para montar o link de reset)
//
// Para Gmail: ative "Senhas de app" nas configuraÃ§Ãµes da conta Google.
// Para SendGrid: use smtp.sendgrid.net:587, usuÃ¡rio "apikey", senha = API Key.
// =============================================================================

using System.Net;
using System.Net.Mail;
using BenditaCoxinha.Services.Interfaces;

namespace BenditaCoxinha.Services.Implementations;

public class EmailService : IEmailService
{
    private readonly IConfiguration         _config;
    private readonly ILogger<EmailService>  _logger;

    public EmailService(IConfiguration config, ILogger<EmailService> logger)
    {
        _config = config;
        _logger = logger;
    }

    public async Task SendPasswordResetAsync(string toEmail, string toName, string resetToken)
    {
        var appUrl = _config["EmailSettings:AppUrl"] ?? "http://localhost:3000";
        var link   = $"{appUrl}/reset-password?token={Uri.EscapeDataString(resetToken)}";

        var body = $"""
            <p>OlÃ¡, <strong>{toName}</strong>!</p>
            <p>Recebemos uma solicitaÃ§Ã£o de redefiniÃ§Ã£o de senha para sua conta no <strong>softNerd</strong>.</p>
            <p>
              <a href="{link}" style="background:#f59e0b;color:#000;padding:12px 24px;border-radius:8px;text-decoration:none;font-weight:bold;">
                Redefinir minha senha
              </a>
            </p>
            <p style="color:#888;font-size:12px;">
              Este link expira em 2 horas. Se vocÃª nÃ£o solicitou a redefiniÃ§Ã£o, ignore este email.
            </p>
            """;

        await SendAsync(toEmail, toName, "RedefiniÃ§Ã£o de senha â€” softNerd", body);
    }

    public async Task SendWelcomeAsync(string toEmail, string toName)
    {
        var body = $"""
            <p>OlÃ¡, <strong>{toName}</strong>! Seja bem-vindo(a) ao softNerd!</p>
            <p>Seu cadastro foi criado automaticamente ao escanear o QR Code da mesa.</p>
            <p>Acumule pontos a cada visita e troque por produtos na loja.</p>
            <p style="color:#888;font-size:12px;">
              DÃºvidas? Fale com o Maikon no balcÃ£o.
            </p>
            """;

        await SendAsync(toEmail, toName, "Bem-vindo(a) ao softNerd!", body);
    }

    public async Task SendCrediarioAbertoAsync(string toEmail, string toName, decimal valor, DateTime vencimento)
    {
        var venc = vencimento.ToLocalTime().ToString("dd/MM/yyyy");
        var body = $"""
            <div style="font-family:sans-serif;max-width:500px">
              <h2 style="color:#7839F3">softNerd â€” CrediÃ¡rio Aberto</h2>
              <p>OlÃ¡, <strong>{toName}</strong>!</p>
              <p>
                Uma comanda foi registrada no seu crediÃ¡rio.
                Por favor, efetue o pagamento atÃ© a data de vencimento.
              </p>
              <table style="width:100%;border-collapse:collapse;margin:16px 0">
                <tr>
                  <td style="padding:8px;color:#666">Valor</td>
                  <td style="padding:8px;font-weight:bold;color:#111">R$ {valor:N2}</td>
                </tr>
                <tr style="background:#f9f9f9">
                  <td style="padding:8px;color:#666">Vencimento</td>
                  <td style="padding:8px;font-weight:bold;color:#dc2626">{venc}</td>
                </tr>
              </table>
              <p>
                Enquanto o crediÃ¡rio estiver em aberto, novas comandas ficarÃ£o bloqueadas.
                CompareÃ§a Ã  loja ou fale com o Maikon para quitar.
              </p>
              <p style="color:#888;font-size:12px">softNerd â€” Sistema de GestÃ£o</p>
            </div>
            """;

        await SendAsync(toEmail, toName, $"CrediÃ¡rio aberto â€” R$ {valor:N2} vence em {venc}", body);
    }

    public async Task SendCrediarioPagoAsync(string toEmail, string toName, decimal valor)
    {
        var body = $"""
            <div style="font-family:sans-serif;max-width:500px">
              <h2 style="color:#00F0A8">softNerd â€” CrediÃ¡rio Quitado</h2>
              <p>OlÃ¡, <strong>{toName}</strong>!</p>
              <p>
                Seu crediÃ¡rio de <strong>R$ {valor:N2}</strong> foi quitado com sucesso.
                Obrigado pelo pagamento!
              </p>
              <p>VocÃª jÃ¡ pode abrir uma nova comanda normalmente.</p>
              <p style="color:#888;font-size:12px">softNerd â€” Sistema de GestÃ£o</p>
            </div>
            """;

        await SendAsync(toEmail, toName, "CrediÃ¡rio quitado â€” softNerd", body);
    }

    public async Task SendCampeonatoInscricaoAsync(string toEmail, string toName, string campeonato, DateTime data, decimal entryFee)
    {
        var dataFmt = data.ToLocalTime().ToString("dd/MM/yyyy 'Ã s' HH:mm");
        var body = $"""
            <div style="font-family:sans-serif;max-width:500px">
              <h2 style="color:#7839F3">softNerd â€” InscriÃ§Ã£o Confirmada</h2>
              <p>OlÃ¡, <strong>{toName}</strong>!</p>
              <p>Sua inscriÃ§Ã£o no campeonato abaixo foi confirmada:</p>
              <table style="width:100%;border-collapse:collapse;margin:16px 0">
                <tr>
                  <td style="padding:8px;color:#666">Campeonato</td>
                  <td style="padding:8px;font-weight:bold">{campeonato}</td>
                </tr>
                <tr style="background:#f9f9f9">
                  <td style="padding:8px;color:#666">Data</td>
                  <td style="padding:8px;font-weight:bold">{dataFmt}</td>
                </tr>
                <tr>
                  <td style="padding:8px;color:#666">Taxa de InscriÃ§Ã£o</td>
                  <td style="padding:8px;font-weight:bold">R$ {entryFee:N2}</td>
                </tr>
              </table>
              <p>ApareÃ§a na loja no dia do evento. Boa sorte!</p>
              <p style="color:#888;font-size:12px">softNerd â€” Sistema de GestÃ£o</p>
            </div>
            """;

        await SendAsync(toEmail, toName, $"InscriÃ§Ã£o confirmada: {campeonato}", body);
    }

    public async Task SendAnuncioAsync(IEnumerable<(string email, string name)> destinatarios, string titulo, string corpo)
    {
        var body = $"""
            <div style="font-family:sans-serif;max-width:500px">
              <h2 style="color:#7839F3">softNerd â€” {titulo}</h2>
              <div style="margin:16px 0;color:#333">
                {corpo}
              </div>
              <p style="color:#888;font-size:12px">
                VocÃª recebe este email por ser cliente softNerd.<br/>
                DÃºvidas? Fale com o Maikon no balcÃ£o.
              </p>
            </div>
            """;

        foreach (var (email, name) in destinatarios)
            await SendAsync(email, name, $"softNerd â€” {titulo}", body);
    }

    // â”€â”€ LGPD â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    public async Task SendLgpdConfirmationAsync(
        string   toEmail,
        string   toName,
        string   protocol,
        string   requestType,
        DateTime deadline)
    {
        var prazo = deadline.ToLocalTime().ToString("dd/MM/yyyy");
        var body = $"""
            <div style="font-family:sans-serif;max-width:560px;color:#222">
              <h2 style="color:#7839F3">softNerd â€” SolicitaÃ§Ã£o LGPD Recebida</h2>
              <p>OlÃ¡, <strong>{toName}</strong>!</p>
              <p>
                Sua solicitaÃ§Ã£o de <strong>{requestType}</strong> de dados pessoais foi recebida
                com sucesso pela <strong>softNerd</strong>.
              </p>
              <table style="width:100%;border-collapse:collapse;margin:20px 0;font-size:14px">
                <tr style="background:#f5f0ff">
                  <td style="padding:10px 14px;color:#555;width:40%">NÃºmero de Protocolo</td>
                  <td style="padding:10px 14px;font-weight:bold;font-family:monospace">{protocol}</td>
                </tr>
                <tr>
                  <td style="padding:10px 14px;color:#555">Tipo de SolicitaÃ§Ã£o</td>
                  <td style="padding:10px 14px;font-weight:bold">{requestType}</td>
                </tr>
                <tr style="background:#f5f0ff">
                  <td style="padding:10px 14px;color:#555">Prazo de Resposta</td>
                  <td style="padding:10px 14px;font-weight:bold;color:#dc2626">{prazo}</td>
                </tr>
              </table>
              <p>
                Nos termos da Lei Geral de ProteÃ§Ã£o de Dados (LGPD â€” Lei 13.709/2018, Art. 18 Â§ 5Â°),
                sua solicitaÃ§Ã£o serÃ¡ respondida em atÃ© <strong>15 dias corridos</strong>.
              </p>
              <p>
                Guarde seu nÃºmero de protocolo para acompanhar o andamento em:
                <br/>
                <a href="https://softnerd.com.br/lgpd" style="color:#7839F3">softnerd.com.br/lgpd</a>
              </p>
              <hr style="border:none;border-top:1px solid #eee;margin:24px 0"/>
              <p style="color:#888;font-size:12px">
                DÃºvidas? Entre em contato: <a href="mailto:privacidade@softnerd.com.br">privacidade@softnerd.com.br</a><br/>
                softNerd â€” SÃ£o JosÃ© do Rio Preto, SP
              </p>
            </div>
            """;

        await SendAsync(toEmail, toName, $"SolicitaÃ§Ã£o LGPD recebida â€” Protocolo {protocol}", body);
    }

    public async Task SendLgpdResponseAsync(
        string toEmail,
        string toName,
        string protocol,
        string requestType,
        string response)
    {
        var body = $"""
            <div style="font-family:sans-serif;max-width:560px;color:#222">
              <h2 style="color:#7839F3">softNerd â€” Resposta Ã  sua SolicitaÃ§Ã£o LGPD</h2>
              <p>OlÃ¡, <strong>{toName}</strong>!</p>
              <p>
                Sua solicitaÃ§Ã£o de <strong>{requestType}</strong> (Protocolo: <code>{protocol}</code>)
                foi analisada e respondida pela <strong>softNerd</strong>.
              </p>
              <div style="background:#f5f0ff;border-left:4px solid #7839F3;padding:16px;margin:20px 0;border-radius:4px">
                <p style="margin:0;font-weight:bold;color:#555;font-size:13px;margin-bottom:8px">RESPOSTA DA SOFTNERD:</p>
                <p style="margin:0;white-space:pre-wrap">{response}</p>
              </div>
              <p>
                Caso nÃ£o esteja satisfeito(a) com a resposta, vocÃª tem o direito de apresentar
                reclamaÃ§Ã£o Ã  Autoridade Nacional de ProteÃ§Ã£o de Dados (ANPD) atravÃ©s do portal:
                <a href="https://www.gov.br/anpd" style="color:#7839F3">www.gov.br/anpd</a>
              </p>
              <hr style="border:none;border-top:1px solid #eee;margin:24px 0"/>
              <p style="color:#888;font-size:12px">
                DÃºvidas? Entre em contato: <a href="mailto:privacidade@softnerd.com.br">privacidade@softnerd.com.br</a><br/>
                softNerd â€” SÃ£o JosÃ© do Rio Preto, SP
              </p>
            </div>
            """;

        await SendAsync(toEmail, toName, $"Resposta Ã  sua solicitaÃ§Ã£o LGPD â€” Protocolo {protocol}", body);
    }

    public async Task<bool> SendDiagnosticEmailAsync(string toEmail)
    {
        var body = $"""
            <h2>Teste de DiagnÃ³stico â€” softNerd</h2>
            <p>Se vocÃª estÃ¡ lendo isso, a configuraÃ§Ã£o de SMTP do servidor estÃ¡ <strong>funcional</strong>!</p>
            <hr/>
            <p><strong>Timestamp:</strong> {DateTime.Now:dd/MM/yyyy HH:mm:ss}</p>
            <p><strong>Servidor:</strong> {Environment.MachineName}</p>
            """;

        try
        {
            await SendAsync(toEmail, "Admin Teste", "DiagnÃ³stico de Email â€” softNerd", body);
            return true;
        }
        catch
        {
            return false;
        }
    }

    // â”€â”€ Interno â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    private async Task SendAsync(string toEmail, string toName, string subject, string htmlBody)
    {
        var host     = _config["EmailSettings:Host"];
        var portStr  = _config["EmailSettings:Port"];
        var user     = _config["EmailSettings:User"];
        var password = _config["EmailSettings:Password"];
        var from     = _config["EmailSettings:From"] ?? user;

        // Se email nÃ£o estiver configurado, loga e retorna sem erro â€”
        // o sistema funciona sem email em dev/testes.
        if (string.IsNullOrWhiteSpace(host) || string.IsNullOrWhiteSpace(user))
        {
            _logger.LogWarning(
                "EmailService: configuraÃ§Ã£o ausente. Email para {To} ('{Subject}') nÃ£o foi enviado.",
                toEmail, subject);
            return;
        }

        try
        {
            var port   = int.TryParse(portStr, out var p) ? p : 587;
            using var client = new SmtpClient(host, port)
            {
                Credentials       = new NetworkCredential(user, password),
                EnableSsl         = true,
                DeliveryMethod    = SmtpDeliveryMethod.Network,
            };

            using var msg = new MailMessage
            {
                From       = new MailAddress(from!, "softNerd"),
                Subject    = subject,
                Body       = htmlBody,
                IsBodyHtml = true,
            };
            msg.To.Add(new MailAddress(toEmail, toName));

            await client.SendMailAsync(msg);
            _logger.LogInformation("Email '{Subject}' enviado para {To}", subject, toEmail);
        }
        catch (Exception ex)
        {
            // Falha de email nÃ£o derruba o fluxo principal
            _logger.LogError(ex, "Falha ao enviar email '{Subject}' para {To}", subject, toEmail);
        }
    }
}


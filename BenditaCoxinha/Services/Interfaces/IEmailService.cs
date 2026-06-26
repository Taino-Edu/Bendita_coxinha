// =============================================================================
// IEmailService.cs â€” Contrato de envio de emails do sistema
// =============================================================================

namespace BenditaCoxinha.Services.Interfaces;

public interface IEmailService
{
    // â”€â”€ AutenticaÃ§Ã£o â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    /// <summary>Envia email de recuperaÃ§Ã£o de senha com link contendo o token.</summary>
    Task SendPasswordResetAsync(string toEmail, string toName, string resetToken);

    /// <summary>Envia email de boas-vindas apÃ³s primeiro login via QR Code.</summary>
    Task SendWelcomeAsync(string toEmail, string toName);

    // â”€â”€ CrediÃ¡rio â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    /// <summary>Notifica o cliente que uma comanda foi lanÃ§ada no crediÃ¡rio.</summary>
    Task SendCrediarioAbertoAsync(string toEmail, string toName, decimal valor, DateTime vencimento);

    /// <summary>Notifica o cliente que seu crediÃ¡rio foi quitado.</summary>
    Task SendCrediarioPagoAsync(string toEmail, string toName, decimal valor);

    // â”€â”€ Campeonatos â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    /// <summary>ConfirmaÃ§Ã£o de inscriÃ§Ã£o em campeonato.</summary>
    Task SendCampeonatoInscricaoAsync(string toEmail, string toName, string campeonato, DateTime data, decimal entryFee);

    // â”€â”€ AnÃºncios (broadcast) â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    /// <summary>Envia anÃºncio/promoÃ§Ã£o para uma lista de destinatÃ¡rios.</summary>
    Task SendAnuncioAsync(IEnumerable<(string email, string name)> destinatarios, string titulo, string corpo);

    // â”€â”€ LGPD â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    /// <summary>
    /// Confirma ao solicitante o recebimento da solicitaÃ§Ã£o LGPD com nÃºmero de protocolo e prazo.
    /// </summary>
    Task SendLgpdConfirmationAsync(string toEmail, string toName, string protocol,
                                   string requestType, DateTime deadline);

    /// <summary>
    /// Envia ao solicitante a resposta formal do responsÃ¡vel pelo tratamento de dados.
    /// </summary>
    Task SendLgpdResponseAsync(string toEmail, string toName, string protocol,
                                string requestType, string response);

    /// <summary>Envia um email de diagnÃ³stico para testar as configuraÃ§Ãµes de SMTP.</summary>
    Task<bool> SendDiagnosticEmailAsync(string toEmail);
}


using BenditaCoxinha.DTOs;

namespace BenditaCoxinha.Services.Interfaces;

public interface IAnnouncementService
{
    /// <summary>Retorna todos os anÃºncios visÃ­veis (ativos + dentro do prazo). PÃºblico.</summary>
    Task<IEnumerable<AnnouncementDto>> GetVisibleAsync();

    /// <summary>Retorna todos os anÃºncios (ativos e inativos). Admin only.</summary>
    Task<IEnumerable<AnnouncementDto>> GetAllAsync();

    Task<AnnouncementDto> CreateAsync(CreateAnnouncementRequest request, Guid adminId);
    Task<AnnouncementDto> UpdateAsync(Guid id, UpdateAnnouncementRequest request);
    Task DeleteAsync(Guid id);
}


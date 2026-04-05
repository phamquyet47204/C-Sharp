using VinhKhanh.Domain.Interfaces;

namespace VinhKhanh.Application.UseCases;

public class AdminApproveUseCase(IPoiRepository repository)
{
    public async Task<bool> ExecuteAsync(int poiId, CancellationToken cancellationToken = default)
    {
        return await repository.ApprovePoiAsync(poiId, cancellationToken);
    }
}

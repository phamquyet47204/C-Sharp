using System.Threading.Tasks;

namespace VinhKhanhFoodStreet.Services;

public partial class NarrationService
{
    // Mac dinh khong lam gi tren non-Android.
    private partial Task BeginAudioDuckingAsync()
    {
        return Task.CompletedTask;
    }

    private partial void EndAudioDucking()
    {
    }
}

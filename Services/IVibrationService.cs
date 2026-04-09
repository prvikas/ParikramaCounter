using System.Threading.Tasks;

namespace ParikramaCounter.Services
{
    // Fix #5 (ViewModel responsibility): vibration logic extracted from
    // TrackingViewModel into its own service. Reads configuration from
    // IAppPreferences so TrackingViewModel has no vibration implementation details.
    public interface IVibrationService
    {
        void VibrateThirdSide();
        void VibrateApproachingStart();
        void VibrateCompletion();
        Task VibrateTargetReachedAsync();
    }
}

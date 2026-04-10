using Microsoft.Maui.Storage;

namespace ParikramaCounter.Services
{
    public class AppPreferences : IAppPreferences
    {
        private static class Keys
        {
            public const string TargetParikrama       = "pref_target";
            public const string ParikramaCount        = "pref_count";
            public const string ActiveTempleId        = "pref_temple_id";
            public const string IsDescendingMode      = "pref_descending";
            public const string AutoCountingEnabled   = "pref_autocounting";
            public const string EnableVibrations      = "pref_vib_enabled";
            public const string ThirdSideVibrationMs  = "pref_vib_third";
            public const string ApproachingStartVibMs = "pref_vib_approach";
            public const string CompletionVibrationMs = "pref_vib_complete";
            public const string TargetVibrationMs     = "pref_vib_target_ms";
            public const string TargetVibrationCount  = "pref_vib_target_n";
            public const string StepThreshold         = "pref_step_thresh";
            public const string MinStepInterval       = "pref_step_interval";
        }

        // Hot-path cache (read every sensor tick)
        private bool _isDescendingMode;
        private bool _autoCountingEnabled;

        public AppPreferences()
        {
            _isDescendingMode    = Preferences.Get(Keys.IsDescendingMode,    false);
            _autoCountingEnabled = Preferences.Get(Keys.AutoCountingEnabled,  true);
        }

        // ISessionState
        public int    TargetParikrama  { get => Preferences.Get(Keys.TargetParikrama, 7);   set => Preferences.Set(Keys.TargetParikrama, value); }
        public int    ParikramaCount   { get => Preferences.Get(Keys.ParikramaCount,  0);   set => Preferences.Set(Keys.ParikramaCount, value); }
        public string? ActiveTempleId  { get => Preferences.Get(Keys.ActiveTempleId, (string?)null); set => Preferences.Set(Keys.ActiveTempleId, value); }

        // IUserPreferences (hot-path cached)
        public bool IsDescendingMode
        {
            get => _isDescendingMode;
            set { _isDescendingMode = value; Preferences.Set(Keys.IsDescendingMode, value); }
        }
        public bool AutoCountingEnabled
        {
            get => _autoCountingEnabled;
            set { _autoCountingEnabled = value; Preferences.Set(Keys.AutoCountingEnabled, value); }
        }

        // IUserPreferences (standard)
        public bool EnableVibrations         { get => Preferences.Get(Keys.EnableVibrations,       true); set => Preferences.Set(Keys.EnableVibrations, value); }
        public int  ThirdSideVibrationMs     { get => Preferences.Get(Keys.ThirdSideVibrationMs,    400); set => Preferences.Set(Keys.ThirdSideVibrationMs, value); }
        public int  ApproachingStartVibrationMs { get => Preferences.Get(Keys.ApproachingStartVibMs, 200); set => Preferences.Set(Keys.ApproachingStartVibMs, value); }
        public int  CompletionVibrationMs    { get => Preferences.Get(Keys.CompletionVibrationMs,   500); set => Preferences.Set(Keys.CompletionVibrationMs, value); }
        public int  TargetVibrationMs        { get => Preferences.Get(Keys.TargetVibrationMs,       300); set => Preferences.Set(Keys.TargetVibrationMs, value); }
        public int  TargetVibrationCount     { get => Preferences.Get(Keys.TargetVibrationCount,      3); set => Preferences.Set(Keys.TargetVibrationCount, value); }

        // ISensorConfiguration
        public int  StepThreshold            { get => Preferences.Get(Keys.StepThreshold,           120); set => Preferences.Set(Keys.StepThreshold, value); }
        public int  MinStepInterval          { get => Preferences.Get(Keys.MinStepInterval,          250); set => Preferences.Set(Keys.MinStepInterval, value); }
    }
}

namespace ParikramaCounter.Services
{
    // Fix #6 (scattered Preferences): all preference keys in one place.
    // Typed properties eliminate string-key typos and make all persisted state
    // discoverable. Changing storage mechanism (e.g. to SecureStorage or SQLite)
    // only requires changing the implementation, not every call site.
    public interface IAppPreferences
    {
        // Session state
        int  TargetParikrama     { get; set; }
        int  ParikramaCount      { get; set; }

        // Counting behaviour
        bool IsDescendingMode    { get; set; }
        bool AutoCountingEnabled { get; set; }

        // Vibration
        bool EnableVibrations            { get; set; }
        int  ThirdSideVibrationMs        { get; set; }
        int  ApproachingStartVibrationMs { get; set; }
        int  CompletionVibrationMs       { get; set; }
        int  TargetVibrationMs           { get; set; }
        int  TargetVibrationCount        { get; set; }

        // Step detection
        int StepThreshold   { get; set; }
        int MinStepInterval { get; set; }
    }
}

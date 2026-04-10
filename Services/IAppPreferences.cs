namespace ParikramaCounter.Services
{
    // Fix #3: split into three focused interfaces with different lifetimes
    // and different consumers. IAppPreferences is a composed facade for
    // DI registration — implementations implement all three.

    // Runtime session state — changes on every count update.
    public interface ISessionState
    {
        int    TargetParikrama   { get; set; }
        int    ParikramaCount    { get; set; }
        string? ActiveTempleId   { get; set; }
    }

    // User-facing settings — changes only when user visits Settings page.
    public interface IUserPreferences
    {
        bool IsDescendingMode    { get; set; }
        bool AutoCountingEnabled { get; set; }
        bool EnableVibrations            { get; set; }
        int  ThirdSideVibrationMs        { get; set; }
        int  ApproachingStartVibrationMs { get; set; }
        int  CompletionVibrationMs       { get; set; }
        int  TargetVibrationMs           { get; set; }
        int  TargetVibrationCount        { get; set; }
    }

    // Algorithm tuning — changes only during expert calibration.
    public interface ISensorConfiguration
    {
        int StepThreshold   { get; set; }
        int MinStepInterval { get; set; }
    }

    // Composed facade — single DI registration, implements all three.
    public interface IAppPreferences : ISessionState, IUserPreferences, ISensorConfiguration { }
}

using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Input;
using Microsoft.Extensions.Logging;
using Microsoft.Maui.Controls;
using ParikramaCounter.Domain;
using ParikramaCounter.Repositories;
using ParikramaCounter.Services;

namespace ParikramaCounter.ViewModels
{
    // Manages the temple list and the active temple selection.
    // Creating/editing/deleting temples and viewing per-temple heading data.
    public class TempleViewModel : INotifyPropertyChanged
    {
        private readonly ITempleRepository           templeRepo;
        private readonly IPradhakshinaSessionService session;
        private readonly ILogger<TempleViewModel>    logger;

        private Temple?  selectedTemple;
        private bool     isLoading;
        private string   newTempleName     = string.Empty;
        private string   newTempleLocation = string.Empty;

        public ObservableCollection<Temple> Temples { get; } = new ObservableCollection<Temple>();

        public Temple? SelectedTemple
        {
            get => selectedTemple;
            set
            {
                selectedTemple = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(HasActiveTemple));
                OnPropertyChanged(nameof(ActiveTempleSummary));
                OnPropertyChanged(nameof(HeadingRows));
                // Push selection into the session service so next StartTracking uses it
                session.SetActiveTemple(value?.Id, value?.Name);
                logger.LogInformation("Active temple set: {Name}", value?.Name ?? "none");
            }
        }

        public bool   IsLoading          { get => isLoading;          set { isLoading = value;          OnPropertyChanged(); } }
        public bool   HasActiveTemple    => selectedTemple != null;
        public string NewTempleName      { get => newTempleName;      set { newTempleName = value;      OnPropertyChanged(); } }
        public string NewTempleLocation  { get => newTempleLocation;  set { newTempleLocation = value;  OnPropertyChanged(); } }

        // Summary label for the active temple card on the Tracking page
        public string ActiveTempleSummary => selectedTemple != null
            ? $"{selectedTemple.Name} — {selectedTemple.Location}"
            : "No temple selected";

        // Heading distribution rows for display (36 buckets × 10°)
        public ObservableCollection<HeadingRow> HeadingRows { get; } = new ObservableCollection<HeadingRow>();

        public ICommand LoadCommand         { get; }
        public ICommand AddTempleCommand    { get; }
        public ICommand DeleteTempleCommand { get; }
        public ICommand ClearSelectionCommand { get; }

        public TempleViewModel(
            ITempleRepository            templeRepo,
            IPradhakshinaSessionService  session,
            ILogger<TempleViewModel>     logger)
        {
            this.templeRepo = templeRepo ?? throw new ArgumentNullException(nameof(templeRepo));
            this.session    = session    ?? throw new ArgumentNullException(nameof(session));
            this.logger     = logger     ?? throw new ArgumentNullException(nameof(logger));

            LoadCommand           = new Command(async () => await LoadAsync());
            AddTempleCommand      = new Command(async () => await AddTempleAsync(), () => !string.IsNullOrWhiteSpace(NewTempleName));
            DeleteTempleCommand   = new Command<Temple>(async t => await DeleteTempleAsync(t));
            ClearSelectionCommand = new Command(() => SelectedTemple = null);
        }

        public async Task LoadAsync()
        {
            IsLoading = true;
            try
            {
                var temples = await templeRepo.GetAllAsync();
                Temples.Clear();
                foreach (var t in temples)
                    Temples.Add(t);

                // Restore previously selected temple
                string? savedId = session.ActiveTempleId;
                if (savedId != null)
                {
                    foreach (var t in Temples)
                        if (t.Id == savedId) { selectedTemple = t; break; }
                    OnPropertyChanged(nameof(SelectedTemple));
                    OnPropertyChanged(nameof(HasActiveTemple));
                    OnPropertyChanged(nameof(ActiveTempleSummary));
                    RefreshHeadingRows();
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to load temples");
            }
            finally { IsLoading = false; }
        }

        private async Task AddTempleAsync()
        {
            if (string.IsNullOrWhiteSpace(NewTempleName)) return;
            var temple = new Temple
            {
                Name     = NewTempleName.Trim(),
                Location = NewTempleLocation.Trim()
            };
            try
            {
                await templeRepo.SaveAsync(temple);
                Temples.Add(temple);
                logger.LogInformation("Temple created: {Name}", temple.Name);
                NewTempleName     = string.Empty;
                NewTempleLocation = string.Empty;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to save temple");
            }
        }

        private async Task DeleteTempleAsync(Temple temple)
        {
            if (temple == null) return;
            try
            {
                await templeRepo.DeleteAsync(temple.Id);
                Temples.Remove(temple);
                if (selectedTemple?.Id == temple.Id)
                    SelectedTemple = null;
                logger.LogInformation("Temple deleted: {Name}", temple.Name);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to delete temple");
            }
        }

        private void RefreshHeadingRows()
        {
            HeadingRows.Clear();
            if (selectedTemple == null) return;
            int total = 0;
            foreach (var kv in selectedTemple.HeadingBucketCounts)
                total += kv.Value;

            for (int bucket = 0; bucket < 36; bucket++)
            {
                selectedTemple.HeadingBucketCounts.TryGetValue(bucket, out int count);
                if (count == 0) continue;
                double pct = total > 0 ? (double)count / total * 100.0 : 0;
                HeadingRows.Add(new HeadingRow
                {
                    BearingLabel = $"{bucket * 10}°–{bucket * 10 + 9}°",
                    Count        = count,
                    Percentage   = pct,
                    Bar          = pct / 100.0
                });
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    // One row in the heading distribution table
    public class HeadingRow
    {
        public string BearingLabel { get; set; } = string.Empty;
        public int    Count        { get; set; }
        public double Percentage   { get; set; }
        public double Bar          { get; set; }   // 0.0–1.0 for ProgressBar
    }
}

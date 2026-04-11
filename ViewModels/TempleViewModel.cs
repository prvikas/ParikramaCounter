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
    public class TempleViewModel : INotifyPropertyChanged
    {
        private readonly ITempleRepository           templeRepo;
        private readonly IPradhakshinaSessionService session;
        private readonly ILogger<TempleViewModel>    logger;

        private Temple?  selectedTemple;
        private bool     isLoading;
        private string   newTempleName     = string.Empty;
        private string   newTempleLocation = string.Empty;

        public ObservableCollection<Temple>    Temples    { get; } = new ObservableCollection<Temple>();
        public ObservableCollection<HeadingRow> HeadingRows { get; } = new ObservableCollection<HeadingRow>();

        public Temple? SelectedTemple
        {
            get => selectedTemple;
            set
            {
                if (selectedTemple == value) return;
                selectedTemple = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(HasActiveTemple));
                OnPropertyChanged(nameof(ActiveTempleSummary));
                RefreshHeadingRows();   // repopulate rows when selection changes
                session.SetActiveTemple(value?.Id, value?.Name);
                logger.LogInformation("Active temple set: {Name}", value?.Name ?? "none");
            }
        }

        public bool IsLoading { get => isLoading; set { isLoading = value; OnPropertyChanged(); } }
        public bool HasActiveTemple  => selectedTemple != null;
        public bool HasNoHeadingData => selectedTemple == null || HeadingRows.Count == 0;

        public string NewTempleName
        {
            get => newTempleName;
            set
            {
                newTempleName = value;
                OnPropertyChanged();
                // Re-evaluate CanExecute so Add button enables/disables as user types
                ((Command)AddTempleCommand).ChangeCanExecute();
            }
        }

        public string NewTempleLocation
        {
            get => newTempleLocation;
            set { newTempleLocation = value; OnPropertyChanged(); }
        }

        public string ActiveTempleSummary => selectedTemple != null
            ? $"{selectedTemple.Name}" + (string.IsNullOrEmpty(selectedTemple.Location) ? "" : $" — {selectedTemple.Location}")
            : "No temple selected";

        public ICommand LoadCommand           { get; }
        public ICommand AddTempleCommand      { get; }
        public ICommand DeleteTempleCommand   { get; }
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
            AddTempleCommand      = new Command(async () => await AddTempleAsync(),
                                               () => !string.IsNullOrWhiteSpace(NewTempleName));
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
                foreach (var t in temples) Temples.Add(t);

                // Restore previously selected temple from session service
                string? savedId = session.ActiveTempleId;
                if (savedId != null)
                {
                    foreach (var t in Temples)
                    {
                        if (t.Id == savedId)
                        {
                            // Set backing field directly to avoid double-setting prefs
                            selectedTemple = t;
                            OnPropertyChanged(nameof(SelectedTemple));
                            OnPropertyChanged(nameof(HasActiveTemple));
                            OnPropertyChanged(nameof(ActiveTempleSummary));
                            RefreshHeadingRows();
                            break;
                        }
                    }
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
                logger.LogError(ex, "Failed to save temple {Name}", temple.Name);
            }
        }

        private async Task DeleteTempleAsync(Temple temple)
        {
            if (temple == null) return;
            try
            {
                await templeRepo.DeleteAsync(temple.Id);
                Temples.Remove(temple);
                if (selectedTemple?.Id == temple.Id) SelectedTemple = null;
                logger.LogInformation("Temple deleted: {Name}", temple.Name);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to delete temple {Name}", temple.Name);
            }
        }

        private void RefreshHeadingRows()
        {
            HeadingRows.Clear();
            OnPropertyChanged(nameof(HasNoHeadingData));  // reset before repopulating
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
            OnPropertyChanged(nameof(HasNoHeadingData));
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    public class HeadingRow
    {
        public string BearingLabel { get; set; } = string.Empty;
        public int    Count        { get; set; }
        public double Percentage   { get; set; }
        public double Bar          { get; set; }
    }
}

using ParikramaCounter.ViewModels;

namespace ParikramaCounter.Views
{
    public partial class DiagnosticsPage : ContentPage
    {
        private readonly DiagnosticsViewModel viewModel;

        public DiagnosticsPage(DiagnosticsViewModel vm)
        {
            InitializeComponent();
            BindingContext = viewModel = vm;
        }

        protected override void OnAppearing()   { base.OnAppearing();    viewModel.Activate(); }
        protected override void OnDisappearing(){ base.OnDisappearing(); viewModel.Deactivate(); }
    }
}

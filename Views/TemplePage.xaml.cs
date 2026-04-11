using ParikramaCounter.ViewModels;

namespace ParikramaCounter.Views
{
    public partial class TemplePage : ContentPage
    {
        private readonly TempleViewModel viewModel;

        public TemplePage(TempleViewModel vm)
        {
            InitializeComponent();
            BindingContext = viewModel = vm;
        }

        protected override void OnAppearing()
        {
            base.OnAppearing();
            // Reload temples each time the page appears so additions from
            // other sessions (future sync) are picked up immediately.
            _ = viewModel.LoadAsync();
        }
    }
}

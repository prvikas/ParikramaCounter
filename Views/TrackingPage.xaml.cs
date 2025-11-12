using ParikramaCounter.ViewModels;

namespace ParikramaCounter.Views
{
    public partial class TrackingPage : ContentPage
    {
        public TrackingPage(TrackingViewModel viewModel)
        {
            InitializeComponent();
            BindingContext = viewModel;
        }
    }
}

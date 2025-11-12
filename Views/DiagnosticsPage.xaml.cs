using ParikramaCounter.ViewModels;

namespace ParikramaCounter.Views
{
    public partial class DiagnosticsPage : ContentPage
    {
        public DiagnosticsPage(DiagnosticsViewModel viewModel)
        {
            InitializeComponent();
            BindingContext = viewModel;
        }
    }
}

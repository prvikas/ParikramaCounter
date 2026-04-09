using ParikramaCounter.ViewModels;

namespace ParikramaCounter.Views
{
    public partial class TrackingPage : ContentPage
    {
        private TrackingViewModel viewModel;

        public TrackingPage(TrackingViewModel vm)
        {
            InitializeComponent();
            BindingContext = viewModel = vm;
        }

        // Handle Set button for custom target: apply the value then clear the Entry
        // so the user gets clear feedback that the action was taken.
        private void OnSetCustomTargetClicked(object sender, EventArgs e)
        {
            viewModel.SetCustomTargetCommand.Execute(CustomTargetEntry.Text);
            CustomTargetEntry.Text = string.Empty;
            CustomTargetEntry.Unfocus(); // dismiss keyboard
        }
    }
}

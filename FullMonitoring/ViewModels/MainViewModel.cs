using CommunityToolkit.Mvvm.ComponentModel;

namespace FullMonitoring.ViewModels;

public partial class MainViewModel : ViewModelBase
{
    [ObservableProperty]
    private string _greeting = "Ласкаво просимо до FullMonitoring!";
}
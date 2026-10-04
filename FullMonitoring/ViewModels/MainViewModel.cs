using CommunityToolkit.Mvvm.ComponentModel;

namespace FullMonitoring.ViewModels;

public partial class MainViewModel : ViewModelBase
{
    [ObservableProperty] public partial string Greeting { get; set; } = "Ласкаво просимо до FullMonitoring!";
}
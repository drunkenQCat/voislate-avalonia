using CommunityToolkit.Mvvm.ComponentModel;

namespace VoiSlate.ViewModels;

/// <summary>
/// M0 占位 VM（Agent B 将接管全部 VM；本类随后删除）。
/// </summary>
public partial class PlaceholderViewModel : ObservableObject
{
    [ObservableProperty]
    private string _placeholderText = "M0 skeleton compiles — P0.5 vertical slice next.";
}
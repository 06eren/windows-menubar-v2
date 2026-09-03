using CommunityToolkit.Mvvm.ComponentModel;
using Wpf.Ui.Common;

namespace Windows_10_MenuBar.Models;

public partial class BatteryInfo : ObservableObject
{
    [ObservableProperty] private bool _hasBattery;
    [ObservableProperty] private int _level = 100;
    [ObservableProperty] private bool _isCharging;
    [ObservableProperty] private SymbolRegular _icon = SymbolRegular.Battery1024;
}

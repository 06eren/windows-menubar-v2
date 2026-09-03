using CommunityToolkit.Mvvm.ComponentModel;
using Wpf.Ui.Common;

namespace Windows_10_MenuBar.Models;

public partial class WifiInfo : ObservableObject
{
    [ObservableProperty] private string _ssid = string.Empty;
    [ObservableProperty] private byte _signalStrength;
    [ObservableProperty] private bool _isConnected;
    [ObservableProperty] private string _statusText = string.Empty;
    [ObservableProperty] private bool _isPasswordPromptVisible;

    public SymbolRegular SignalIcon =>
        SignalStrength >= 4 ? SymbolRegular.Wifi424
        : SignalStrength == 3 ? SymbolRegular.Wifi324
        : SignalStrength == 2 ? SymbolRegular.Wifi224
        : SymbolRegular.Wifi124;

    partial void OnSignalStrengthChanged(byte value) => OnPropertyChanged(nameof(SignalIcon));
}

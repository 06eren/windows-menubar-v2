using CommunityToolkit.Mvvm.ComponentModel;
using Windows.Devices.Enumeration;

namespace Windows_10_MenuBar.Models;

public partial class BluetoothDeviceInfo : ObservableObject
{
    [ObservableProperty] private string _name = string.Empty;
    [ObservableProperty] private string _id = string.Empty;
    [ObservableProperty] private bool _isConnected;
    [ObservableProperty] private string _statusText = string.Empty;

    public DeviceInformation? DeviceInfo { get; set; }
}

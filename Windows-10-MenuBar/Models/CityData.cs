using System.Collections.Generic;

namespace Windows_10_MenuBar.Models;

public class CityData
{
    public string name { get; set; } = string.Empty;
    public string plate { get; set; } = string.Empty;
    public string latitude { get; set; } = string.Empty;
    public string longitude { get; set; } = string.Empty;
    public List<string> counties { get; set; } = new();
}

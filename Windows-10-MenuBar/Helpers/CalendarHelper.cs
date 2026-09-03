using System.Collections.Generic;

namespace Windows_10_MenuBar.Helpers;

public static class CalendarHelper
{
    public static readonly Dictionary<(int month, int day), string> TurkishHolidays = new()
    {
        { (1,  1),  "Yılbaşı"           },
        { (4,  23), "23 Nisan"          },
        { (5,  1),  "İşçi Bayramı"      },
        { (5,  19), "19 Mayıs"          },
        { (7,  15), "15 Temmuz"         },
        { (8,  30), "30 Ağustos"        },
        { (10, 29), "Cumhuriyet Bayramı" },
    };
}

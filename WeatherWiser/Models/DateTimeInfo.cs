using System;
using System.Globalization;

namespace WeatherWiser.Models
{
    public class DateTimeInfo
    {
        public DateTime Now { get; set; }
        public string Date => Now.ToString("yyyy-MM-dd");
        public string Time => Now.ToString("H:mm:ss");
        public string ShortDayOfWeek => Now.ToString("ddd", CultureInfo.InvariantCulture);

        public DateTimeInfo()
        {
            Now = DateTime.Now;
        }
    }
}

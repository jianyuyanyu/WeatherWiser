using System;

namespace WeatherWiser.Models
{
    public class WeatherInfo
    {
        readonly string[] directions = { "北", "北北東", "北東", "東北東", "東", "東南東", "南東", "南南東", "南", "南南西", "南西", "西南西", "西", "西北西", "北西", "北北西" };
        public string Main { get; set; }
        public string Description { get; set; }
        public int Temperature { get; set; }
        public int FeelsLike { get; set; }
        public int Humidity { get; set; }
        public string City { get; set; }
        public double PrecipitationProbability { get; set; }
        public double WindSpeed { get; set; }
        public int Pressure { get; set; }
        public string IconId { get; set; }
        public int WindDirection { get; set; }
        public string CardinalDirection
        {
            get
            {
                int index = (int)Math.Round((double)WindDirection % 360 / 22.5);
                return directions[index];
            }
        }
    }
}

using Newtonsoft.Json.Linq;
using System;
using System.Net.Http;
using System.Threading.Tasks;
using WeatherWiser.Models;

namespace WeatherWiser.Services
{
    public class WeatherService
    {
        private readonly string apiKey;

        public WeatherService()
        {
            // APIキーを環境変数から取得
            apiKey = Environment.GetEnvironmentVariable("WEATHER_API_KEY");
            if (string.IsNullOrEmpty(apiKey))
            {
                throw new InvalidOperationException("API key is not configured.");
            }
        }

        public async Task<WeatherInfo> GetWeatherAsync(string city)
        {
            // OpenWeather の API を呼び出して JSON 形式の天気情報を取得
            using HttpClient client = new();
            string url = $"https://api.openweathermap.org/data/2.5/weather?q={city}&appid={apiKey}&units=metric&lang=en";
            HttpResponseMessage response = await client.GetAsync(url);
            response.EnsureSuccessStatusCode();
            string responseBody = await response.Content.ReadAsStringAsync();
            JObject weatherData = JObject.Parse(responseBody);

            // 降雨量と降雪量をどちらも降雨量として簡略化
            double precipitationProbability = 0;
            if (weatherData["rain"] != null && weatherData["rain"]["1h"] != null)
            {
                precipitationProbability = (double)weatherData["rain"]["1h"];
            }
            else if (weatherData["snow"] != null && weatherData["snow"]["1h"] != null)
            {
                precipitationProbability = (double)weatherData["snow"]["1h"];
            }

            // WeatherInfo クラスに天気情報を格納
            return new WeatherInfo
            {
                Main = weatherData["weather"][0]["main"].ToString(),
                Description = weatherData["weather"][0]["description"].ToString(),
                Temperature = (int)Math.Round((double)weatherData["main"]["temp"]),
                FeelsLike = (int)Math.Round((double)weatherData["main"]["feels_like"]),
                Humidity = (int)weatherData["main"]["humidity"],
                City = weatherData["name"].ToString(),
                PrecipitationProbability = precipitationProbability,
                WindSpeed = (double)weatherData["wind"]["speed"],
                Pressure = (int)weatherData["main"]["pressure"],
                IconId = weatherData["weather"][0]["icon"].ToString(),
                WindDirection = (int)weatherData["wind"]["deg"]
            };
        }
    }
}

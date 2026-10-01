using System.ComponentModel;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace SampleMcpServer.Tools;

public enum TemperatureUnit { Celsius, Fahrenheit }

[McpServerToolType]
public sealed class DemoTools
{
    [McpServerTool(Name = "echo", ReadOnly = true, Idempotent = true)]
    [Description("Echoes the message back to the client.")]
    public static string Echo([Description("Message to echo")] string message) => $"Echo: {message}";

    [McpServerTool(Name = "add", ReadOnly = true, Idempotent = true)]
    [Description("Adds two numbers.")]
    public static double Add(
        [Description("First number")] double a,
        [Description("Second number")] double b) => a + b;

    [McpServerTool(Name = "get_weather", ReadOnly = true, OpenWorld = true)]
    [Description("Returns a (fake) weather forecast for a city.")]
    public static WeatherForecast GetWeather(
        [Description("City name, e.g. Istanbul")] string city,
        [Description("Temperature unit")] TemperatureUnit unit = TemperatureUnit.Celsius,
        [Description("Number of days to forecast (1-7)")] int days = 3)
    {
        var rnd = new Random(city.GetHashCode());
        var list = Enumerable.Range(0, Math.Clamp(days, 1, 7)).Select(i =>
        {
            var c = rnd.Next(-5, 35);
            return new DailyForecast(DateOnly.FromDateTime(DateTime.Today.AddDays(i)),
                unit == TemperatureUnit.Celsius ? c : c * 9 / 5 + 32,
                new[] { "Sunny", "Cloudy", "Rainy", "Windy" }[rnd.Next(4)]);
        }).ToList();
        return new WeatherForecast(city, unit.ToString(), list);
    }

    [McpServerTool(Name = "generate_image")]
    [Description("Returns a tiny PNG image together with a text block, to show image results.")]
    public static IEnumerable<ContentBlock> GenerateImage([Description("Color as hex, e.g. #4f46e5")] string color = "#4f46e5")
    {
        // 1x1 PNG pixel, enough to demonstrate image content blocks.
        var png = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNk+M9QDwADhgGAWjR9awAAAABJRU5ErkJggg==");
        return [
            new TextContentBlock { Text = $"Generated image for {color}" },
            ImageContentBlock.FromBytes(png, "image/png"),
        ];
    }

    [McpServerTool(Name = "fail")]
    [Description("Always throws, to show how tool errors look in the UI.")]
    public static string Fail([Description("Error message")] string reason = "Something went wrong")
        => throw new McpException(reason);
}

public record DailyForecast(DateOnly Date, int Temperature, string Summary);
public record WeatherForecast(string City, string Unit, List<DailyForecast> Days);

using System.Diagnostics;

namespace Browser.Management;

public static class Timer
{
    public static long startTime;

    public static void start()
    {
        startTime = Stopwatch.GetTimestamp();
    }

    public static void end()
    {
        long endTime = Stopwatch.GetTimestamp();

        // Вычисляем разницу
        long elapsedTime = endTime - startTime;
        double elapsedSeconds = (double)elapsedTime / Stopwatch.Frequency;

        // Выводим результат
        Console.WriteLine($"Время выполнения: {elapsedSeconds * 1000} мс");
    }
}
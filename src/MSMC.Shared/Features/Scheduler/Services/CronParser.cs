// -----------------------------------------------------------------------------
// 文件名: CronParser.cs
// 命名空间: io.NET.ZTR_OS.Features.Scheduler.Services
// 功能描述: Cron 表达式解析器 —— 支持 5 字段标准格式
// 设计模式: 三链原则 - 因果链：Cron 表达式 → 下次运行时间；执行链：输入校验
// -----------------------------------------------------------------------------

using System.Globalization;

namespace io.NET.ZTR_OS.Features.Scheduler.Services;

/// <summary>
/// Cron 表达式解析器
/// 支持格式: 分钟 小时 日 月 星期
/// 通配符: *, 数字, 范围(1-5), 列表(1,3,5), 步长(*/5, 1-10/2)
/// </summary>
public static class CronParser
{
    private static readonly char[] Separators = { ' ', '\t' };

    /// <summary>
    /// 计算 Cron 表达式的下次运行时间
    /// </summary>
    public static DateTimeOffset? GetNextRunTime(string cronExpression, DateTimeOffset fromTime)
    {
        if (string.IsNullOrWhiteSpace(cronExpression))
            return null;

        var parts = cronExpression.Split(Separators, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 5)
            return null;

        try
        {
            int[] minutes = ParseField(parts[0], 0, 59);
            int[] hours = ParseField(parts[1], 0, 23);
            int[] days = ParseField(parts[2], 1, 31);
            int[] months = ParseField(parts[3], 1, 12);
            int[] weekdays = ParseWeekdayField(parts[4]);

            if (minutes.Length == 0 || hours.Length == 0 || days.Length == 0 ||
                months.Length == 0 || weekdays.Length == 0)
                return null;

            var cursor = fromTime.AddMinutes(1).AddSeconds(-fromTime.Second);
            var endLimit = fromTime.AddYears(1);

            while (cursor < endLimit)
            {
                bool monthOk = months.Contains(cursor.Month);
                bool dayOk = days.Contains(cursor.Day);
                bool weekdayOk = weekdays.Contains((int)cursor.DayOfWeek);
                bool hourOk = hours.Contains(cursor.Hour);
                bool minuteOk = minutes.Contains(cursor.Minute);

                if (monthOk && dayOk && weekdayOk && hourOk && minuteOk)
                    return cursor;

                cursor = cursor.AddMinutes(1);
            }

            return null;
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <summary>
    /// 解析单个字段
    /// </summary>
    private static int[] ParseField(string field, int min, int max)
    {
        var result = new HashSet<int>();
        var segments = field.Split(',', StringSplitOptions.RemoveEmptyEntries);

        foreach (var segment in segments)
        {
            if (segment == "*" || segment == "?")
            {
                for (int i = min; i <= max; i++)
                    result.Add(i);
                continue;
            }

            if (segment.StartsWith("*/"))
            {
                int step = int.Parse(segment.AsSpan(2), CultureInfo.InvariantCulture);
                if (step <= 0) step = 1;
                for (int i = min; i <= max; i += step)
                    result.Add(i);
                continue;
            }

            if (segment.Contains('-'))
            {
                var rangeParts = segment.Split('-');
                int from = int.Parse(rangeParts[0], CultureInfo.InvariantCulture);
                int to;
                int step = 1;

                var tail = rangeParts[1];
                if (tail.Contains('/'))
                {
                    var stepParts = tail.Split('/');
                    to = int.Parse(stepParts[0], CultureInfo.InvariantCulture);
                    step = int.Parse(stepParts[1], CultureInfo.InvariantCulture);
                }
                else
                {
                    to = int.Parse(tail, CultureInfo.InvariantCulture);
                }

                from = Math.Clamp(from, min, max);
                to = Math.Clamp(to, min, max);
                if (from <= to)
                {
                    for (int i = from; i <= to; i += step)
                        result.Add(i);
                }
                continue;
            }

            int value = int.Parse(segment, CultureInfo.InvariantCulture);
            if (value >= min && value <= max)
                result.Add(value);
        }

        return result.OrderBy(x => x).ToArray();
    }

    /// <summary>
    /// 解析星期字段（支持 SUN-SAT 缩写或数字）
    /// </summary>
    private static int[] ParseWeekdayField(string field)
    {
        var normalized = field.Trim().ToUpperInvariant() switch
        {
            _ => field
        };

        normalized = normalized
            .Replace("SUN", "0").Replace("MON", "1").Replace("TUE", "2")
            .Replace("WED", "3").Replace("THU", "4").Replace("FRI", "5")
            .Replace("SAT", "6");

        return ParseField(normalized, 0, 6);
    }

    /// <summary>
    /// 验证 Cron 表达式是否合法
    /// </summary>
    public static bool IsValid(string cronExpression)
    {
        if (string.IsNullOrWhiteSpace(cronExpression))
            return false;

        var parts = cronExpression.Split(Separators, StringSplitOptions.RemoveEmptyEntries);
        return parts.Length == 5 && GetNextRunTime(cronExpression, DateTimeOffset.UtcNow) != null;
    }
}

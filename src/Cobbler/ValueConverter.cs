using System.Globalization;

namespace Cobbler;

public static class ValueConverter
{
    public static T Convert<T>(string raw, string subject)
    {
        return (T)Convert(typeof(T), raw, subject)!;
    }

    public static object? Convert(Type type, string raw, string subject)
    {
        var target = Nullable.GetUnderlyingType(type) ?? type;
        try
        {
            if (target == typeof(string))
            {
                return raw;
            }

            if (target == typeof(bool))
            {
                return ParseBool(raw);
            }

            if (target.IsEnum)
            {
                return ParseEnum(target, raw);
            }

            if (target == typeof(Guid))
            {
                return Guid.Parse(raw);
            }

            if (target == typeof(TimeSpan))
            {
                return TimeSpan.Parse(raw, CultureInfo.InvariantCulture);
            }

            if (target == typeof(DateTime))
            {
                return DateTime.Parse(raw, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
            }

            if (target == typeof(DateOnly))
            {
                return DateOnly.Parse(raw, CultureInfo.InvariantCulture);
            }

            if (target == typeof(Uri))
            {
                return new Uri(raw, UriKind.RelativeOrAbsolute);
            }

            if (target == typeof(FileInfo))
            {
                return new FileInfo(raw);
            }

            if (target == typeof(DirectoryInfo))
            {
                return new DirectoryInfo(raw);
            }

            return System.Convert.ChangeType(raw, target, CultureInfo.InvariantCulture);
        }
        catch (Exception e) when (e is FormatException or OverflowException or ArgumentException or InvalidCastException or UriFormatException)
        {
            throw new ParseException(
                ParseErrorKind.InvalidValue,
                $"invalid value '{raw}' for {subject}: expected {Describe(target)}",
                subject);
        }
    }

    private static bool ParseBool(string raw)
    {
        switch (raw.ToLowerInvariant())
        {
            case "true":
            case "yes":
            case "on":
            case "1":
                return true;
            case "false":
            case "no":
            case "off":
            case "0":
                return false;
            default:
                throw new FormatException("not a boolean");
        }
    }

    private static object ParseEnum(Type enumType, string raw)
    {
        var value = Enum.Parse(enumType, raw, true);
        if (!Enum.IsDefined(enumType, value))
        {
            throw new ArgumentException("undefined enum value");
        }

        return value;
    }

    private static string Describe(Type target)
    {
        if (target.IsEnum)
        {
            return "one of " + string.Join(", ", Enum.GetNames(target).Select(n => n.ToLowerInvariant()));
        }

        if (target == typeof(bool))
        {
            return "true or false";
        }

        if (target == typeof(int) || target == typeof(long) || target == typeof(short) || target == typeof(byte)
            || target == typeof(uint) || target == typeof(ulong) || target == typeof(ushort) || target == typeof(sbyte))
        {
            return "an integer";
        }

        if (target == typeof(double) || target == typeof(float) || target == typeof(decimal))
        {
            return "a number";
        }

        return target.Name;
    }
}

using Humanizer;

namespace AonGenerator
{
    public static class ExtensionMethods
    {
        public static IEnumerable<T> ForEach<T>(this IEnumerable<T> enumerable, Action<T> action)
        {
            foreach (var item in enumerable)
            {
                action(item);
            }

            return enumerable;
        }

        public static string RemoveNonAlphaNumeric(this string str)
        {
            return new string(str.Where(c => char.IsLetterOrDigit(c) || c == '-').ToArray());
        }

        public static string Join(this IEnumerable<string?> strings, char separator)
        {
            return string.Join(separator, strings);
        }

        public static string Join(this IEnumerable<string?> strings, string separator)
        {
            return string.Join(separator, strings);
        }

        public static string UrlFormat(this string str)
        {
            return str
                .Underscore()
                .Dasherize()
                .Replace("&", "and")
                .RemoveNonAlphaNumeric()
                .Split('-', StringSplitOptions.RemoveEmptyEntries)
                .Join('-');
        }

        public static string WithSign(this int value)
        {
            return (value < 0 ? "" : "+") + value.ToString();
        }

        public static string WrapWithParentheses(this string? value)
        {
            return value == null || value == "" ? "" : $"({value})";
        }
    }
}

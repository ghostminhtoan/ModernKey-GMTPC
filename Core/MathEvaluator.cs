using System;
using System.Globalization;
using System.Text.RegularExpressions;

namespace ModernKey.Core
{
    /// <summary>
    /// Bộ phân tích và tính toán biểu thức toán học inline an toàn, không phụ thuộc thư viện ngoài.
    /// Hỗ trợ: +, -, *, /, ^, %, dấu ngoặc () và số thập phân.
    /// </summary>
    public static class MathEvaluator
    {
        private static readonly Regex MathPattern = new Regex(
            @"(?:^|[\s\(\[\{;:,])([0-9\.\,\+\-\*\/\^\%\(\)\s]+)=\s*$",
            RegexOptions.Compiled);

        public static bool IsPotentialMathExpression(string input, out string cleanExpression)
        {
            cleanExpression = null;
            if (string.IsNullOrEmpty(input)) return false;

            var match = MathPattern.Match(input);
            if (!match.Success) return false;

            string expr = match.Groups[1].Value.Trim();
            // Phải chứa ít nhất 1 toán tử và 1 chữ số
            if (!Regex.IsMatch(expr, @"[\+\-\*\/\^\%]") || !Regex.IsMatch(expr, @"\d"))
                return false;

            cleanExpression = expr;
            return true;
        }

        public static bool TryEvaluate(string expression, out double result, out string formatted)
        {
            result = 0;
            formatted = string.Empty;

            if (string.IsNullOrWhiteSpace(expression))
                return false;

            try
            {
                // Chuẩn hóa dấu phẩy thập phân nếu nằm giữa hai chữ số
                string normalized = expression.Replace(" ", "");
                // Thay thế dấu phẩy giữa 2 số thành dấu chấm nếu không có dấu chấm
                normalized = Regex.Replace(normalized, @"(?<=\d),(?=\d)", ".");

                int pos = 0;
                result = ParseExpression(normalized, ref pos);

                if (pos < normalized.Length)
                    return false; // Còn ký tự chưa phân tích hết

                if (double.IsNaN(result) || double.IsInfinity(result))
                    return false;

                // Định dạng kết quả gọn gàng
                if (Math.Abs(result - Math.Round(result)) < 1e-9)
                {
                    formatted = ((long)Math.Round(result)).ToString(CultureInfo.InvariantCulture);
                }
                else
                {
                    formatted = Math.Round(result, 6).ToString("0.######", CultureInfo.InvariantCulture);
                }

                return true;
            }
            catch
            {
                return false;
            }
        }

        private static double ParseExpression(string s, ref int pos)
        {
            double value = ParseTerm(s, ref pos);
            while (pos < s.Length)
            {
                char op = s[pos];
                if (op != '+' && op != '-') break;
                pos++;
                double nextTerm = ParseTerm(s, ref pos);
                if (op == '+') value += nextTerm;
                else value -= nextTerm;
            }
            return value;
        }

        private static double ParseTerm(string s, ref int pos)
        {
            double value = ParseFactor(s, ref pos);
            while (pos < s.Length)
            {
                char op = s[pos];
                if (op != '*' && op != '/' && op != '%') break;
                pos++;
                double nextFactor = ParseFactor(s, ref pos);
                if (op == '*')
                {
                    value *= nextFactor;
                }
                else if (op == '/')
                {
                    if (Math.Abs(nextFactor) < 1e-12) throw new DivideByZeroException();
                    value /= nextFactor;
                }
                else if (op == '%')
                {
                    if (Math.Abs(nextFactor) < 1e-12) throw new DivideByZeroException();
                    value %= nextFactor;
                }
            }
            return value;
        }

        private static double ParseFactor(string s, ref int pos)
        {
            double value = ParsePrimary(s, ref pos);
            if (pos < s.Length && s[pos] == '^')
            {
                pos++;
                double exponent = ParseFactor(s, ref pos);
                value = Math.Pow(value, exponent);
            }
            return value;
        }

        private static double ParsePrimary(string s, ref int pos)
        {
            if (pos >= s.Length) throw new FormatException();

            // Unary + / -
            if (s[pos] == '+')
            {
                pos++;
                return ParsePrimary(s, ref pos);
            }
            if (s[pos] == '-')
            {
                pos++;
                return -ParsePrimary(s, ref pos);
            }

            // Dấu ngoặc ( ... )
            if (s[pos] == '(')
            {
                pos++;
                double value = ParseExpression(s, ref pos);
                if (pos >= s.Length || s[pos] != ')') throw new FormatException("Missing closing parenthesis");
                pos++;
                return value;
            }

            // Số
            int start = pos;
            while (pos < s.Length && (char.IsDigit(s[pos]) || s[pos] == '.'))
            {
                pos++;
            }

            if (start == pos) throw new FormatException("Expected number");

            string numStr = s.Substring(start, pos - start);
            if (double.TryParse(numStr, NumberStyles.Float, CultureInfo.InvariantCulture, out double num))
            {
                return num;
            }

            throw new FormatException("Invalid number: " + numStr);
        }
    }
}

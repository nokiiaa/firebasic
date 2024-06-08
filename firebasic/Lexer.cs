using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;

namespace firebasic
{
    public enum TokenType
    {
        Punctuator,
        Keyword,
        Integer,
        Name,
        Float,
        String,
        WString,
        Newline
    }

    public class Token
    {
        public int Line { get; set; }
        public int Column { get; set; }
        public TokenType Type { get; set; }
        public object Value { get; set; }
        public static Token Punct(string str, int line = 0, int col = 0) 
            => new Token { Line = line, Column = col, Type = TokenType.Punctuator, Value = str };
        public static Token Keyword(string str, int line = 0, int col = 0) 
            => new Token { Line = line, Column = col, Type = TokenType.Keyword, Value = str };
        public static Token Double(double lit, int line = 0, int col = 0) 
            => new Token { Line = line, Column = col, Type = TokenType.Float, Value = lit };
        public static Token Float(float lit, int line = 0, int col = 0)
           => new Token { Line = line, Column = col, Type = TokenType.Float, Value = lit };
        public static Token Integer(ulong lit, int line = 0, int col = 0) 
            => new Token { Line = line, Column = col, Type = TokenType.Integer, Value = lit };
        public static Token Name(string name, int line = 0, int col = 0) 
            => new Token { Line = line, Column = col, Type = TokenType.Name, Value = name };
        public static Token String(string lit, int line = 0, int col = 0) 
            => new Token { Line = line, Column = col, Type = TokenType.String, Value = lit };
        public static Token WString(string lit, int line = 0, int col = 0)
           => new Token { Line = line, Column = col, Type = TokenType.WString, Value = lit };
        public static Token Newline(int line = 0, int col = 0)
           => new Token { Line = line, Column = col, Type = TokenType.Newline };
        public static implicit operator bool(Token token) 
            => token != null;

        public override string ToString()
            => $"{Line}:{Column}: {Type} {Value}";
    }
    public static class Lexer
    {
        public static string[]
            // Must be sorted by descending length.
            Punctuators =
            {
                ">>>=", "<<<=", ">>=", "<<=",
                ">>>", "<<<", "<>", "::",
                "+=", "-=", "*=", "/=",
                "\\=", "&=", "^=", ">>",
                "<<", ">=", "<=", "(",
                ")", ",", "=", ".",
                "<", ">", "^", "+", "-",
                "*", "/", "\\", "&",
                "{", "}"
            },
            Keywords =
            {
                "sub", "function", "static", "return", "namespace", "use", "lib", "end", "dim",
                "const", "to", "as", "me", "if", "else", "elseif", "cchr", "cwchr",
                "cbyte", "cshort", "cushort", "cint", "cuint", "clng", "culng", "cbool", "cstr",
                "cwstr", "for", "do", "loop", "next", "while", "then", "ctype", "void",
                "select", "case", "and", "or", "xor", "exit", "sizeof", "size", "not", "mod",
                "integer", "string", "boolean", "float", "double", "true", "false", "byte",
                "char", "until",  "step", "ushort", "short", "uinteger",  "ulong", "long",
                "structure", "declare", "enum", "wstring", "wchar", "continue", "private"
            };

        public static string
            PunctFirstChars;

        static Lexer()
        {
            CultureInfo customCulture = (CultureInfo)Thread.CurrentThread.CurrentCulture.Clone();
            customCulture.NumberFormat.NumberDecimalSeparator = ".";
            Thread.CurrentThread.CurrentCulture = customCulture;
            var sb = new StringBuilder();
            foreach (string p in Punctuators)
                sb.Append(p[0]);
            PunctFirstChars = sb.ToString();
        }

        public static List<Token> Scan(string file, string data = null)
        {
            List<Token> output = new List<Token>();
            data ??= File.ReadAllText(file);
            int line = 1, column = 1, ptr = 0;

            bool end() => ptr >= data.Length || ptr < 0;
            char current() => end() ? '\0' : data[ptr];
            char adv()
            {
                char c = current();
                ptr++;
                if (c == '\n') { line++; column = 1; }
                else column++;
                return c;
            }
            bool check_char(char c) => current() == c;
            bool check(string any) => any.Contains(current());
            bool check_pred(Predicate<char> pred) => pred(current());
            bool match_char(char c)
            {
                char ch = current();
                if (ch == c) { adv(); return true; }
                return false;
            }
            bool match_str(string str)
            {
                if (str.Length + ptr > data.Length)
                    return false;
                if (data.Substring(ptr, str.Length) == str)
                {
                    for (int i = 0; str.Length > i; i++) adv();
                    return true;
                }
                return false;
            }
            string seq(Predicate<char> pred)
            {
                var matched = new StringBuilder();
                int l = line, c = column, p = ptr;
                for (;; adv())
                {
                    char ch = current();
                    if (!pred(ch))
                    {
                        if (matched.Length == 0) return null;
                        else return matched.ToString();
                    }
                    else
                        matched.Append(ch);
                }
            }

            while (!end())
            {
                while (match_char(' ') || match_char('\t') || match_char('\r')
                    || match_char('\v') || match_char('\f'));

                if (match_str("REM ") || match_char('\''))
                {
                    while (!end() && !check_char('\n')) adv();
                    continue;
                }
                int ls = line, cs = column;
                string sequence = "";
                bool wideStr = false;

                if (match_char('\n'))
                {
                    if (output.Count > 0 &&
                        output.Last().Type == TokenType.Name &&
                        output.Last().Value as string == "_")
                        output.RemoveAt(output.Count - 1);
                    else
                        output.Add(Token.Newline(ls, cs));
                }
                else if (match_str("0x") || match_str("0X"))
                {
                    sequence = seq(x => x >= '0' && x <= '9' ||
                        char.ToLower(x) >= 'a' && char.ToLower(x) <= 'f');
                    if (!ulong.TryParse(sequence, NumberStyles.HexNumber,
                        CultureInfo.InvariantCulture, out ulong result))
                        Output.Error(file, ls, cs, "Integer literal exceeds 64-bit boundary");
                    else
                        output.Add(Token.Integer(result, ls, cs));
                }
                else if (check(PunctFirstChars))
                {
                    foreach (string p in Punctuators)
                    {
                        if (match_str(p))
                        {
                            output.Add(Token.Punct(p, ls, cs));
                            break;
                        }
                    }
                }
                else if ((match_str("L\"") && (wideStr = true)) || match_char('"'))
                {
                    int start = ptr;
                    while (!match_char('"'))
                    {
                        if (end() || match_char('\n'))
                        {
                            Output.Error(file, line, column, "Unterminated string literal");
                            break;
                        }
                        adv();
                    }
                    string str = data.Substring(start, ptr - start - 1);
                    if (!wideStr) output.Add(Token.String(str, ls, cs));
                    else output.Add(Token.WString(str, ls, cs));
                }
                else if (check_pred(x => char.ToLower(x) >= 'a'
                    && char.ToLower(x) <= 'z' || x == '_' || x == '$'))
                {
                    sequence = seq(x => char.ToLower(x) >= 'a' && char.ToLower(x) <= 'z' ||
                        x >= '0' && x <= '9' || x == '_' || x == '$');

                    if (sequence != null)
                        if (!Keywords.Contains(sequence.ToLower()))
                            output.Add(Token.Name(sequence, ls, cs));
                        else
                            output.Add(Token.Keyword(sequence.ToLower(), ls ,cs));
                }
                else if ((sequence = seq(x => x >= '0' && x <= '9')) != null)
                {
                    string seq1 = sequence;
                    if (match_char('.'))
                    {
                        string seq2 = seq(x => x >= '0' && x <= '9');
                        bool isFloat = match_char('f') || match_char('F');
                        if (double.TryParse(seq1 + "." + seq2, out double result))
                        {
                            if (isFloat)
                                output.Add(Token.Float((float)result, ls, cs));
                            else
                                output.Add(Token.Double(result, ls, cs));
                        }
                    }
                    else
                    {
                        if (!ulong.TryParse(seq1, out ulong result))
                            Output.Error(file, ls, cs, "Integer literal exceeds 64-bit boundary");
                        else
                            output.Add(Token.Integer(result, ls, cs));
                    }
                }
                else
                    Output.Error(file, ls, cs, $"Stray character '{adv()}'");
            }

            return output;
        }
    }
}
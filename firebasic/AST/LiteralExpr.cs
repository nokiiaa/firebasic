namespace firebasic.AST
{
    public class LiteralExpr : Expr
    {
        public enum Enum
        {
            Char,
            Byte,
            Short,
            UShort,
            Integer,
            UInteger,
            Long,
            ULong,
            Float,
            Double,
            String,
            WString,
            Bool,
            WChar
        }

        public Enum Type { get; set; }
        public object Value { get; set; }

        public LiteralExpr(Enum type, object value, int line = 0, int col = 0)
            : base(line, col)
        {
            Type = type;
            Value = value;
        }
    }
}

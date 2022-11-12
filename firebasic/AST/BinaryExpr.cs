namespace firebasic.AST
{
    public class BinaryExpr : Expr
    {
        public static readonly string[] OpStrings =
        {
            "&", "+", "-", "*", "/", "Mod", "Xor", "And",
            "Or", "==", "!=", "<", ">", "<=", ">=", "<<",
            ">>", "<<<", ">>>"
        };

        public enum Operations
        {
            Concat,
            Add,
            Sub,
            Mul,
            Div,
            Mod,
            Xor,
            And,
            Or,
            Eq,
            Neq,
            Lt,
            Gt,
            Le,
            Ge,
            Shl,
            Shr,
            Rol,
            Ror
        }

        public Expr Left { get; set; }

        public Expr Right { get; set; }

        public Operations Op { get; set; }

        public BinaryExpr(Expr left, Operations op, Expr right,
            int line = 0, int col = 0)
            : base(line, col)
        {
            Left = left;
            Op = op;
            Right = right;
        }
    }
}

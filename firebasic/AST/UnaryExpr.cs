namespace firebasic.AST
{
    public class UnaryExpr : Expr
    {
        public enum Operations
        {
            Not,
            Minus,
            Plus,
            GetAddr,
            Deref,
            SizeOf
        }

        public Expr Expr { get; set; }
        public Operations Op { get; set; }

        public UnaryExpr(Operations op, Expr expr,
            int line = 0, int col = 0)
            : base(line, col)
        {
            Op = op;
            Expr = expr;
        }
    }
}

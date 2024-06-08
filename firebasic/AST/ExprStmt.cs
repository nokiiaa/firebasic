namespace firebasic.AST
{
    public class ExprStmt : Stmt
    {
        public Expr Expr { get; set; }

        public ExprStmt(Expr expr, int line = 0, int col = 0)
            : base(line, col) => Expr = expr;
    }
}

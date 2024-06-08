namespace firebasic.AST
{
    public class ReturnStmt : Stmt
    {
        public Expr Value { get; set; }

        public ReturnStmt(Expr value, int line = 0, int col = 0)
            : base(line, col) => Value = value;
    }
}

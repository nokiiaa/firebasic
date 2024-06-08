namespace firebasic.AST
{
    // Only used in Select...Case statements.
    public class RangeExpr : Expr
    {
        public Expr Start { get; set; }

        public Expr Stop  { get; set; }

        public RangeExpr(Expr start, Expr stop,
            int line = 0, int col = 0)
            : base(line, col)
        {
            Start = start;
            Stop = stop;
        }
    }
}

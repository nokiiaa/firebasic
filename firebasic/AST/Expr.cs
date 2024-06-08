namespace firebasic.AST
{
    public class Expr
    {
        public int Line { get; set; }

        public int Column { get; set; }

        public Expr(int line = 0, int col = 0) { Line = line; Column = col; }

        public FBType DeducedTypeCache { get; set; }
    }
}

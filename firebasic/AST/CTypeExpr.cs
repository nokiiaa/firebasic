namespace firebasic.AST
{
    public class CTypeExpr : Expr
    {
        public Expr Casted { get; set; }

        public FBType ToType { get; set; }

        public CTypeExpr(Expr casted, FBType toType,
            int line = 0, int col = 0)
            : base(line, col)
        {
            Casted = casted;
            ToType = toType;
        }
    }
}

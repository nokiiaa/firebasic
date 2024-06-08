namespace firebasic.AST
{
    public class AccessExpr : Expr
    {
        public Expr Accessed { get; set; }

        public string Member { get; set; }

        public AccessExpr(Expr accessed, string member, int line = 0, int col = 0)
            : base(line, col)
        {
            Accessed = accessed;
            Member = member;
        }
    }
}

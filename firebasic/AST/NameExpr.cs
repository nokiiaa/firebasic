namespace firebasic.AST
{
    public class NameExpr : Expr
    {
        public NameExpr(Name name, int line = 0, int col = 0)
            : base(line, col) => Name = name;

        public Name Name { get; set; }

        public override string ToString() => Name;
    }
}

using firebasic.AST;

namespace firebasic
{
    public class ExpressionAlias
    {
        public ExpressionAlias(string name, Expr expr)
        {
            Name = name;
            Expr = expr;
        }

        public string Name { get; set; }
        public Expr Expr { get; set; }
    }
}

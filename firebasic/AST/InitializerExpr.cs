using System.Collections.Generic;

namespace firebasic.AST
{
    public class InitializerExpr : Expr
    {
        public FBType TypeHint { get; set; }

        public List<Expr> Elements { get; set; }

        public InitializerExpr(List<Expr> elements, FBType typeHint = null, int line = 0, int col = 0)
            : base(line, col)
        {
            TypeHint = typeHint;
            Elements = elements;
        }
    }
}

using System.Collections.Generic;

namespace firebasic.AST
{
    public class CallExpr : Expr
    {
        public Expr Callee { get; set; }
        public List<Expr> Args { get; set; }

        public CallExpr(Expr callee, List<Expr> args,
            int line = 0, int col = 0)
            : base(line, col)
        {
            Args = args;
            Callee = callee;
        }
    }
}

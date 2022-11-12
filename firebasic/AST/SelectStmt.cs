using System.Collections.Generic;

namespace firebasic.AST
{
    public class SelectStmt : Stmt
    {
        public Expr Selected { get; set; }

        public Dictionary<Expr, List<Stmt>> Cases { get; set; }

        public SelectStmt(Expr selected,
            Dictionary<Expr, List<Stmt>> cases, int line = 0, int col = 0) :
            base(line, col)
        {
            Selected = selected;
            Cases = cases;
        }
    }
}

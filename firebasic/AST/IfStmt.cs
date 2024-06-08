using System.Collections.Generic;

namespace firebasic.AST
{
    public class IfStmt : Stmt
    {
        public Expr Condition { get; set; }

        public List<Stmt> OnTrue { get; set; }

        public List<Stmt> OnFalse { get; set; }

        public IfStmt(Expr condition, List<Stmt> @true,
            List<Stmt> @false = null, int line = 0, int col = 0) :
            base(line, col)
        {
            Condition = condition;
            OnTrue = @true;
            OnFalse = @false;
        }
    }
}

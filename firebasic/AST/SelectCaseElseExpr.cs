using System;
namespace firebasic.AST
{
    // Only used in Select...Case statements.
    public class SelectCaseElseExpr : Expr
    {
        public SelectCaseElseExpr(int line = 0, int col = 0)
            : base(line, col) { }
    }
}
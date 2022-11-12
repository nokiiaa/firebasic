using System.Collections.Generic;

namespace firebasic.AST
{
    public class ForStmt : Stmt
    {
        public TypedEntity Counter { get; set; }

        public Expr Destination { get; set; }

        public Expr Step { get; set; }

        public List<Stmt> Body { get; set; }

        public BinaryExpr.Operations ComparisonOperator { get; set; }

        public ForStmt(TypedEntity counter, BinaryExpr.Operations comp,
            Expr dest, Expr step, List<Stmt> body, int line = 0, int col = 0) :
            base(line, col)
        {
            ComparisonOperator = comp;
            Counter = counter;
            Destination = dest;
            Step = step;
            Body = body;
        }
    }
}

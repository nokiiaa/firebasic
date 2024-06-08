using System.Collections.Generic;

namespace firebasic.AST
{
    public class WhileStmt : Stmt
    {
        public bool IsDoLoop { get; set; }

        public bool CheckAfterBody { get; set; }

        public bool Not { get; set; }

        public Expr Condition { get; set; }

        public List<Stmt> Body { get; set; }

        public WhileStmt(Expr condition, List<Stmt> @body,
            bool isDoLoop = false, bool checkAfterBody = false, bool not = false,
            int line = 0, int col = 0) :
            base(line, col)
        {
            Not = not;
            Condition = condition;
            Body = @body;
            IsDoLoop = isDoLoop;
            CheckAfterBody = checkAfterBody;
        }
    }
}

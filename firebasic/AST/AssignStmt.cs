namespace firebasic.AST
{
    public class AssignStmt : Stmt
    {
        public enum Operations
        {
            None,
            Add,
            Sub,
            Mul,
            Div,
            Concat,
            Shl,
            Shr,
            Rol,
            Ror,
            Xor
        }

        public Expr Left { get; set; }

        public Expr Right { get; set; }

        public Operations Op { get; set; }

        public AssignStmt(Expr left, Expr right, Operations op,
            int line = 0, int col = 0)
            : base(line, col)
        {
            Left = left;
            Right = right;
            Op = op;
        }
    }
}

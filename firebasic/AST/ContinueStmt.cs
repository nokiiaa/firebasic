namespace firebasic.AST
{
    public class ContinueStmt : Stmt
    {
        public enum Enum
        {
            Do,
            For,
            While
        }

        public Enum What { get; set; }

        public ContinueStmt(Enum what, int line = 0, int col = 0)
            : base(line, col) => What = what;
    }
}

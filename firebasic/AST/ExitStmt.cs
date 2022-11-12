namespace firebasic.AST
{
    public class ExitStmt : Stmt
    {
        public enum Enum
        {
            Do,
            While,
            For,
            Select,
            Program
        }

        public Enum What { get; set; }

        public ExitStmt(Enum what, int line = 0, int col = 0) :
            base(line, col) => What = what;
    }
}

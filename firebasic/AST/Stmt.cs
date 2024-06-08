namespace firebasic.AST
{
    public class Stmt
    {
        public int Line { get; set; }

        public int Column { get; set; }

        public Stmt(int line = 0, int col = 0) { Line = line; Column = col; }
    }
}

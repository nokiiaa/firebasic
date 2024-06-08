namespace firebasic.AST
{
    public abstract class DeclarativeStmt : Stmt
    {
        public DeclarativeStmt(bool @private = false, int line = 0, int col = 0)
            : base(line, col) => Private = @private;

        public bool Private { get; set; }
    }
}

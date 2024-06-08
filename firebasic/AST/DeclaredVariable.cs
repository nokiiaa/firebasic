namespace firebasic.AST
{
    public class DeclaredVariable : TypedEntity
    {
        public DeclaredVariable(VarStmt parent, string name, FBType type,
            Expr initializer, int line = 0, int col = 0)
            : base(name, type, initializer, false, line, col) => Parent = parent;

        public VarStmt Parent { get; set; }
    }
}

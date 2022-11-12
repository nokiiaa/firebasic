using System.Collections.Generic;

namespace firebasic.AST
{
    public class NamespaceStmt : DeclarativeStmt
    {
        public Name Name { get; set; }

        public List<Stmt> Contents { get; set; }

        public NamespaceStmt(Name name, List<Stmt> contents,
            bool @private = false, int line = 0, int col = 0)
            : base(@private, line, col)
        {
            Name = name;
            Contents = contents;
        }
    }
}

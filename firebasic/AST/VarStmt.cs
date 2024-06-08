using System.Collections.Generic;

namespace firebasic.AST
{
    public class VarStmt : DeclarativeStmt
    {
        public bool Const { get; set; }

        public bool Static { get; set; }

        public List<DeclaredVariable> Declared { get; set; }

        public VarStmt(List<DeclaredVariable> declared,
            bool @const = false, bool @private = false, bool @static = false,
            int line = 0, int col = 0)
            : base(@private, line, col)
        {
            Const = @const;
            Declared = declared;
            Static = @static;
        }
    }
}

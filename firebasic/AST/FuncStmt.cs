using System.Collections.Generic;

namespace firebasic.AST
{
    public class FuncStmt : DeclarativeStmt
    {
        public FBType ReturnType { get; set; }

        public List<TypedEntity> Args { get; set; } = new List<TypedEntity>();

        public string Name { get; set; }

        public string ImportLibrary { get; set; }

        public bool Static { get; set; }

        public List<Stmt> Body { get; set; }

        public bool Declare { get; set; }

        public StructStmt MemberOf { get; set; }

        public FuncStmt(string name, FBType returnType, List<TypedEntity> args = null,
            List<Stmt> body = null, bool @private = false, bool @static = false,
            string importLibrary = null, bool declare = false, StructStmt memberOf = null,
            int line = 0, int col = 0)
            : base(@private, line, col)
        {
            if (args != null) Args = args;
            ReturnType = returnType;
            Name = name;
            ImportLibrary = importLibrary;
            Static = @static;
            Body = body;
            Declare = declare;
            MemberOf = memberOf;
        }
    }
}

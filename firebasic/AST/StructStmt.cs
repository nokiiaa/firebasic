using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace firebasic.AST
{
    public class StructStmt : DeclarativeStmt
    {
        public List<Stmt> Members { get; set; } = new List<Stmt>();

        public string Name { get; set; }

        public IEnumerable<DeclaredVariable> GetFields()
        {
            var fields = new List<DeclaredVariable>();
            foreach (VarStmt decl in Members.Where(x => x is VarStmt d && !d.Const))
                fields.AddRange(decl.Declared);
            return fields;
        }

        public IEnumerable<Stmt> GetFunctions(bool @public = false, bool @static = false)
            => Members.Where(x => x is FuncStmt func &&
                (!@public || !func.Private) &&
                (!@static || func.Static));

        public object GetFieldOrFunction(string name) =>
            (object)GetFields().ToList().Find(x => x.Name == name) ??
            GetFunctions().ToList().Find(x => (x as FuncStmt).Name == name);

        public StructStmt(string name, List<Stmt> members = null,
            bool @private = false, int line = 0, int col = 0)
            : base(@private, line, col)
        {
            Name = name;
            Members = members ?? new List<Stmt>();
        }
    }
}

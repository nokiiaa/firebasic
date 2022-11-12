using System.Collections.Generic;

namespace firebasic.AST
{
    public class Unit
    {
        public string Filename { get; set; }

        public List<Stmt> Statements { get; set; }

        public Unit(string filename, List<Stmt> stmts)
        {
            Filename = filename;
            Statements = stmts;
        }
    }
}

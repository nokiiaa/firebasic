using System.Collections.Generic;

namespace firebasic.AST
{
    public class EnumStmt : DeclarativeStmt
    {
        public FBType Base { get; set; }

        public string Name { get; set; }

        public List<EnumConstant> Values { get; set; }


        public EnumStmt(FBType @as, string name, List<EnumConstant> values,
            bool @private = false, int line = 0, int col = 0)
            : base(@private, line, col)
        {
            Base = @as;
            Name = name;
            Values = values;
        }
    }
}

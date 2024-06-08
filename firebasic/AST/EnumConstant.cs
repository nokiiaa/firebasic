namespace firebasic.AST
{
    public class EnumConstant
    {
        public EnumConstant(EnumStmt parent, string name, Expr value,
            int line = 0, int col = 0)
        {
            Line = line;
            Column = col;
            Parent = parent;
            Name = name;
            Value = value;
        }

        public int Line { get; set; }

        public int Column { get; set; }

        public EnumStmt Parent { get; set; }

        public string Name { get; set; }

        public Expr Value { get; set; }
    }
}
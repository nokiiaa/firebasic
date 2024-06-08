namespace firebasic.AST
{
    // A subroutine/function parameter/variable.
    public class TypedEntity
    {
        public int Line { get; set; }

        public int Column { get; set; }

        public string Name { get; set; }

        public FBType Type { get; set; }

        public Expr Initializer { get; set; }

        public bool Private { get; set; }

        public TypedEntity(string name, FBType type, Expr initializer,
            bool priv = false, int line = 0, int col = 0)
        {
            Line = line;
            Column = col;
            Initializer = initializer;
            Name = name;
            Type = type;
            Private = priv;
        }

        public override string ToString() => $"{Name} As {Type}";
    }
}

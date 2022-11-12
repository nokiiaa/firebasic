using System.Collections.Generic;

namespace firebasic.AST
{
    public class FBType
    {
        public FBType(int line = 0, int col = 0) { Line = line; Column = col; }

        public int Line { get; set; }
        public int Column { get; set; }

        public static FBPrimitiveType Prim(FBPrimitiveType.Enum type)
            => new FBPrimitiveType(type);
    }

    public class FBPrimitiveType : FBType
    {
        public enum Enum
        {
            Bool,
            Byte,
            Char,
            UShort,
            Short,
            UInteger,
            Integer,
            ULong,
            Long,
            WChar,
            Float,
            Double,
            Void,
        }

        public Enum Type { get; set; }

        public FBPrimitiveType(Enum type, int line = 0, int col = 0)
            : base(line, col) => Type = type;

        public override string ToString() => Type.ToString();
    }

    public class FBPointerType : FBType
    {
        public FBType To { get; set; }

        public FBPointerType(FBType to, int line = 0, int col = 0)
            : base(line, col) => To = to;

        public override string ToString() => $"{To}*";
    }

    public class FBArrayType : FBType
    {
        public FBType Of { get; set; }

        public Expr Length { get; set; }

        public FBArrayType(FBType of, Expr length, int line = 0, int col = 0)
            : base(line, col)
        {
            Of = of;
            Length = length;
        }

        public override string ToString() => $"{Of}()";
    }

    public class FBFuncPointerType : FBType
    {
        public bool MemberFunction => Args.Count > 0 && Args[0].Name == "me";

        public FBType Return { get; set; }

        public List<TypedEntity> Args { get; set; }

        public FBFuncPointerType(FBType ret, List<TypedEntity> args = null,
            int line = 0, int col = 0)
            : base(line, col)
        {
            Return = ret;
            if (args != null) Args = args;
        }

        public override string ToString() =>
            (Return is FBPrimitiveType fbpt && fbpt.Type == FBPrimitiveType.Enum.Void)
            ? $"Sub({string.Join(", ", Args)})"
            : $"Function({Return}({string.Join(", ", Args)}))";
    }

    public class FBNamedType : FBType
    {
        public Name Name { get; set; }

        public FBNamedType(Name name, int line = 0, int col = 0)
            : base(line, col) => Name = name;
        public override string ToString() => Name;
    }
}

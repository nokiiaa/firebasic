using firebasic.AST;
using NIR;
using NIR.Instructions;
using System;
using System.Collections.Generic;
using System.Linq;

namespace firebasic
{
    public class IRGenerator
    {
        readonly FBPrimitiveType SizeType = FBType.Prim(FBPrimitiveType.Enum.UInteger);
        public int Line { get; set; }
        public int Col { get; set; }
        public Scope Scope { get; set; }
        public Unit Unit { get; set; }
        public IRGenerator(Unit unit) => Unit = unit;

        IRFunction currentFunction = null;
        Dictionary<string, IRType> irGlobalTypeMap = new Dictionary<string, IRType>();
        Dictionary<string, IRType> irLocalTypeMap = new Dictionary<string, IRType>();
        Dictionary<string, int> varNameMap = new Dictionary<string, int>();

        void DeclareIRVar(string var)
        {
            if (!varNameMap.ContainsKey(var))
                varNameMap[var] = 0;
            varNameMap[var]++;
        }

        string GetIRVarName(string var)
        {
            if (!varNameMap.ContainsKey(var))
                DeclareIRVar(var);
            int ver = varNameMap[var];
            return $"{var}{(ver == 1 ? "" : $"${ver}")}";
        }

        IRType GetIRType(IRName name, bool deref = false)
        {
            IRType type = null;
            if (irGlobalTypeMap.ContainsKey(name))
                type = irGlobalTypeMap[name];
            if (irLocalTypeMap.ContainsKey(name))
                type = irLocalTypeMap[name];
            if (type != null)
                return deref ? (type as IRPointerType).To : type;
            return null;
        }

        void UpdateIRType(IRName name, IRType newType)
        {
            if (irGlobalTypeMap.ContainsKey(name))
                irGlobalTypeMap[name] = newType;
            if (irLocalTypeMap.ContainsKey(name))
                irLocalTypeMap[name] = newType;
        }

        int TempCounter { get; set; }

        string GenTemp(IRType type = null)
        {
            string name = $"%{TempCounter++}";
            if (type != null)
            {
                irGlobalTypeMap[name] = type;
                currentFunction.Emit(new IRLocalOp(name, type));
            }
            return name;
        }

        List<(string, IRType)> FBArgListToIR(FuncStmt fb)
        {
            var args = new List<(string, IRType)>();
            if (fb.MemberOf != null && !fb.Static)
                args.Add(("me", FBTypeToIR(new FBPointerType(new FBNamedType(fb.MemberOf.Name)))));
            foreach (TypedEntity entity in fb.Args)
                args.Add((entity.Name, FBTypeToIR(entity.Type)));
            return args;
        }

        Dictionary<string, IRType> FBArgListToIR(List<TypedEntity> fbList)
        {
            var args = new Dictionary<string, IRType>();
            foreach (TypedEntity entity in fbList)
                args[entity.Name] = FBTypeToIR(entity.Type);
            return args;
        }

        int SizeOfType(FBType type)
        {
            switch (type)
            {
                case FBPrimitiveType prim:
                    return prim.Type switch
                    {
                        FBPrimitiveType.Enum.Bool     => Platform.Current.SizeOfBool,
                        FBPrimitiveType.Enum.Byte     => Platform.Current.SizeOfChar,
                        FBPrimitiveType.Enum.Char     => Platform.Current.SizeOfChar,
                        FBPrimitiveType.Enum.WChar    => Platform.Current.SizeOfWChar,
                        FBPrimitiveType.Enum.Short    => Platform.Current.SizeOfShort,
                        FBPrimitiveType.Enum.UShort   => Platform.Current.SizeOfShort,
                        FBPrimitiveType.Enum.Integer  => Platform.Current.SizeOfInt,
                        FBPrimitiveType.Enum.UInteger => Platform.Current.SizeOfInt,
                        FBPrimitiveType.Enum.Long     => Platform.Current.SizeOfLong,
                        FBPrimitiveType.Enum.ULong    => Platform.Current.SizeOfLong,
                        FBPrimitiveType.Enum.Float    => Platform.Current.SizeOfFloat,
                        FBPrimitiveType.Enum.Double   => Platform.Current.SizeOfDouble,
                        FBPrimitiveType.Enum.Void     => Platform.Current.SizeOfChar,
                        _ => 0
                    };
                case FBPointerType _:
                case FBFuncPointerType _:
                    return Platform.Current.SizeOfPtr;
                case FBArrayType arr:
                    int length = arr.Length is LiteralExpr lit &&
                        lit.Type != LiteralExpr.Enum.Float &&
                        lit.Type != LiteralExpr.Enum.Double &&
                        lit.Type != LiteralExpr.Enum.String &&
                        lit.Type != LiteralExpr.Enum.WString ?
                        (int)(dynamic)lit.Value :
                        0;
                    return SizeOfType(arr.Of) * length;
                case FBNamedType named:
                    return Scope.Get<StructStmt>(named.Name).GetFields().Sum(x => SizeOfType(x.Type));
            }
            return 0;
        }

        IRType FBTypeToIR(FBType fb)
        {
            switch (fb)
            {
                case FBPrimitiveType prim:
                    return prim.Type switch
                    {
                        FBPrimitiveType.Enum.Void     => (IRType)new IRVoidType(),
                        FBPrimitiveType.Enum.Char     => new IRIntegerType(true, Platform.Current.SizeOfChar * 8),
                        FBPrimitiveType.Enum.WChar    => new IRIntegerType(true, Platform.Current.SizeOfWChar * 8),
                        FBPrimitiveType.Enum.Byte     => new IRIntegerType(false, Platform.Current.SizeOfChar * 8),
                        FBPrimitiveType.Enum.Short    => new IRIntegerType(true, Platform.Current.SizeOfShort * 8),
                        FBPrimitiveType.Enum.UShort   => new IRIntegerType(false, Platform.Current.SizeOfShort * 8),
                        FBPrimitiveType.Enum.Integer  => new IRIntegerType(true, Platform.Current.SizeOfInt * 8),
                        FBPrimitiveType.Enum.UInteger => new IRIntegerType(false, Platform.Current.SizeOfInt * 8),
                        FBPrimitiveType.Enum.Long     => new IRIntegerType(true, Platform.Current.SizeOfLong * 8),
                        FBPrimitiveType.Enum.ULong    => new IRIntegerType(false, Platform.Current.SizeOfLong * 8),
                        FBPrimitiveType.Enum.Bool     => new IRIntegerType(true, Platform.Current.SizeOfInt * 8),
                        FBPrimitiveType.Enum.Float    => new IRFloatType(@double: false),
                        FBPrimitiveType.Enum.Double   => new IRFloatType(@double: true),
                        _ => null
                    };
                case FBArrayType array:
                    return new IRPointerType(FBTypeToIR(array.Of));
                case FBPointerType pointer:
                    return new IRPointerType(FBTypeToIR(pointer.To));
                case FBNamedType named:
                    string name = named.Name.ToString();
                    var stct = Scope.Get<StructStmt>(name);
                    if (stct == null)
                    {
                        Output.Error(Unit.Filename, Line, Col, $"Undeclared typename {name}");
                        return null;
                    }
                    return new IRPointerType(new IRVoidType());
                    //new IRStructureType(stct.GetFields().Select(x => x.Type).Sum(SizeOfType));
                case FBFuncPointerType func:
                    return new IRFuncPointerType(FBTypeToIR(func.Return), FBArgListToIR(func.Args));
                default:
                    return null;
            }
        }

        // Deduces the type of an expression.
        // All validity checks for the expression are
        // assumed to already have been made.
        FBType TypeOfExpr(Expr expr)
        {
            if (expr.DeducedTypeCache != null)
                return expr.DeducedTypeCache;
            FBType deduced = null;
            switch (expr)
            {
                case InitializerExpr initializer:
                    deduced = initializer.TypeHint;
                    break;
                case AccessExpr access:
                    FBType t = TypeOfExpr(access.Accessed);
                    if (!(t is FBNamedType))
                        while (t is FBPointerType ptr)
                            t = ptr.To;
                    StructStmt stct = Scope.Get<StructStmt>((t as FBNamedType).Name);
                    object stmt = stct.GetFieldOrFunction(access.Member);
                    if (stmt is DeclaredVariable field)
                        deduced = field.Type;
                    else if (stmt is FuncStmt func)
                        deduced = new FBFuncPointerType(func.ReturnType,
                            // Prepend 'Me' argument to arglist because it's a member function.
                            func.Args.Prepend(new TypedEntity("me",
                            new FBPointerType(new FBNamedType(stct.Name)), null)).ToList(),
                            func.Line, func.Column);
                    break;
                case CallExpr call:
                    FBType calleeType = TypeOfExpr(call.Callee);
                    if (calleeType is FBFuncPointerType fptr)
                        deduced = fptr.Return;
                    else if (calleeType is FBArrayType arrType)
                        deduced = arrType.Of;
                    else if (calleeType is FBPointerType ptr)
                        deduced = ptr.To;
                    break;
                case CTypeExpr ctype:
                    deduced = ctype.ToType;
                    break;
                case LiteralExpr lit:
                    deduced = lit.Type switch
                    {
                        LiteralExpr.Enum.Bool     => (FBType)FBType.Prim(FBPrimitiveType.Enum.Bool),
                        LiteralExpr.Enum.Float    => FBType.Prim(FBPrimitiveType.Enum.Float),
                        LiteralExpr.Enum.Double   => FBType.Prim(FBPrimitiveType.Enum.Double),
                        LiteralExpr.Enum.Char     => FBType.Prim(FBPrimitiveType.Enum.Char),
                        LiteralExpr.Enum.Byte     => FBType.Prim(FBPrimitiveType.Enum.Byte),
                        LiteralExpr.Enum.Short    => FBType.Prim(FBPrimitiveType.Enum.Short),
                        LiteralExpr.Enum.UShort   => FBType.Prim(FBPrimitiveType.Enum.UShort),
                        LiteralExpr.Enum.Integer  => FBType.Prim(FBPrimitiveType.Enum.Integer),
                        LiteralExpr.Enum.UInteger => FBType.Prim(FBPrimitiveType.Enum.UInteger),
                        LiteralExpr.Enum.Long     => FBType.Prim(FBPrimitiveType.Enum.Long),
                        LiteralExpr.Enum.ULong    => FBType.Prim(FBPrimitiveType.Enum.ULong),
                        LiteralExpr.Enum.String   => new FBPointerType(FBType.Prim(FBPrimitiveType.Enum.Char)),
                        LiteralExpr.Enum.WString  => new FBPointerType(FBType.Prim(FBPrimitiveType.Enum.WChar)),
                        _ => null,
                    };
                    break;
                case UnaryExpr unary:
                    FBType type = TypeOfExpr(unary.Expr);
                    if (unary.Op == UnaryExpr.Operations.Plus || 
                        unary.Op == UnaryExpr.Operations.Minus || 
                        unary.Op == UnaryExpr.Operations.Not)
                        deduced = type;
                    else if (unary.Op == UnaryExpr.Operations.GetAddr)
                        deduced = new FBPointerType(type, type.Line, type.Column);
                    else if (unary.Op == UnaryExpr.Operations.Deref)
                    {
                        if (type is FBPointerType ptr)
                            deduced = ptr.To;
                        else if (type is FBArrayType arr)
                            deduced = arr.Of;
                    }
                    else if (unary.Op == UnaryExpr.Operations.SizeOf)
                        deduced = SizeType;
                    break;
                case NameExpr name:
                    DeclaredVariable dv; FuncStmt fs; EnumConstant ec; ExpressionAlias ea;
                    if ((dv = Scope.Get<DeclaredVariable>(name.Name)) != null)
                        deduced = dv.Type;
                    else if ((fs = Scope.Get<FuncStmt>(name.Name)) != null)
                        deduced = new FBFuncPointerType(
                            fs.ReturnType, fs.Args, fs.Line, fs.Column);
                    else if ((ec = Scope.Get<EnumConstant>(name.Name)) != null)
                        deduced = ec.Parent.Base;
                    else if ((ea = Scope.Get<ExpressionAlias>(name.Name)) != null)
                        deduced = TypeOfExpr(ea.Expr);
                    break;
                case BinaryExpr binary:
                    var tLeft = TypeOfExpr(binary.Left);
                    var tRight = TypeOfExpr(binary.Right);
                    if (tLeft is FBArrayType lArr)
                        tLeft = new FBPointerType(lArr.Of);
                    if (tRight is FBArrayType rArr)
                        tRight = new FBPointerType(rArr.Of);
                    switch (binary.Op)
                    {
                        case BinaryExpr.Operations.Ge:
                        case BinaryExpr.Operations.Gt:
                        case BinaryExpr.Operations.Le:
                        case BinaryExpr.Operations.Lt:
                        case BinaryExpr.Operations.Eq:
                        case BinaryExpr.Operations.Neq:
                            deduced = FBType.Prim(FBPrimitiveType.Enum.Bool);
                            break;
                        case BinaryExpr.Operations.Shl:
                        case BinaryExpr.Operations.Shr:
                        case BinaryExpr.Operations.Rol:
                        case BinaryExpr.Operations.Ror:
                            deduced = tLeft;
                            break;
                        case BinaryExpr.Operations.Add:
                        case BinaryExpr.Operations.Sub:
                        case BinaryExpr.Operations.Mul:
                        case BinaryExpr.Operations.Div:
                        case BinaryExpr.Operations.Mod:
                        case BinaryExpr.Operations.And:
                        case BinaryExpr.Operations.Xor:
                        case BinaryExpr.Operations.Or:
                            if (tLeft is FBPointerType ptr1)
                                return ptr1;
                            if (tRight is FBPointerType ptr2)
                                return ptr2;
                            var pLeft = (tLeft as FBPrimitiveType).Type;
                            var pRight = (tRight as FBPrimitiveType).Type;
                            if (pLeft == FBPrimitiveType.Enum.Double || pRight == FBPrimitiveType.Enum.Double)
                                deduced = FBType.Prim(FBPrimitiveType.Enum.Double);
                            else if (pLeft == FBPrimitiveType.Enum.Float || pRight == FBPrimitiveType.Enum.Float)
                                deduced = FBType.Prim(FBPrimitiveType.Enum.Float);
                            else if (pLeft == FBPrimitiveType.Enum.ULong || pRight == FBPrimitiveType.Enum.ULong)
                                deduced = FBType.Prim(FBPrimitiveType.Enum.ULong);
                            else if (pLeft == FBPrimitiveType.Enum.Long || pRight == FBPrimitiveType.Enum.Long)
                                deduced = FBType.Prim(FBPrimitiveType.Enum.Long);
                            else if (pLeft == FBPrimitiveType.Enum.UInteger || pRight == FBPrimitiveType.Enum.UInteger)
                                deduced = FBType.Prim(FBPrimitiveType.Enum.UInteger);
                            else if (pLeft == FBPrimitiveType.Enum.Integer || pRight == FBPrimitiveType.Enum.Integer)
                                deduced = FBType.Prim(FBPrimitiveType.Enum.Integer);
                            else if (pLeft == FBPrimitiveType.Enum.Bool || pRight == FBPrimitiveType.Enum.Bool)
                                deduced = FBType.Prim(FBPrimitiveType.Enum.Bool);
                            else if (pLeft == FBPrimitiveType.Enum.UShort || pRight == FBPrimitiveType.Enum.UShort)
                                deduced = FBType.Prim(FBPrimitiveType.Enum.UShort);
                            else if (pLeft == FBPrimitiveType.Enum.WChar || pRight == FBPrimitiveType.Enum.WChar)
                                deduced = FBType.Prim(FBPrimitiveType.Enum.WChar);
                            else if (pLeft == FBPrimitiveType.Enum.Short || pRight == FBPrimitiveType.Enum.Short)
                                deduced = FBType.Prim(FBPrimitiveType.Enum.Short);
                            else if (pLeft == FBPrimitiveType.Enum.Byte || pRight == FBPrimitiveType.Enum.Byte)
                                deduced = FBType.Prim(FBPrimitiveType.Enum.Byte);
                            else if (pLeft == FBPrimitiveType.Enum.Char || pRight == FBPrimitiveType.Enum.Char)
                                deduced = FBType.Prim(FBPrimitiveType.Enum.Char);
                            break;
                        case BinaryExpr.Operations.Concat:
                            if ((tLeft as FBNamedType).Name == "WString" || (tRight as FBNamedType).Name == "WString")
                                deduced = new FBNamedType("WString");
                            if ((tLeft as FBNamedType).Name == "String" || (tRight as FBNamedType).Name == "String")
                                deduced = new FBNamedType("String");
                            break;
                    }
                    break;
            }
            if (deduced == null)
                throw new Exception("Failed to deduce type of expression");
            return expr.DeducedTypeCache = deduced;
        }

        // Returns whether an expression is an lvalue,
        // assuming it is valid in the current context.
        bool IsExprLvalue(Expr expr, bool disallowConst = false)
        {
            if (expr is AccessExpr)
                return true;
            else if (expr is UnaryExpr unary && unary.Op == UnaryExpr.Operations.Deref)
                return true;
            else if (expr is CallExpr call)
            {
                FBType type = TypeOfExpr(call.Callee);
                if ((type is FBArrayType || type is FBPointerType) && call.Args.Count == 1)
                {
                    CastabilityTest(call.Args[0], new FBPrimitiveType(FBPrimitiveType.Enum.UInteger));
                    return true;
                }
            }
            else if (expr is NameExpr name)
            {
                if (Scope.Get<DeclaredVariable>(name.Name.ToString()) is DeclaredVariable dv)
                    return !(disallowConst && dv.Parent.Const);
                if (Scope.Get<ExpressionAlias>(name.Name.ToString()) is ExpressionAlias alias)
                    return IsExprLvalue(alias.Expr);
                return false;
            }
            return false;
        }

        bool IsIntegerType(FBType type) =>
            type is FBPrimitiveType prim &&
                (prim.Type == FBPrimitiveType.Enum.Bool || prim.Type == FBPrimitiveType.Enum.Byte    ||
                prim.Type == FBPrimitiveType.Enum.Char     || prim.Type == FBPrimitiveType.Enum.UInteger ||
                prim.Type == FBPrimitiveType.Enum.Integer  || prim.Type == FBPrimitiveType.Enum.Long     ||
                prim.Type == FBPrimitiveType.Enum.ULong    || prim.Type == FBPrimitiveType.Enum.Short    ||
                prim.Type == FBPrimitiveType.Enum.WChar    || prim.Type == FBPrimitiveType.Enum.UShort);

        bool IsNumericType(FBType type) => IsIntegerType(type) || IsFloatType(type);

        bool IsStringType(FBType type) =>
            // type == String || type == WString
            type is FBNamedType named &&
            (named.Name == "String" || named.Name == "WString");

        bool IsFloatType(FBType type) =>
            type is FBPrimitiveType prim && (prim.Type == FBPrimitiveType.Enum.Float ||
                prim.Type == FBPrimitiveType.Enum.Double);

        bool IsAnyPointer(FBType type) => type is FBPointerType || type is FBFuncPointerType;

        Expr GetAssignedValue(AssignStmt assign) =>
            assign.Op == AssignStmt.Operations.None ? assign.Right :
                new BinaryExpr(assign.Left, assign.Op switch
                {
                    AssignStmt.Operations.Add => BinaryExpr.Operations.Add,
                    AssignStmt.Operations.Sub => BinaryExpr.Operations.Sub,
                    AssignStmt.Operations.Mul => BinaryExpr.Operations.Mul,
                    AssignStmt.Operations.Div => BinaryExpr.Operations.Div,
                    AssignStmt.Operations.Rol => BinaryExpr.Operations.Rol,
                    AssignStmt.Operations.Ror => BinaryExpr.Operations.Ror,
                    AssignStmt.Operations.Shl => BinaryExpr.Operations.Shl,
                    AssignStmt.Operations.Shr => BinaryExpr.Operations.Shr,
                    AssignStmt.Operations.Xor => BinaryExpr.Operations.Xor,
                    _ => (BinaryExpr.Operations)0
                }, assign.Right, assign.Right.Line, assign.Right.Column);

        void CastabilityTest(Expr expr, FBType to, bool @implicit = true)
        {
            if (expr == null) return;
            FBType a = TypeOfExpr(expr), b = to;
            if ((!@implicit && (IsNumericType(a) || IsAnyPointer(a)) || IsStringType(a))
                && IsStringType(to))
                return;

            if (IsNumericType(a) && IsNumericType(b) ||
                (a is FBArrayType || IsAnyPointer(a)) && (b is FBArrayType || IsAnyPointer(b)))
                return;
            if (IsAnyPointer(a) && IsIntegerType(b) ||
                IsAnyPointer(b) && IsIntegerType(a))
            {
                if (@implicit)
                    Output.Warning(Unit.Filename,
                        expr.Line, expr.Column, $"Implicit cast of '{a}' to '{b}'");
                return;
            }
            if (a is FBNamedType aNamed && b is FBNamedType bNamed &&
                aNamed.Name.Equals(bNamed.Name))
                return;
            Output.Error(Unit.Filename, expr.Line, expr.Column,
                $"Cannot implicitly cast '{a}' to '{b}'");
        }

        bool IsVoidType(FBType type) => type is FBPrimitiveType prim &&
            prim.Type == FBPrimitiveType.Enum.Void;

        int whileNest = 0;
        int forNest = 0;
        int doNest = 0;
        readonly int selectNest = 0;

        // Used to detect circular definitions
        // (and other errors, such as "Me" outside a member function).
        readonly Stack<Name> definitionStack = new Stack<Name>();
        readonly Stack<FuncStmt> funcStack = new Stack<FuncStmt>();
        readonly Stack<string> structNameStack = new Stack<string>();
        // Used to jump to the beginning or end of the
        // loop when compiling Continue/Exit statements
        readonly Stack<(string, string)> forInfoStack = new Stack<(string, string)>();
        readonly Stack<(string, string)> whileInfoStack = new Stack<(string, string)>();
        readonly Stack<(string, string)> doInfoStack = new Stack<(string, string)>();

        static LiteralExpr.Enum ChooseLiteralRank(LiteralExpr.Enum a, LiteralExpr.Enum b)
        {
            if (a == LiteralExpr.Enum.WString || b == LiteralExpr.Enum.WString)
                return LiteralExpr.Enum.WString;
            if (a == LiteralExpr.Enum.String || b == LiteralExpr.Enum.String)
                return LiteralExpr.Enum.String;
            if (a == LiteralExpr.Enum.Double || b == LiteralExpr.Enum.Double)
                return LiteralExpr.Enum.Double;
            if (a == LiteralExpr.Enum.Float || b == LiteralExpr.Enum.Float)
                return LiteralExpr.Enum.Float;
            if (a == LiteralExpr.Enum.ULong || b == LiteralExpr.Enum.ULong)
                return LiteralExpr.Enum.ULong;
            if (a == LiteralExpr.Enum.Long || b == LiteralExpr.Enum.Long)
                return LiteralExpr.Enum.Long;
            if (a == LiteralExpr.Enum.UInteger || b == LiteralExpr.Enum.UInteger)
                return LiteralExpr.Enum.UInteger;
            if (a == LiteralExpr.Enum.Integer || b == LiteralExpr.Enum.Integer)
                return LiteralExpr.Enum.Integer;
            if (a == LiteralExpr.Enum.Bool || b == LiteralExpr.Enum.Bool)
                return LiteralExpr.Enum.Bool;
            if (a == LiteralExpr.Enum.UShort || b == LiteralExpr.Enum.UShort)
                return LiteralExpr.Enum.UShort;
            if (a == LiteralExpr.Enum.WChar || b == LiteralExpr.Enum.WChar)
                return LiteralExpr.Enum.WChar;
            if (a == LiteralExpr.Enum.Short || b == LiteralExpr.Enum.Short)
                return LiteralExpr.Enum.Short;
            if (a == LiteralExpr.Enum.Byte || b == LiteralExpr.Enum.Byte)
                return LiteralExpr.Enum.Byte;
            if (a == LiteralExpr.Enum.Char || b == LiteralExpr.Enum.Char)
                return LiteralExpr.Enum.Char;
            return 0;
        }

        static double ConvertToDouble(object x)
        {
            return x switch { double d => d, float f => f, long l => l, ulong u => u, _ => 0 };
        }

        static float ConvertToFloat(object x)
        {
            return x switch { double d => (float)d, float f => f, long l => l, ulong u => u, _ => 0 };
        }

        static ulong ConvertToUlong(object x)
        {
            return x switch
            {
                double d => (ulong)d,
                float f => (ulong)f,
                long l => (ulong)l,
                ulong u => u,
                _ => 0ul
            };
        }

        static object ConvertToIntType(object x, LiteralExpr.Enum type)
        {
            if (x is double d) x = (long)d;
            if (x is float f) x = (long)f;
            ulong mask = type switch
            {
                LiteralExpr.Enum.ULong => Platform.Current.ULongLimits.Max,
                LiteralExpr.Enum.Long => Platform.Current.ULongLimits.Max,
                LiteralExpr.Enum.UInteger => Platform.Current.UIntLimits.Max,
                LiteralExpr.Enum.Integer => Platform.Current.UIntLimits.Max,
                LiteralExpr.Enum.UShort => Platform.Current.UShortLimits.Max,
                LiteralExpr.Enum.Short => Platform.Current.UShortLimits.Max,
                LiteralExpr.Enum.WChar => Platform.Current.UShortLimits.Max,
                LiteralExpr.Enum.Char => Platform.Current.UCharLimits.Max,
                LiteralExpr.Enum.Byte => Platform.Current.UCharLimits.Max,
                LiteralExpr.Enum.Bool => Platform.Current.UIntLimits.Max,
                _ => 0ul
            };
            ulong result = (x is long ? (ulong)(long)x : (ulong)x) & mask;
            // Sign extend the number
            if (type == LiteralExpr.Enum.Bool ||
                type == LiteralExpr.Enum.Char ||
                type == LiteralExpr.Enum.Short ||
                type == LiteralExpr.Enum.Integer ||
                type == LiteralExpr.Enum.Long ||
                type == LiteralExpr.Enum.WChar)
            {
                if ((result & (mask ^ mask >> 1)) != 0)
                    result |= ~mask;
                return (long)result;
            }
            return result;
        }

        static LiteralExpr.Enum ToLiteralType(FBPrimitiveType.Enum primType) =>
            primType switch
            {
                FBPrimitiveType.Enum.Bool => LiteralExpr.Enum.Bool,
                FBPrimitiveType.Enum.Char => LiteralExpr.Enum.Char,
                FBPrimitiveType.Enum.Byte => LiteralExpr.Enum.Byte,
                FBPrimitiveType.Enum.Short => LiteralExpr.Enum.Short,
                FBPrimitiveType.Enum.UShort => LiteralExpr.Enum.UShort,
                FBPrimitiveType.Enum.Integer => LiteralExpr.Enum.Integer,
                FBPrimitiveType.Enum.UInteger => LiteralExpr.Enum.UInteger,
                FBPrimitiveType.Enum.Long => LiteralExpr.Enum.Long,
                FBPrimitiveType.Enum.ULong => LiteralExpr.Enum.ULong,
                FBPrimitiveType.Enum.Float => LiteralExpr.Enum.Float,
                FBPrimitiveType.Enum.Double => LiteralExpr.Enum.Double,
                FBPrimitiveType.Enum.WChar => LiteralExpr.Enum.WChar,
                _ => LiteralExpr.Enum.Integer
            };

        Expr SimplifyExpr(Expr expr)
        {
            // The expression will be modified
            expr.DeducedTypeCache = null;

            if (expr is BinaryExpr bin)
            {
                Expr left  = bin.Left  = SimplifyExpr(bin.Left),
                     right = bin.Right = SimplifyExpr(bin.Right);

                if (bin.Op == BinaryExpr.Operations.Mul || bin.Op == BinaryExpr.Operations.Div)
                {
                    if (bin.Op == BinaryExpr.Operations.Mul && left is LiteralExpr le
                        && (ulong)ConvertToIntType(le.Value, LiteralExpr.Enum.ULong) == 1UL)
                        return right;
                    if (right is LiteralExpr re
                        && (ulong)ConvertToIntType(re.Value, LiteralExpr.Enum.ULong) == 1UL)
                        return left;
                }

                if (bin.Op == BinaryExpr.Operations.Add || bin.Op == BinaryExpr.Operations.Sub)
                {
                    if (bin.Op == BinaryExpr.Operations.Add &&
                        left is LiteralExpr le
                        && (ulong)ConvertToIntType(le.Value, LiteralExpr.Enum.ULong) == 0UL)
                        return right;
                    if (right is LiteralExpr re
                        && (ulong)ConvertToIntType(re.Value, LiteralExpr.Enum.ULong) == 0UL)
                        return left;
                }


                if (left is LiteralExpr lit1 && right is LiteralExpr lit2)
                {
                    if (lit1.Value is string sa && lit2.Value is string sb)
                        return new LiteralExpr(
                            lit1.Type == LiteralExpr.Enum.WString ||
                            lit2.Type == LiteralExpr.Enum.WString ?
                            LiteralExpr.Enum.WString : LiteralExpr.Enum.String,
                            sa + sb, lit1.Line, lit1.Column);

                    LiteralExpr result = new LiteralExpr(
                        ChooseLiteralRank(lit1.Type, lit2.Type), null,
                        lit1.Line, lit1.Column);
                    object a = lit1.Value, b = lit2.Value;
                    if (a is double) b = ConvertToDouble(b);
                    else if (b is double) a = ConvertToDouble(a);
                    else if (a is float) b = ConvertToFloat(b);
                    else if (b is float) a = ConvertToFloat(a);
                    else if (a is ulong) b = ConvertToUlong(b);
                    else if (b is ulong) a = ConvertToUlong(a);
                    try
                    {
                        result.Value = bin.Op switch
                        {
                            BinaryExpr.Operations.Add => (dynamic)a + (dynamic)b,
                            BinaryExpr.Operations.Sub => (dynamic)a - (dynamic)b,
                            BinaryExpr.Operations.Mul => (dynamic)a * (dynamic)b,
                            BinaryExpr.Operations.Div => (dynamic)a / (dynamic)b,
                            BinaryExpr.Operations.And => (dynamic)a & (dynamic)b,
                            BinaryExpr.Operations.Xor => (dynamic)a ^ (dynamic)b,
                            BinaryExpr.Operations.Or  => (dynamic)a | (dynamic)b,
                            BinaryExpr.Operations.Shl => (dynamic)a << (b is ulong u ? (int)u : (int)(long)b),
                            BinaryExpr.Operations.Shr => (dynamic)a >> (b is ulong u ? (int)u : (int)(long)b),
                            BinaryExpr.Operations.Neq => (dynamic)a != (dynamic)b ? -1L : 0L,
                            BinaryExpr.Operations.Eq  => (dynamic)a == (dynamic)b ? -1L : 0L,
                            BinaryExpr.Operations.Ge  => (dynamic)a >= (dynamic)b ? -1L : 0L,
                            BinaryExpr.Operations.Gt  => (dynamic)a > (dynamic)b ? -1L : 0L,
                            BinaryExpr.Operations.Le  => (dynamic)a <= (dynamic)b ? -1L : 0L,
                            BinaryExpr.Operations.Lt  => (dynamic)a < (dynamic)b ? -1L : 0L,
                            BinaryExpr.Operations.Mod => (dynamic)a % (dynamic)b,
                            _ => 0ul
                        };
                    }
                    catch (DivideByZeroException)
                    {
                        Output.Error(Unit.Filename,
                            bin.Line, bin.Column, "Compile-time division by zero");
                        result.Value = (dynamic)a * 0; // Galaxy brain type preservation
                    }
                    if (result.Type != LiteralExpr.Enum.Float && result.Type != LiteralExpr.Enum.Double)
                        result.Value = ConvertToIntType(result.Value, result.Type);
                    return result;
                }
            }
            else if (expr is CTypeExpr ctype)
            {
                Expr casted = ctype.Casted = SimplifyExpr(ctype.Casted);
                if (casted is LiteralExpr lit)
                {
                    LiteralExpr newLit = null;
                    if (ctype.ToType is FBPrimitiveType prim)
                    {
                        newLit = new LiteralExpr(ToLiteralType(prim.Type), null,
                            ctype.Line, ctype.Column);
                        if (newLit.Type == LiteralExpr.Enum.Double)
                            newLit.Value = ConvertToDouble(lit.Value);
                        else if (newLit.Type == LiteralExpr.Enum.Float)
                            newLit.Value = ConvertToFloat(lit.Value);
                        else
                            newLit.Value = ConvertToIntType(lit.Value, newLit.Type);
                    }
                    else if (IsStringType(ctype.ToType))
                    {
                        string strName = (ctype.ToType as FBNamedType).Name;
                        newLit = new LiteralExpr(strName[0] == 'W'
                            ? LiteralExpr.Enum.WString
                            : LiteralExpr.Enum.String, null,
                            ctype.Line, ctype.Column);

                        switch (lit.Type)
                        {
                            case LiteralExpr.Enum.Bool:
                                lit.Value = (long)lit.Value == 0L ? "False" : "True";
                                break;
                            case LiteralExpr.Enum.Short:
                            case LiteralExpr.Enum.Integer:
                            case LiteralExpr.Enum.Long:
                                lit.Value = ((long)lit.Value).ToString();
                                break;
                            case LiteralExpr.Enum.Byte:
                            case LiteralExpr.Enum.Char:
                            case LiteralExpr.Enum.WChar:
                                lit.Value = ((char)(ulong)lit.Value).ToString();
                                break;
                            default:
                                lit.Value = lit.Value.ToString();
                                break;
                        }
                    }
                    else
                        return expr;
                    return newLit;
                }
            }
            else if (expr is NameExpr name)
            {
                if (definitionStack.Contains(name.Name))
                {
                    Output.Error(Unit.Filename,
                        expr.Line, expr.Column,
                        "Circular definition detected during expression evaluation");
                    return null;
                }
                definitionStack.Push(name.Name);
                EnumConstant cnst = Scope.Get<EnumConstant>(name.Name);
                DeclaredVariable var = Scope.Get<DeclaredVariable>(name.Name);
                // Ignore identifiers of non-constant variables
                if (var == null || !(var.Parent is VarStmt ds) || !ds.Const)
                    var = null;
                if (cnst == null)
                {
                    if (var == null)
                    {
                        definitionStack.Pop();
                        return name;
                    }
                    else
                    {
                        Expr substituted = SimplifyExpr(var.Initializer);
                        definitionStack.Pop();
                        if (!(substituted is LiteralExpr))
                            return name;
                        return substituted;
                    }
                }
                else
                {
                    Expr @enum = SimplifyExpr(cnst.Value);
                    definitionStack.Pop();
                    return @enum;
                }
            }
            else if (expr is CallExpr call)
            {
                call.Args = call.Args.Select(SimplifyExpr).ToList();
                call.Callee = SimplifyExpr(call.Callee);
            }
            else if (expr is InitializerExpr array)
                array.Elements = array.Elements.Select(SimplifyExpr).ToList();
            else if (expr is UnaryExpr unary)
            {
                Expr operand = unary.Expr = SimplifyExpr(unary.Expr);
                if (unary.Expr == null) return null;
                if (unary.Op == UnaryExpr.Operations.Plus)
                    return operand;
                else if (unary.Op == UnaryExpr.Operations.Not)
                {
                    if (operand is UnaryExpr unary2 &&
                        unary2.Op == UnaryExpr.Operations.Not)
                        return unary2.Expr;
                    else if (operand is LiteralExpr lit)
                    {
                        if (lit.Value is ulong u)
                            return new LiteralExpr(
                                lit.Type, ConvertToIntType(~u, lit.Type),
                                unary.Line, unary.Column);
                        else if (lit.Value is long l)
                            return new LiteralExpr(
                                lit.Type, ConvertToIntType(~l, lit.Type));
                    }
                }
                else if (unary.Op == UnaryExpr.Operations.Minus)
                {
                    if (operand is UnaryExpr unary2 &&
                        unary2.Op == UnaryExpr.Operations.Minus)
                        return unary2.Expr;
                    else if (operand is LiteralExpr lit)
                    {
                        if (lit.Value is double d)
                            return new LiteralExpr(
                                LiteralExpr.Enum.Double, -d, unary.Line, unary.Column);
                        else if (lit.Value is float f)
                            return new LiteralExpr(
                                LiteralExpr.Enum.Float, -f, unary.Line, unary.Column);
                        else if (lit.Value is ulong u)
                            return new LiteralExpr(
                                lit.Type, ConvertToIntType(-(long)u, lit.Type),
                                unary.Line, unary.Column);
                        else if (lit.Value is long l)
                            return new LiteralExpr(lit.Type, ConvertToIntType(-l, lit.Type),
                                unary.Line, unary.Column);
                    }
                }
            }
            return expr;
        }

        void FillStructMemberScope(StructStmt stct, bool generating = false)
        {
            VarStmt dummyDecl = new VarStmt(null);
            FBNamedType stctType = new FBNamedType(stct.Name);
            if (stct == null)
                return;
            FillScope(stct.Members.Where(x =>
            {
                if (x is DeclarativeStmt d && d.Private)
                {
                    if (x is FuncStmt f)
                        return f.Static;
                    if (x is VarStmt v)
                        return v.Static;
                    return true;
                }
                return false;
            }), clear: false);

            if (generating)
                DeclareIRVar("me");

            Scope.Declare("me", new DeclaredVariable(
                dummyDecl, "me", new FBPointerType(stctType), null,
                stctType.Line, stctType.Column));

            foreach (Stmt func in stct.GetFunctions().Where(x => !(x as FuncStmt).Static))
            {
                FuncStmt fs = func as FuncStmt;
                Scope.Declare(fs.Name,
                    new ExpressionAlias(fs.Name,
                    new AccessExpr(new NameExpr("me"), fs.Name)));
            }

            foreach (DeclaredVariable field in stct.GetFields().Where(x => !x.Parent.Static))
            {
                if (generating)
                    DeclareIRVar(field.Name);

                Scope.Declare(field.Name,
                    new ExpressionAlias(field.Name,
                    new AccessExpr(new NameExpr("me"), field.Name)));
            }

        }

        void ValidateType(FBType type, bool allowNoArraySize = true, bool verifyCircularStruct = true)
        {
            bool CheckCircularStructDefinition(FBNamedType named)
                => structNameStack.Contains(named.Name) ||
                !Scope.Get<StructStmt>(named.Name).GetFields().Any(
                    x => x.Type is FBNamedType n && !CheckCircularStructDefinition(n));
            if (type is FBNamedType named && verifyCircularStruct && CheckCircularStructDefinition(named))
                Output.Error(Unit.Filename,
                    type.Line, type.Column, "Circular structure definition detected");
            else if (type is FBFuncPointerType func)
            {
                ValidateType(func.Return);
                foreach (TypedEntity arg in func.Args)
                {
                    ValidateType(arg.Type);
                    ValidateExpr(arg.Initializer);
                }
            }
            else if (type is FBPointerType ptr)
                ValidateType(ptr.To, allowNoArraySize, verifyCircularStruct: false);
            else if (type is FBArrayType array)
            {
                if (!allowNoArraySize && array.Length == null)
                    Output.Error(Unit.Filename,
                        type.Line, type.Column, "Array length required in type");
                ValidateType(array.Of, allowNoArraySize);
            }
        }

        // Returns a boolean, indicating whether an
        // expression and its sub-expressions are
        // semantically valid in the current context.
        // If the expression is not valid, errors are printed.
        bool ValidateExpr(Expr expr)
        {
            int errors = Output.Errors;
            switch (expr)
            {
                case CTypeExpr ctype:
                    if (ValidateExpr(ctype.Casted))
                        CastabilityTest(ctype.Casted, ctype.ToType, @implicit: false);
                    break;
                case NameExpr name:
                    string sName = name.ToString();
                    if (sName == "me" && (funcStack.Count == 0 || funcStack.Peek().MemberOf == null))
                        Output.Error(Unit.Filename,
                            name.Line, name.Column, "'Me' expression outside a member function");
                    else if (Scope.Get<object>(sName) == null)
                        Output.Error(Unit.Filename,
                            name.Line, name.Column, $"Undeclared identifier {sName}");
                    break;
                case AccessExpr access:
                    if (ValidateExpr(access.Accessed))
                    {
                        FBType type = TypeOfExpr(access.Accessed);
                        FBType derefType = type;
                        if (!(type is FBNamedType))
                            while (derefType is FBPointerType ptr)
                                derefType = ptr.To;
                        if (derefType is FBNamedType named)
                        {
                            string stctName = named.Name.ToString();
                            var stct = Scope.Get<StructStmt>(stctName);
                            if (stct.GetFieldOrFunction(access.Member) == null)
                                Output.Error(Unit.Filename,
                                    access.Line, access.Column,
                                    $"Structure '{stctName}' contains no field or function called '{access.Member}'");
                        }
                        else
                            Output.Error(Unit.Filename,
                                access.Line, access.Column,
                                "Attempt to access a member of something that's not a structure");
                    }
                    break;
                case InitializerExpr array:
                    foreach (Expr elem in array.Elements)
                        ValidateExpr(elem);
                    break;
                case RangeExpr range:
                    ValidateExpr(range.Start);
                    ValidateExpr(range.Stop);
                    if (!IsNumericType(TypeOfExpr(range.Start)))
                        Output.Error(Unit.Filename, range.Start.Line, range.Start.Column,
                            "Both sides of range expression must be of a numeric type");
                    else if (!IsNumericType(TypeOfExpr(range.Stop)))
                        Output.Error(Unit.Filename, range.Stop.Line, range.Stop.Column,
                            "Both sides of range expression must be of a numeric type");
                    break;
                case BinaryExpr binary:
                    if (ValidateExpr(binary.Left) && ValidateExpr(binary.Right))
                    {
                        FBType tLeft = TypeOfExpr(binary.Left),
                               tRight = TypeOfExpr(binary.Right);
                        if (tLeft is FBFuncPointerType || tRight is FBFuncPointerType)
                            Output.Error(Unit.Filename,
                                binary.Line, binary.Column,
                                "Function pointer in binary expression");
                        else if (tLeft is FBNamedType || tRight is FBNamedType)
                            Output.Error(Unit.Filename,
                                binary.Line, binary.Column,
                                "Non-numeric/pointer type in binary expression");
                        else
                        {
                            void bin_error(string op, string type = "integral")
                                => Output.Error(Unit.Filename,
                                binary.Line, binary.Column,
                                $"'{op}' binary expression takes {type} operands");
                            bool hasPointer =
                                   tLeft is FBPointerType || tLeft is FBArrayType ||
                                   tRight is FBPointerType || tRight is FBArrayType;
                            bool allIntegral =
                                !hasPointer && IsIntegerType(tLeft) && IsIntegerType(tRight);
                            bool hasFloat =
                                !allIntegral && IsFloatType(tLeft) && IsFloatType(tRight);
                            if (binary.Op == BinaryExpr.Operations.Add)
                            {
                                if (hasPointer && !IsIntegerType(tLeft) && !IsIntegerType(tRight))
                                    Output.Error(Unit.Filename,
                                        binary.Line, binary.Column,
                                        "At least one operand of '+' expression with pointer must be an integer");
                            }
                            else if (binary.Op == BinaryExpr.Operations.Sub)
                            {
                                if (tLeft is FBPointerType && !IsIntegerType(tRight))
                                    Output.Error(Unit.Filename,
                                        binary.Line, binary.Column,
                                        "Right operand of '-' expression with pointer must be an integer");
                            }
                            else if (binary.Op == BinaryExpr.Operations.Concat &&
                                (!IsStringType(tLeft) || !IsStringType(tRight)))
                                bin_error("&", "string");
                            else if (binary.Op == BinaryExpr.Operations.Mul && hasPointer)
                                bin_error("*", "numeric");
                            else if (binary.Op == BinaryExpr.Operations.Div && hasPointer)
                                bin_error("/", "numeric");
                            else if (binary.Op == BinaryExpr.Operations.Mod && !allIntegral)
                                bin_error("Mod");
                            else if (binary.Op == BinaryExpr.Operations.And && !allIntegral)
                                bin_error("And");
                            else if (binary.Op == BinaryExpr.Operations.Xor && !allIntegral)
                                bin_error("Xor");
                            else if (binary.Op == BinaryExpr.Operations.Or && !allIntegral)
                                bin_error("Or");
                            else if (binary.Op == BinaryExpr.Operations.Shl && !allIntegral)
                                bin_error("<<");
                            else if (binary.Op == BinaryExpr.Operations.Shr && !allIntegral)
                                bin_error(">>");
                            else if (binary.Op == BinaryExpr.Operations.Rol && !allIntegral)
                                bin_error("<<<");
                            else if (binary.Op == BinaryExpr.Operations.Ror && !allIntegral)
                                bin_error(">>>");
                            else if (binary.Op == BinaryExpr.Operations.Eq ||
                                binary.Op == BinaryExpr.Operations.Neq ||
                                binary.Op == BinaryExpr.Operations.Lt ||
                                binary.Op == BinaryExpr.Operations.Gt ||
                                binary.Op == BinaryExpr.Operations.Ge ||
                                binary.Op == BinaryExpr.Operations.Le)
                            {
                                if (hasPointer && hasFloat)
                                    Output.Error(Unit.Filename, binary.Line, binary.Column,
                                        "Comparison between floating-point type and pointer");
                                else if (hasPointer && (IsIntegerType(tLeft) || IsIntegerType(tRight)))
                                    Output.Warning(Unit.Filename, binary.Line, binary.Column,
                                        "Comparison between integer and pointer");
                            }
                        }
                    }
                    break;
                case UnaryExpr unary:
                    if (ValidateExpr(unary.Expr))
                    {
                        FBType type = TypeOfExpr(unary.Expr);
                        if (unary.Op == UnaryExpr.Operations.Deref &&
                            !(type is FBPointerType) && !(type is FBArrayType))
                            Output.Error(Unit.Filename, unary.Line, unary.Column,
                                "'*' unary operator takes a pointer");
                        if (unary.Op == UnaryExpr.Operations.GetAddr
                            && !(type is FBFuncPointerType) && !IsExprLvalue(unary.Expr))
                            Output.Error(Unit.Filename, unary.Line, unary.Column,
                                "Cannot take address of rvalue");
                        if (unary.Op == UnaryExpr.Operations.Not && !IsIntegerType(type))
                            Output.Error(Unit.Filename, unary.Line, unary.Column,
                                "'Not' unary operator takes an integral type");
                        if (unary.Op == UnaryExpr.Operations.Minus && !IsNumericType(type))
                            Output.Error(Unit.Filename, unary.Line, unary.Column,
                                "'-' unary operator takes a numeric type");
                        if (unary.Op == UnaryExpr.Operations.Plus && !IsNumericType(type))
                            Output.Error(Unit.Filename, unary.Line, unary.Column,
                                "'+' unary operator takes a numeric type");
                    }
                    break;
                case CallExpr call:
                    if (ValidateExpr(call.Callee))
                    {
                        FBType tCallee = TypeOfExpr(call.Callee);
                        if (tCallee is FBFuncPointerType func)
                        {
                            bool member = func.MemberFunction;
                            int expectedArgs = func.Args.Count;
                            // Subtract the implicit 'Me' argument if it's a member function
                            if (member)
                                expectedArgs--;
                            if (call.Args.Count != expectedArgs)
                                Output.Error(Unit.Filename, call.Line, call.Column,
                                $"Callee expects {expectedArgs} argument(s) to be passed, not {call.Args.Count}");
                            // Implicit 'Me' argument must be skipped
                            int offset = member ? 1 : 0;
                            for (int i = 0; call.Args.Count > i; i++)
                            {
                                Expr arg = call.Args[i];
                                if (ValidateExpr(arg))
                                    CastabilityTest(arg, func.Args[i + offset].Type);
                            }
                        }
                        else if (tCallee is FBPointerType || tCallee is FBArrayType)
                        {
                            FBType resultType = tCallee is FBPointerType ptr ?
                                ptr.To : (tCallee as FBArrayType).Of;
                            if (resultType is FBPrimitiveType prim && prim.Type == FBPrimitiveType.Enum.Void)
                                Output.Error(Unit.Filename, call.Line, call.Column,
                                    "Attempt to index a void pointer");
                            if (call.Args.Count != 1)
                                Output.Error(Unit.Filename, call.Line, call.Column,
                                    $"Index operator takes only one index");
                            if (call.Args.Count > 0 && ValidateExpr(call.Args[0]))
                                CastabilityTest(call.Args[0], SizeType);
                        }
                        else
                            Output.Error(Unit.Filename,
                                call.Line, call.Column,
                                "Expression is not callable or indexable");
                    }
                    break;
            }
            return errors == Output.Errors;
        }

        void DoPass(Stmt stmt, bool simplification = false)
        {
            switch (stmt)
            {
                case VarStmt decl:
                    foreach (DeclaredVariable dv in decl.Declared)
                    {
                        if (funcStack.Count != 0)
                        {
                            try
                            {
                                Scope.Declare(dv.Name, dv);
                            }
                            catch (ArgumentException aex)
                            {
                                Output.Error(Unit.Filename, dv.Line, dv.Column, aex.Message);
                            }
                        }
                        if (!simplification)
                        {
                            if (dv.Initializer == null && dv.Parent.Const)
                                Output.Error(Unit.Filename, dv.Line, dv.Column,
                                    "Constant variable must have initializer");
                            if (dv.Initializer != null &&
                                ValidateExpr(dv.Initializer))
                            {
                                void WalkAndFillInArraySizes(InitializerExpr init,
                                    FBType type)
                                {
                                    if (type is FBArrayType arr)
                                        arr.Length ??= new LiteralExpr(
                                            LiteralExpr.Enum.UInteger, init.Elements.Count);
                                    StructStmt stct =
                                        type is FBNamedType named ?
                                            Scope.Get<StructStmt>(named.Name) :
                                            null;
                                    var fields = stct?.GetFields().ToList();
                                    FBType elemType = null;
                                    if (type is FBPointerType ptrType)
                                        elemType = ptrType.To;
                                    else if (type is FBArrayType arrType)
                                        elemType = arrType.Of;
                                    for (int i = 0; init.Elements.Count > i; i++)
                                    {
                                        if (fields != null)
                                            elemType = fields[i].Type;
                                        if (init.Elements[i] is InitializerExpr init2)
                                            WalkAndFillInArraySizes(init2, elemType);
                                        else
                                            CastabilityTest(init.Elements[i], elemType);
                                    }
                                }
                                if (dv.Initializer is InitializerExpr init)
                                    WalkAndFillInArraySizes(init, dv.Type);
                                else
                                    CastabilityTest(dv.Initializer, dv.Type);
                            }
                        }
                        else
                        {
                            if (dv.Initializer != null)
                            {
                                definitionStack.Clear();
                                definitionStack.Push(dv.Name);
                                dv.Initializer = SimplifyExpr(dv.Initializer);
                                definitionStack.Pop();
                                if (dv.Parent.Const && !(dv.Initializer is LiteralExpr))
                                    Output.Error(Unit.Filename, dv.Line, dv.Column,
                                        "Initializer must be compile-time value");
                            }
                        }
                    }
                    break;
                case EnumStmt @enum:
                    if (!simplification && @enum.Base != null && !IsNumericType(@enum.Base))
                        Output.Error(Unit.Filename,
                            @enum.Line, @enum.Column,
                            "Base type of enum must be numeric");
                    foreach (EnumConstant cnst in @enum.Values)
                    {
                        if (funcStack.Count != 0)
                        {
                            try
                            {
                                Scope.Declare(cnst.Name, cnst);
                            }
                            catch (ArgumentException aex)
                            {
                                Output.Error(Unit.Filename, cnst.Line, cnst.Column, aex.Message);
                            }
                        }
                        if (!simplification)
                        {
                            if (ValidateExpr(cnst.Value))
                                if (!IsNumericType(TypeOfExpr(cnst.Value)))
                                    Output.Error(Unit.Filename,
                                        cnst.Line, cnst.Column,
                                        "Enum constant must evaluate to a numeric type");
                        }
                        else
                        {
                            definitionStack.Clear();
                            definitionStack.Push(cnst.Name);
                            cnst.Value = SimplifyExpr(cnst.Value);
                            definitionStack.Pop();
                            if (!(cnst.Value is LiteralExpr))
                                Output.Error(Unit.Filename, cnst.Line, cnst.Column,
                                    "Enum constant must be a compile-time value");
                        }
                    }
                    break;
                case StructStmt @struct:
                    structNameStack.Push(@struct.Name);
                    if (funcStack.Count != 0)
                    {
                        try
                        {
                            Scope.Declare(@struct.Name, @struct);
                        }
                        catch (ArgumentException aex)
                        {
                            Output.Error(Unit.Filename, @struct.Line, @struct.Column,
                                aex.Message);
                        }
                    }
                    Scope = Scope.Down;
                    FillStructMemberScope(@struct);
                    foreach (var s in @struct.Members)
                        DoPass(s, simplification);
                    Scope = Scope.Up;
                    structNameStack.Pop();
                    break;
                case NamespaceStmt @namespace:
                    Scope = Scope.Down;
                    FillScope(@namespace.Contents.Where(x => x is DeclarativeStmt d && d.Private),
                        clear: false);
                    @namespace.Contents.ForEach(x => DoPass(x, simplification));
                    Scope = Scope.Up;
                    break;
                case ExitStmt exit when !simplification:
                    void exit_error(string err) =>
                        Output.Error(Unit.Filename,
                             exit.Line, exit.Column, err);
                    if (exit.What == ExitStmt.Enum.Do && doNest == 0)
                        exit_error("'Exit Do' not in Do loop");
                    else if (exit.What == ExitStmt.Enum.While && whileNest == 0)
                        exit_error("'Exit While' not in While loop");
                    else if (exit.What == ExitStmt.Enum.For && forNest == 0)
                        exit_error("'Exit For' not in For statement");
                    else if (exit.What == ExitStmt.Enum.Select && selectNest == 0)
                        exit_error("'Exit Select' not in Select...Case statement");
                    break;
                case ExprStmt expr:
                    if (!simplification) ValidateExpr(expr.Expr);
                    else expr.Expr = SimplifyExpr(expr.Expr);
                    break;
                case ForStmt @for:
                    forNest++;
                    if (!simplification)
                    {
                        ValidateExpr(@for.Counter.Initializer);
                        ValidateExpr(@for.Destination);
                        ValidateExpr(@for.Step);
                    }
                    else
                    {
                        @for.Counter.Initializer = SimplifyExpr(@for.Counter.Initializer);
                        @for.Destination = SimplifyExpr(@for.Destination);
                        @for.Step = SimplifyExpr(@for.Step);
                    }
                    Scope = Scope.Down;
                    // Declare counter variable
                    FBType counterType = @for.Counter.Type ??
                        TypeOfExpr(new BinaryExpr(@for.Counter.Initializer,
                        BinaryExpr.Operations.Add, @for.Step));
                    Scope.Declare(@for.Counter.Name,
                        new DeclaredVariable(null, @for.Counter.Name,
                        counterType, @for.Counter.Initializer));
                    @for.Body.ForEach(x => DoPass(x, simplification));
                    Scope = Scope.Up;
                    forNest--;
                    break;
                case AssignStmt assign:
                    if (!simplification)
                    {
                        bool lvalid = ValidateExpr(assign.Left);
                        if (lvalid && !IsExprLvalue(assign.Left, true))
                            Output.Error(Unit.Filename, assign.Line, assign.Column,
                                "Assigned expression must be an lvalue");
                        if (ValidateExpr(assign.Right) && lvalid)
                            CastabilityTest(GetAssignedValue(assign), TypeOfExpr(assign.Left));
                    }
                    else
                    {
                        assign.Left = SimplifyExpr(assign.Left);
                        assign.Right = SimplifyExpr(assign.Right);
                    }
                    break;
                case ContinueStmt @continue when !simplification:
                    if (@continue.What == ContinueStmt.Enum.Do && doNest == 0)
                        Output.Error(Unit.Filename, @continue.Line, @continue.Column,
                            "'Exit Do' not in Do loop");
                    else if (@continue.What == ContinueStmt.Enum.While && whileNest == 0)
                        Output.Error(Unit.Filename, @continue.Line, @continue.Column,
                            "'Exit While' not in While loop");
                    else if (@continue.What == ContinueStmt.Enum.For && forNest == 0)
                        Output.Error(Unit.Filename, @continue.Line, @continue.Column,
                            "'Exit For' not in For statement");
                    break;
                case FuncStmt func:
                    if (funcStack.Count != 0)
                        Output.Error(Unit.Filename, func.Line, func.Column,
                            "Local functions are currently unsupported");
                    funcStack.Push(func);
                    func.Args.ForEach(x =>
                    {
                        ValidateType(x.Type);
                        ValidateExpr(x.Initializer);
                        CastabilityTest(x.Initializer, x.Type);
                    });
                    ValidateType(func.ReturnType);
                    Scope = Scope.Down;
                    VarStmt dummyDecl = new VarStmt(null);
                    func.Args.ForEach(x =>
                        Scope.Declare(x.Name, new DeclaredVariable(
                        dummyDecl, x.Name, x.Type, x.Initializer,
                        x.Type.Line, x.Type.Column)));
                    func.Body?.ForEach(x => DoPass(x, simplification));
                    Scope = Scope.Up;

                    funcStack.Pop();
                    break;
                case IfStmt @if:
                    if (!simplification)
                    {
                        if (ValidateExpr(@if.Condition))
                            CastabilityTest(@if.Condition,
                                FBType.Prim(FBPrimitiveType.Enum.Bool),
                                @implicit: false);
                    }
                    else
                        SimplifyExpr(@if.Condition);
                    Scope = Scope.Down;
                    @if.OnTrue.ForEach(x => DoPass(x, simplification));
                    Scope = Scope.Up;

                    Scope = Scope.Down;
                    @if.OnFalse.ForEach(x => DoPass(x, simplification));
                    Scope = Scope.Up;
                    break;
                case ReturnStmt @return:
                    if (!simplification)
                    {
                        if (ValidateExpr(@return.Value))
                            CastabilityTest(@return.Value, funcStack.Peek().ReturnType);
                    }
                    else
                        @return.Value = SimplifyExpr(@return.Value);
                    break;
                case SelectStmt select:
                    Output.Error(Unit.Filename, select.Line,
                        select.Column, "Sory not suported :)");
                    break;
                case WhileStmt @while:
                    if (!simplification)
                    {
                        if (ValidateExpr(@while.Condition))
                            CastabilityTest(@while.Condition,
                                FBType.Prim(FBPrimitiveType.Enum.Bool),
                                @implicit: false);
                    }
                    else
                        @while.Condition = SimplifyExpr(@while.Condition);

                    if (@while.IsDoLoop) doNest++; else whileNest++;
                    Scope = Scope.Down;
                    @while.Body.ForEach(x => DoPass(x, simplification));

                    Scope = Scope.Up;
                    if (@while.IsDoLoop) doNest--; else whileNest--;
                    break;
            }
        }

        void PrepareAST()
        {
            FillScope(Unit.Statements);
            foreach (Stmt s in Unit.Statements) DoPass(s, simplification: false);
            if (Output.Errors == 0)
            {
                FillScope(Unit.Statements);
                // Simplify non-functions first
                foreach (Stmt s in Unit.Statements)
                    if (!(s is FuncStmt))
                        DoPass(s, simplification: true);

                foreach (Stmt s in Unit.Statements)
                    if (s is FuncStmt)
                        DoPass(s, simplification: true);
            }
        }

        public int CalculateStructOffset(StructStmt stct, string name)
        {
            int offset = 0;
            foreach (var field in stct.GetFields())
            {
                if (field.Name == name)
                    break;
                offset += SizeOfType(field.Type);
            }
            return offset;
        }

        int _nameGen = 0;

        IRName CompileString(List<IROp> ir, string str, bool wstring = false)
        {
            IRName sName = $"strc_{_nameGen++:x}";
            ir.Insert(0, new IRData(sName, new List<IRDataFragment>
            {
                new IRDataFragment(
                    wstring ?
                    IRDataFragment.Enum.WString :
                    IRDataFragment.Enum.String,
                    str)
            }));
            return sName;
        }

        IRData CompileStaticData(List<IROp> ir, Expr expr, FBType varType = null)
        {
            IRName name = $"stat_{_nameGen++:x}";
            IRDataFragment FragmentFromExpr(Expr expr, FBType typeHint = null)
            {
                var fragType = (IRDataFragment.Enum)(-1);
                if (typeHint != null)
                {
                    if (typeHint is FBPrimitiveType prim)
                    {
                        fragType = prim.Type switch
                        {
                            FBPrimitiveType.Enum.Bool => IRDataFragment.Enum.Dword,
                            FBPrimitiveType.Enum.Byte => IRDataFragment.Enum.Byte,
                            FBPrimitiveType.Enum.Char => IRDataFragment.Enum.Byte,
                            FBPrimitiveType.Enum.WChar => IRDataFragment.Enum.Word,
                            FBPrimitiveType.Enum.Short => IRDataFragment.Enum.Word,
                            FBPrimitiveType.Enum.UShort => IRDataFragment.Enum.Word,
                            FBPrimitiveType.Enum.Integer => IRDataFragment.Enum.Dword,
                            FBPrimitiveType.Enum.UInteger => IRDataFragment.Enum.Dword,
                            FBPrimitiveType.Enum.Long => IRDataFragment.Enum.Qword,
                            FBPrimitiveType.Enum.ULong => IRDataFragment.Enum.Qword,
                            FBPrimitiveType.Enum.Double => IRDataFragment.Enum.Double,
                            FBPrimitiveType.Enum.Float => IRDataFragment.Enum.Float,
                            _ => (IRDataFragment.Enum)0
                        };
                    }
                    else if (typeHint is FBPointerType || typeHint is FBFuncPointerType)
                    {
                        fragType = Platform.Current.SizeOfPtr switch
                        {
                            2 => IRDataFragment.Enum.Word,
                            4 => IRDataFragment.Enum.Dword,
                            8 => IRDataFragment.Enum.Qword,
                            _ => IRDataFragment.Enum.Dword,
                        };
                    }
                }
                if (expr is LiteralExpr lit)
                {
                    if (lit.Type == LiteralExpr.Enum.String ||
                        lit.Type == LiteralExpr.Enum.WString)
                    {
                        return new IRDataFragment((int)fragType == -1 ?
                                lit.Type == LiteralExpr.Enum.String
                                    ? IRDataFragment.Enum.String
                                    : IRDataFragment.Enum.WString
                                    : fragType,
                            new IRConstOffsetName(CompileString(
                                ir, lit.Value as string,
                                lit.Type == LiteralExpr.Enum.WString)));
                    }
                    else
                        return new IRDataFragment(fragType, (expr as LiteralExpr).Value);
                }
                else if (expr is CTypeExpr ctype)
                    return FragmentFromExpr(ctype.Casted, ctype.ToType);
                else if (expr is NameExpr name)
                {
                    return new IRDataFragment((int)fragType == -1 ? IRDataFragment.Enum.Name : fragType,
                        new IRConstOffsetName(name.Name.ToString()));
                }
                else if (expr is UnaryExpr unary &&
                        unary.Op == UnaryExpr.Operations.GetAddr)
                {
                    return new IRDataFragment((int)fragType == -1 ? IRDataFragment.Enum.Name : fragType,
                        new IRConstOffsetName((unary.Expr as NameExpr).Name.ToString()));
                }
                return null;
            }

            List<IRDataFragment> frag = new List<IRDataFragment>();

            if (expr is InitializerExpr init)
            {
                void Visit(InitializerExpr initializer, FBType type)
                {
                    StructStmt stct = type is FBNamedType named ?
                        Scope.Get<StructStmt>(named.Name) :
                        null;
                    var fields = stct?.GetFields().ToList();
                    FBType elementType = null;
                    if (type is FBPointerType ptr)
                        elementType = ptr.To;
                    else if (type is FBArrayType arr)
                        elementType = arr.Of;
                    for (int i = 0; initializer.Elements.Count > i; i++)
                    {
                        var element = initializer.Elements[i];
                        if (fields != null)
                            elementType = fields[i].Type;
                        if (element is InitializerExpr arr2)
                            Visit(arr2, elementType);
                        else
                            frag.Add(FragmentFromExpr(new CTypeExpr(element, elementType)));
                    }
                }
                Visit(init, varType);
            }
            else
                frag.Add(FragmentFromExpr(expr, varType));

            IRData data = new IRData(name, frag);
            ir.Insert(0, data);
            return data;
        }

        List<IROp> irCode = new List<IROp>();

        void CompileExpr(Expr expr, IRName dest, bool indirect = false)
        {
            switch (expr)
            {
                case InitializerExpr init:
                    currentFunction.Emit(new IRStackAllocOp(dest, SizeOfType(init.TypeHint)));
                    throw new NotImplementedException("sory not suported :(");
                case CTypeExpr ctype:
                    FBType from = TypeOfExpr(ctype.Casted);
                    IRName newTemp = GenTemp(FBTypeToIR(ctype.ToType));
                    CompileExpr(ctype.Casted, newTemp);
                    currentFunction.Emit(new IRMovOp(dest, newTemp, indirect, GetIRType(dest, indirect)));

                    if (IsIntegerType(ctype.ToType) && IsIntegerType(from) ||
                        IsAnyPointer(ctype.ToType) && IsIntegerType(from) ||
                        IsIntegerType(ctype.ToType) && IsAnyPointer(from))
                    {
                        int sa = SizeOfType(ctype.ToType);
                        int sb = SizeOfType(from);

                        if (sa < sb)
                            currentFunction.Emit(new IRThreeOp(ThreeOpType.And, dest, dest,
                                new IRPrimitiveOperand((1ul << (sa * 8)) - 1), indirect, GetIRType(dest, indirect)));
                    }
                    break;
                case UnaryExpr unary:
                    if (unary.Op == UnaryExpr.Operations.Deref)
                    {
                        IRPointerType ptr = FBTypeToIR(TypeOfExpr(unary.Expr)) as IRPointerType;
                        string ptrTemp = GenTemp(ptr);
                        CompileExpr(unary.Expr, ptrTemp);
                        currentFunction.Emit(new IRLoadIndirectOp(dest, ptr.To, (IRName)ptrTemp));
                        UpdateIRType(dest, ptr.To);
                    }
                    else if (unary.Op == UnaryExpr.Operations.Not)
                    {
                        CompileExpr(unary.Expr, dest);
                        currentFunction.Emit(new IRTwoOp(TwoOpType.Not, dest, dest, indirect, GetIRType(dest, indirect)));
                    }
                    else if (unary.Op == UnaryExpr.Operations.Minus)
                    {
                        CompileExpr(unary.Expr, dest);
                        currentFunction.Emit(new IRTwoOp(TwoOpType.Neg, dest, dest, indirect, GetIRType(dest, indirect)));
                    }
                    else if (unary.Op == UnaryExpr.Operations.Plus)
                        CompileExpr(unary.Expr, dest, indirect);
                    else if (unary.Op == UnaryExpr.Operations.SizeOf)
                        currentFunction.Emit(new IRMovOp(dest,
                            new IRPrimitiveOperand(SizeOfType(TypeOfExpr(unary.Expr))), indirect, GetIRType(dest, indirect)
                        ));
                    else if (unary.Op == UnaryExpr.Operations.GetAddr)
                    {
                        switch (unary.Expr)
                        {
                            case NameExpr name:
                                if (TypeOfExpr(name) is FBArrayType array)
                                    CompileExpr(name, dest);
                                else
                                    currentFunction.Emit(new IRAddrOfOp(dest, GetIRVarName(name.Name)));
                                break;
                            case CallExpr subscript:
                                CompileExpr(new BinaryExpr(
                                    subscript.Callee,
                                    BinaryExpr.Operations.Add,
                                    subscript.Args[0]), dest);
                                break;
                            case AccessExpr access:
                                FBType type1 = TypeOfExpr(access.Accessed);
                                FBType type2 = TypeOfExpr(access.Accessed);
                                while (type2 is FBPointerType pointer)
                                    type2 = pointer.To;
                                FBNamedType fb = type2 as FBNamedType;
                                var stct = Scope.Get<StructStmt>(fb.Name);
                                Expr @base = type1 is FBPointerType ? access.Accessed
                                    : new UnaryExpr(UnaryExpr.Operations.GetAddr, access.Accessed);
                                bool isMemberFunction = false;
                                foreach (FuncStmt func in stct.GetFunctions())
                                {
                                    if (func.Name == access.Member)
                                    {
                                        isMemberFunction = true;
                                        currentFunction.Emit(new IRMovOp(dest,
                                            new IRName($"{stct.Name}::{func.Name}"), indirect, GetIRType(dest, indirect)));
                                    }
                                }
                                if (!isMemberFunction)
                                {
                                    CompileExpr(
                                        SimplifyExpr(
                                            new BinaryExpr(
                                            // Convert pointer to void* to prevent scaling
                                            new CTypeExpr(@base,
                                                new FBPointerType( FBType.Prim(FBPrimitiveType.Enum.Void))
                                            ),
                                            BinaryExpr.Operations.Add,
                                            new LiteralExpr(LiteralExpr.Enum.UInteger,
                                                (ulong)CalculateStructOffset(stct, access.Member)))
                                        ), dest
                                    );
                                }
                                break;
                        }
                    }
                    break;
                case CallExpr call:
                    FBType retType = TypeOfExpr(call);
                    FBType calleeType = TypeOfExpr(call.Callee);
                    if (calleeType is FBArrayType || calleeType is FBPointerType)
                    {
                        Expr index = call.Args[0];
                        CompileExpr(SimplifyExpr(new UnaryExpr(
                            UnaryExpr.Operations.Deref,
                            new BinaryExpr(call.Callee, BinaryExpr.Operations.Add,
                            index))), dest, indirect);
                    }
                    else if (calleeType is FBFuncPointerType func)
                    {
                        IRName callee = GenTemp(FBTypeToIR(calleeType));
                        CompileExpr(call.Callee, callee);
                        var argTemps = new List<string>();
                        foreach (TypedEntity tEnt in func.Args)
                            argTemps.Add(GenTemp(FBTypeToIR(tEnt.Type)));
                        if (func.MemberFunction)
                            call.Args.Insert(0, new UnaryExpr(UnaryExpr.Operations.GetAddr,
                                (call.Callee as AccessExpr).Accessed));
                        for (int i = 0; call.Args.Count > i; i++)
                            CompileExpr(call.Args[i], argTemps[i]);
                        currentFunction.Emit(new IRCallOp(dest, callee,
                            argTemps.Select(x => (IROperand)(IRName)x).ToList(), indirect, GetIRType(dest, indirect)));
                    }
                    break;
                case BinaryExpr binary:
                    FBType leftType = TypeOfExpr(binary.Left);
                    FBType rightType = TypeOfExpr(binary.Right);
                    IRType irLeftType = FBTypeToIR(leftType);
                    IRType irRightType = FBTypeToIR(rightType);
                    string
                        a = GenTemp(irLeftType),
                        b = GenTemp(irRightType);
                    // Decay array type to pointer type.
                    if (leftType is FBArrayType lArr)
                        leftType = new FBPointerType(lArr.Of);
                    if (rightType is FBArrayType rArr)
                        rightType = new FBPointerType(rArr.Of);
                    // If left side is pointer, then the right side of
                    // the expression is surely an integer.
                    if (leftType is FBPointerType lptr
                        && (binary.Op == BinaryExpr.Operations.Add ||
                        binary.Op == BinaryExpr.Operations.Sub))
                    {
                        // Scale the right side by the size of the pointee
                        CompileExpr(binary.Left, a);
                        CompileExpr(
                            SimplifyExpr(
                                new BinaryExpr(
                                    binary.Right,
                                    BinaryExpr.Operations.Mul,
                                    new LiteralExpr(LiteralExpr.Enum.UInteger, (ulong)SizeOfType(lptr.To)))
                            ), b);
                    }
                    // Similarly here.
                    else if (rightType is FBPointerType rptr
                        && (binary.Op == BinaryExpr.Operations.Add ||
                        binary.Op == BinaryExpr.Operations.Sub))
                    {
                        // Scale the left side by the size of the pointee
                        CompileExpr(
                            SimplifyExpr(new BinaryExpr(
                                binary.Left,
                                BinaryExpr.Operations.Mul,
                                new LiteralExpr(LiteralExpr.Enum.UInteger, (ulong)SizeOfType(rptr.To))
                            )), a);
                        CompileExpr(binary.Right, b);
                    }
                    else
                    {
                        CompileExpr(binary.Left, a);
                        CompileExpr(binary.Right, b);
                    }

                    switch (binary.Op)
                    {
                        case BinaryExpr.Operations.Add:
                        case BinaryExpr.Operations.Sub:
                        case BinaryExpr.Operations.Mul:
                        case BinaryExpr.Operations.Div:
                        case BinaryExpr.Operations.Mod:
                        case BinaryExpr.Operations.Xor:
                        case BinaryExpr.Operations.And:
                        case BinaryExpr.Operations.Or:
                        case BinaryExpr.Operations.Shl:
                        case BinaryExpr.Operations.Shr:
                        case BinaryExpr.Operations.Rol:
                        case BinaryExpr.Operations.Ror:
                            var exprType = GetIRType(dest, indirect);

                            if ((binary.Op == BinaryExpr.Operations.Div ||
                                binary.Op == BinaryExpr.Operations.Mod) &&
                                FBTypeToIR(TypeOfExpr(binary)) is IRIntegerType integer && !integer.Signed &&
                                exprType is IRIntegerType integer2)
                                integer2.Signed = false;

                            currentFunction.Emit(new IRThreeOp(binary.Op switch
                            {
                                BinaryExpr.Operations.Add => ThreeOpType.Add,
                                BinaryExpr.Operations.Sub => ThreeOpType.Sub,
                                BinaryExpr.Operations.Mul => ThreeOpType.Mul,
                                BinaryExpr.Operations.Div => ThreeOpType.Div,
                                BinaryExpr.Operations.Mod => ThreeOpType.Mod,
                                BinaryExpr.Operations.Xor => ThreeOpType.Xor,
                                BinaryExpr.Operations.And => ThreeOpType.And,
                                BinaryExpr.Operations.Or => ThreeOpType.Or,
                                BinaryExpr.Operations.Shl => ThreeOpType.Shl,
                                BinaryExpr.Operations.Shr => ThreeOpType.Shr,
                                BinaryExpr.Operations.Rol => ThreeOpType.Rol,
                                BinaryExpr.Operations.Ror => ThreeOpType.Ror,
                                _ => (ThreeOpType)0
                            }, dest, (IRName)a, (IRName)b, indirect, exprType));
                            break;
                        case BinaryExpr.Operations.Eq:
                        case BinaryExpr.Operations.Neq:
                        case BinaryExpr.Operations.Le:
                        case BinaryExpr.Operations.Lt:
                        case BinaryExpr.Operations.Ge:
                        case BinaryExpr.Operations.Gt:
                            currentFunction.Emit(new IRCmpOp((IRName)a, (IRName)b,
                                SizeOfType(leftType) > SizeOfType(rightType) ? irLeftType : irRightType));
                            currentFunction.Emit(
                                new IRMovFlagOp(dest, binary.Op switch
                                {
                                    BinaryExpr.Operations.Eq => IRBranchOp.Enum.Zero,
                                    BinaryExpr.Operations.Neq => IRBranchOp.Enum.NotZero,
                                    BinaryExpr.Operations.Le => IRBranchOp.Enum.LessEqual,
                                    BinaryExpr.Operations.Lt => IRBranchOp.Enum.Less,
                                    BinaryExpr.Operations.Ge => IRBranchOp.Enum.GreaterEqual,
                                    BinaryExpr.Operations.Gt => IRBranchOp.Enum.Greater,
                                    _ => IRBranchOp.Enum.Always
                                }, indirect, GetIRType(dest, indirect)));
                            break;
                    }
                    break;
                case LiteralExpr lit:
                    object obj = lit.Value;
                    if (lit.Type == LiteralExpr.Enum.String ||
                        lit.Type == LiteralExpr.Enum.WString)
                    {
                        IRName data = CompileString(irCode, obj as string,
                            lit.Type == LiteralExpr.Enum.WString);
                        currentFunction.Emit(new IRMovOp(dest, data, indirect, GetIRType(dest, indirect)));
                    }
                    else
                        currentFunction.Emit(new IRMovOp(dest,
                            new IRPrimitiveOperand(obj,
                            lit.Type == LiteralExpr.Enum.WString), indirect, GetIRType(dest, indirect)));
                    break;
                case AccessExpr access:
                    FBType type = TypeOfExpr(access);
                    IRType irType = FBTypeToIR(type);
                    if (type is FBFuncPointerType)
                        CompileExpr(new UnaryExpr(UnaryExpr.Operations.GetAddr, expr), dest);
                    else
                    {
                        IRName addr = GenTemp(new IRPointerType(FBTypeToIR(type)));
                        CompileExpr(new UnaryExpr(UnaryExpr.Operations.GetAddr, expr), addr);
                        currentFunction.Emit(new IRLoadIndirectOp(dest, irType, addr));
                    }
                    UpdateIRType(dest, irType);
                    break;
                case NameExpr name:
                    ExpressionAlias ea;
                    string strName = GetIRVarName(name.Name);
                    if (strName == "me")
                        currentFunction.Emit(new IRMovOp(dest, new IRName("me"), indirect, GetIRType(dest, indirect)));
                    else if ((ea = Scope.Get<ExpressionAlias>(strName)) != null)
                        CompileExpr(ea.Expr, dest, indirect);
                    else
                        currentFunction.Emit(new IRMovOp(dest, (IRName)strName, indirect, GetIRType(dest, indirect)));
                    break;
            }
        }

        void CompileStmt(Stmt stmt, string namePrefix = "")
        {
            switch (stmt)
            {
                case IfStmt @if:
                    string condTmp = GenTemp(new IRIntegerType(true, 32));
                    CompileExpr(@if.Condition, condTmp);
                    bool hasElse = @if.OnFalse?.Count > 0;
                    string elseBranchName = condTmp.Replace("%", "loc_else_end_");
                    string endBranchName = condTmp.Replace("%", "loc_");
                    currentFunction.Emit(new IRChkOp((IRName)condTmp));
                    currentFunction.Emit(new IRBranchOp(endBranchName, IRBranchOp.Enum.Zero));
                    Scope = Scope.Down;
                    foreach (Stmt s in @if.OnTrue) CompileStmt(s);
                    if (hasElse)
                        currentFunction.Emit(new IRBranchOp(elseBranchName, IRBranchOp.Enum.Always));
                    Scope = Scope.Up;
                    currentFunction.Emit(new IRLabel(endBranchName));
                    if (hasElse)
                    {
                        Scope = Scope.Down;
                        foreach (Stmt s in @if.OnFalse) CompileStmt(s);
                        Scope = Scope.Up;
                        currentFunction.Emit(new IRLabel(elseBranchName));
                    }
                    break;
                case WhileStmt @while:
                    IRType intType = new IRIntegerType(true, 32);
                    condTmp = GenTemp(intType);
                    endBranchName = condTmp.Replace("%", "locwe_");
                    string loopName = condTmp.Replace("%", "locw_");
                    var stack = @while.IsDoLoop ? doInfoStack : whileInfoStack;
                    stack.Push((loopName, endBranchName));
                    currentFunction.Emit(new IRLabel(loopName));
                    if (!@while.CheckAfterBody)
                    {
                        CompileExpr(@while.Condition, condTmp);
                        currentFunction.Emit(new IRChkOp((IRName)condTmp));
                        currentFunction.Emit(new IRBranchOp(endBranchName, @while.Not
                            ? IRBranchOp.Enum.NotZero
                            : IRBranchOp.Enum.Zero));
                    }
                    Scope = Scope.Down;
                    foreach (Stmt s in @while.Body) CompileStmt(s);
                    Scope = Scope.Up;
                    if (!@while.CheckAfterBody)
                    {
                        currentFunction.Emit(new IRBranchOp(loopName));
                        currentFunction.Emit(new IRLabel(endBranchName));
                    }
                    else
                    {
                        CompileExpr(@while.Condition, condTmp);
                        currentFunction.Emit(new IRChkOp((IRName)condTmp));
                        currentFunction.Emit(new IRBranchOp(loopName, @while.Not
                            ? IRBranchOp.Enum.Zero
                            : IRBranchOp.Enum.NotZero));
                        currentFunction.Emit(new IRLabel(endBranchName));

                    }
                    stack.Pop();
                    break;
                case ForStmt @for:
                    condTmp = GenTemp(new IRIntegerType(true, 32));
                    DeclaredVariable dv = new DeclaredVariable(
                        null, @for.Counter.Name,
                        @for.Counter.Type, @for.Counter.Initializer);
                    Scope = Scope.Down;
                    CompileStmt(new VarStmt(new List<DeclaredVariable> { dv }));
                    endBranchName = condTmp.Replace("%", "locfe_");
                    loopName = condTmp.Replace("%", "locf_");
                    string continuePoint = condTmp.Replace("%", "locfc_");
                    forInfoStack.Push((continuePoint, endBranchName));
                    currentFunction.Emit(new IRLabel(loopName));
                    CompileExpr(new BinaryExpr(
                        new NameExpr(dv.Name),
                        @for.ComparisonOperator, @for.Destination), condTmp);
                    currentFunction.Emit(new IRChkOp((IRName)condTmp));
                    currentFunction.Emit(new IRBranchOp(endBranchName,
                        IRBranchOp.Enum.Zero));
                    foreach (Stmt s in @for.Body) CompileStmt(s);
                    currentFunction.Emit(new IRLabel(continuePoint));
                    CompileStmt(new AssignStmt(
                        new NameExpr(dv.Name), @for.Step, AssignStmt.Operations.Add));
                    Scope = Scope.Up;
                    currentFunction.Emit(new IRBranchOp(loopName));
                    currentFunction.Emit(new IRLabel(endBranchName));
                    forInfoStack.Pop();
                    break;
                case ContinueStmt @continue:
                    if (@continue.What == ContinueStmt.Enum.For)
                        currentFunction.Emit(new IRBranchOp(forInfoStack.Peek().Item1));
                    else if (@continue.What == ContinueStmt.Enum.While)
                        currentFunction.Emit(new IRBranchOp(whileInfoStack.Peek().Item1));
                    else if (@continue.What == ContinueStmt.Enum.Do)
                        currentFunction.Emit(new IRBranchOp(doInfoStack.Peek().Item1));
                    break;
                case ExitStmt exit:
                    if (exit.What == ExitStmt.Enum.For)
                        currentFunction.Emit(new IRBranchOp(forInfoStack.Peek().Item2));
                    else if (exit.What == ExitStmt.Enum.While)
                        currentFunction.Emit(new IRBranchOp(whileInfoStack.Peek().Item2));
                    else if (exit.What == ExitStmt.Enum.Do)
                        currentFunction.Emit(new IRBranchOp(doInfoStack.Peek().Item2));
                    break;
                case FuncStmt func:
                    var retType = FBTypeToIR(func.ReturnType);
                    string irName = namePrefix + func.Name;
                    irLocalTypeMap = new Dictionary<string, IRType>();
                    IRFunction irf = new IRFunction(irName, retType,
                        FBArgListToIR(func), @extern: func.Declare);
                    foreach (TypedEntity arg in func.Args)
                        DeclareIRVar(arg.Name);
                    currentFunction = irf;
                    Scope = Scope.Down;
                    VarStmt dummyDecl = new VarStmt(null);
                    func.Args.ForEach(x =>
                        Scope.Declare(x.Name, new DeclaredVariable(
                        dummyDecl, x.Name, x.Type, x.Initializer)));
                    if (func.Body != null)
                    {
                        foreach (Stmt s in func.Body)
                            CompileStmt(s);
                    }
                    Scope = Scope.Up;
                    if (irf.Operations.Count > 0 && !(irf.Operations.Last() is IRRetOp))
                        irf.Emit(new IRRetOp(retType is IRVoidType ? null : (IRPrimitiveOperand)0));
                    irCode.Add(irf);
                    currentFunction = null;
                    break;
                case VarStmt decl:
                    foreach (DeclaredVariable var in decl.Declared)
                    {
                        if (currentFunction != null)
                        {
                            Scope.Declare(var.Name, var);

                            DeclareIRVar(var.Name);
                            IRName irVarName = GetIRVarName(var.Name);

                            bool stackAllocate = true;
                            IRType irType = null;
                            int typeSize = SizeOfType(var.Type);

                            if (var.Type is FBArrayType array)
                                irType = new IRPointerType(FBTypeToIR(array.Of));
                            else if (var.Type is FBNamedType named && Scope.Get<StructStmt>(named.Name) != null)
                                irType = new IRPointerType(new IRVoidType());
                            else
                            {
                                irType = FBTypeToIR(var.Type);
                                stackAllocate = false;
                            }

                            irGlobalTypeMap[irVarName] = irType;
                            currentFunction.Emit(
                                new IRLocalOp(irVarName, irType));

                            if (var.Initializer != null)
                                CompileExpr(var.Initializer, irVarName);
                            else if (stackAllocate)
                                currentFunction.Emit(new IRStackAllocOp(irVarName, typeSize));
                        }
                        else
                        {
                            Expr expr = var.Initializer;
                            IRData data = CompileStaticData(irCode, expr, var.Type);
                            IRType irType = FBTypeToIR(var.Type);
                            irGlobalTypeMap[var.Name] = irType;
                            irCode.Insert(0, new IRGlobal(var.Name,
                                irType, (IRName)data.Name));
                        }
                    }
                    break;
                case ExprStmt expr:
                    FBType typeOf = TypeOfExpr(expr.Expr);
                    CompileExpr(expr.Expr,
                        (typeOf is FBPrimitiveType prim &&
                        prim.Type == FBPrimitiveType.Enum.Void) ? GenTemp(new IRIntegerType(true, 32))
                        : GenTemp(FBTypeToIR(typeOf)));
                    break;
                case ReturnStmt ret:
                    string retName = GenTemp(currentFunction.ReturnType);
                    CompileExpr(ret.Value, retName);
                    currentFunction.Emit(new IRRetOp((IRName)retName));
                    break;
                case AssignStmt assign:
                    if (assign.Left is NameExpr ne)
                    {
                        if (Scope.Get<ExpressionAlias>(ne.Name) is ExpressionAlias alias)
                            CompileStmt(new AssignStmt(alias.Expr, assign.Right, assign.Op),
                                namePrefix);
                        else
                        {
                            Expr value = assign.Op == AssignStmt.Operations.None ? assign.Right :
                          new BinaryExpr(assign.Left, assign.Op switch
                          {
                              AssignStmt.Operations.Add => BinaryExpr.Operations.Add,
                              AssignStmt.Operations.Sub => BinaryExpr.Operations.Sub,
                              AssignStmt.Operations.Mul => BinaryExpr.Operations.Mul,
                              AssignStmt.Operations.Div => BinaryExpr.Operations.Div,
                              AssignStmt.Operations.Rol => BinaryExpr.Operations.Rol,
                              AssignStmt.Operations.Ror => BinaryExpr.Operations.Ror,
                              AssignStmt.Operations.Shl => BinaryExpr.Operations.Shl,
                              AssignStmt.Operations.Shr => BinaryExpr.Operations.Shr,
                              AssignStmt.Operations.Xor => BinaryExpr.Operations.Xor,
                              _ => (BinaryExpr.Operations)0
                          }, assign.Right);
                            string name = (IRName)GetIRVarName(ne.Name);
                            CompileExpr(new CTypeExpr(value, Scope.Get<DeclaredVariable>(ne.Name).Type), name);
                        }
                    }
                    else if (assign.Left is AccessExpr access)
                    {
                        FBType atype = TypeOfExpr(access);
                        IRType irAtype = FBTypeToIR(atype);
                        IRName ptr = GenTemp(new IRPointerType(irAtype));
                        var unary = new UnaryExpr(UnaryExpr.Operations.GetAddr, assign.Left);
                        CompileExpr(unary, ptr);
                        if (assign.Op == AssignStmt.Operations.None)
                            CompileExpr(new CTypeExpr(assign.Right, atype), ptr, indirect: true);
                        else
                        {
                            /*CompileExpr(new CTypeExpr(new BinaryExpr(assign.Left, assign.Op switch
                            {
                                AssignStmt.Operations.Add => BinaryExpr.Operations.Add,
                                AssignStmt.Operations.Sub => BinaryExpr.Operations.Sub,
                                AssignStmt.Operations.Mul => BinaryExpr.Operations.Mul,
                                AssignStmt.Operations.Div => BinaryExpr.Operations.Div,
                                AssignStmt.Operations.Rol => BinaryExpr.Operations.Rol,
                                AssignStmt.Operations.Ror => BinaryExpr.Operations.Ror,
                                AssignStmt.Operations.Shl => BinaryExpr.Operations.Shl,
                                AssignStmt.Operations.Shr => BinaryExpr.Operations.Shr,
                                _ => (BinaryExpr.Operations)0
                            }, assign.Right), atype), ptr, indirect: true);*/

                            IRName leftTemp = GenTemp(irAtype);
                            IRName rightTemp = GenTemp(irAtype);
                            currentFunction.Emit(new IRLoadIndirectOp(leftTemp, irAtype, ptr));
                            CompileExpr(assign.Right, rightTemp);
                            currentFunction.Emit(new IRThreeOp(
                                assign.Op switch
                                {
                                    AssignStmt.Operations.Add => ThreeOpType.Add,
                                    AssignStmt.Operations.Sub => ThreeOpType.Sub,
                                    AssignStmt.Operations.Mul => ThreeOpType.Mul,
                                    AssignStmt.Operations.Div => ThreeOpType.Div,
                                    AssignStmt.Operations.Rol => ThreeOpType.Rol,
                                    AssignStmt.Operations.Ror => ThreeOpType.Ror,
                                    AssignStmt.Operations.Shl => ThreeOpType.Shl,
                                    AssignStmt.Operations.Shr => ThreeOpType.Shr,
                                    AssignStmt.Operations.Xor => ThreeOpType.Xor,
                                    _ => (ThreeOpType)0
                                },
                                ptr, leftTemp, rightTemp, true, (irAtype as IRPointerType).To));
                        }
                    }
                    else if (assign.Left is UnaryExpr ue && ue.Op == UnaryExpr.Operations.Deref)
                    {
                        FBType atype = TypeOfExpr(ue.Expr);
                        IRType irAType = FBTypeToIR(atype);
                        IRName tempPtr = GenTemp(irAType);
                        CompileExpr(ue.Expr, tempPtr);
                        if (assign.Op == AssignStmt.Operations.None)
                            CompileExpr(assign.Right, tempPtr, true);
                        else
                        {
                            /*CompileExpr(new CTypeExpr(new BinaryExpr(assign.Left, assign.Op switch
                            {
                                AssignStmt.Operations.Add => BinaryExpr.Operations.Add,
                                AssignStmt.Operations.Sub => BinaryExpr.Operations.Sub,
                                AssignStmt.Operations.Mul => BinaryExpr.Operations.Mul,
                                AssignStmt.Operations.Div => BinaryExpr.Operations.Div,
                                AssignStmt.Operations.Rol => BinaryExpr.Operations.Rol,
                                AssignStmt.Operations.Ror => BinaryExpr.Operations.Ror,
                                AssignStmt.Operations.Shl => BinaryExpr.Operations.Shl,
                                AssignStmt.Operations.Shr => BinaryExpr.Operations.Shr,
                                _ => (BinaryExpr.Operations)0
                            }, assign.Right), atype), tempPtr, indirect: true);*/

                            IRType irType = FBTypeToIR(TypeOfExpr(assign.Left));
                            IRName leftTemp = GenTemp(irType);
                            IRName rightTemp = GenTemp(irType);
                            currentFunction.Emit(new IRLoadIndirectOp(leftTemp, irType, tempPtr));
                            CompileExpr(assign.Right, rightTemp);
                            currentFunction.Emit(new IRThreeOp(
                                assign.Op switch
                                {
                                    AssignStmt.Operations.Add => ThreeOpType.Add,
                                    AssignStmt.Operations.Sub => ThreeOpType.Sub,
                                    AssignStmt.Operations.Mul => ThreeOpType.Mul,
                                    AssignStmt.Operations.Div => ThreeOpType.Div,
                                    AssignStmt.Operations.Rol => ThreeOpType.Rol,
                                    AssignStmt.Operations.Ror => ThreeOpType.Ror,
                                    AssignStmt.Operations.Shl => ThreeOpType.Shl,
                                    AssignStmt.Operations.Shr => ThreeOpType.Shr,
                                    AssignStmt.Operations.Xor => ThreeOpType.Xor,
                                    _ => (ThreeOpType)0
                                },
                                tempPtr, leftTemp, rightTemp, true, (irAType as IRPointerType).To));
                        }
                    }
                    else if (assign.Left is CallExpr subscript)
                    {
                        Expr index = subscript.Args[0];
                        CompileStmt(new AssignStmt(
                            new UnaryExpr(UnaryExpr.Operations.Deref,
                            new BinaryExpr(subscript.Callee,
                            BinaryExpr.Operations.Add, index)), assign.Right, assign.Op));
                    }
                    break;
                case StructStmt stct:
                    Scope = Scope.Down;
                    FillStructMemberScope(stct, generating: true);
                    foreach (Stmt s in stct.GetFunctions())
                        CompileStmt(s, namePrefix: namePrefix + $"{stct.Name}::");
                    Scope = Scope.Up;
                    break;
                case NamespaceStmt nspace:
                    Scope = Scope.Down;
                    FillScope(nspace.Contents.Where(x => x is DeclarativeStmt d && d.Private),
                        clear: false);
                    foreach (Stmt s in nspace.Contents)
                        CompileStmt(s, namePrefix: namePrefix + $"{nspace.Name}::");
                    Scope = Scope.Up;
                    break;
            }
        }

        public IRProgram Translate()
        {
            irCode = new List<IROp>();
            Scope = new Scope();
            PrepareAST();
            if (Output.Errors == 0)
            {
                // From this point on, we can be 100% sure (right?..) that
                // what we're compiling will not cause an exception in the IR generator.
                foreach (Stmt stmt in Unit.Statements)
                    CompileStmt(stmt);
                return new IRProgram { Body = irCode };
            }
            return null;
        }

        void FillScope(IEnumerable<Stmt> levelTree, string namePrefix = "", bool clear = true)
        {
            if (clear) Scope = new Scope();
            foreach (Stmt stmt in levelTree)
            {
                switch (stmt)
                {
                    case NamespaceStmt nspace:
                        FillScope(nspace.Contents, $"{namePrefix}{nspace.Name}::", clear: false);
                        break;
                    case VarStmt decl:
                        foreach (var dv in decl.Declared)
                        {
                            try
                            {
                                Scope.Declare(namePrefix + dv.Name, dv);
                            }
                            catch (ArgumentException aex)
                            {
                                Output.Error(Unit.Filename,
                                    dv.Line, dv.Column, aex.Message);
                            }
                        }
                        break;
                    case FuncStmt func:
                        try
                        {
                            Scope.Declare(namePrefix + func.Name, func);
                        }
                        catch (ArgumentException aex)
                        {
                            Output.Error(Unit.Filename, func.Line, func.Column, aex.Message);
                        }
                        break;
                    case StructStmt stct:
                        try
                        {
                            Scope.Declare(namePrefix + stct.Name, stct);
                            FillScope(stct.Members.Where(x =>
                                !(x is VarStmt decl && (!decl.Const || !decl.Static)) &&
                                !(x is FuncStmt func && (func.Private || !func.Static))),
                                $"{namePrefix}{stct.Name}::", clear: false);
                        }
                        catch (ArgumentException aex)
                        {
                            Output.Error(Unit.Filename, stct.Line, stct.Column, aex.Message);
                        }
                        break;
                    case EnumStmt @enum:
                        @enum.Values.ForEach(x =>
                        {
                            try
                            {
                                Scope.Declare(@enum.Name != null
                                    ? $"{namePrefix}{@enum.Name}::{x.Name}"
                                    : $"{namePrefix}{x.Name}", x);
                            }
                            catch (ArgumentException aex)
                            {
                                Output.Error(Unit.Filename, x.Line, x.Column, aex.Message);
                            }
                        });
                        break;
                }
            }
        }
    }
}
using firebasic.AST;
using System.Collections.Generic;
using System.Linq;

namespace firebasic
{
    public class Parser
    {
        public List<Token> Input { get; set; }
        public int Pos { get; set; }
        public int Line { get; set; }
        public int Col { get; set; }
        public string Filename { get; set; }

        (int p, int l, int c) State => (Pos, Line, Col);
        void SetState((int p, int l, int c) state) => (Pos, Line, Col) = state;

        public Parser(string file, List<Token> lexed)
        {
            Input = lexed;
            Filename = file;
        }

        bool End => Pos >= Input.Count || Pos < 0;
        Token Current => End ? null : Input[Pos];
        Token Previous => Pos == 0 ? null : Input[Pos - 1];
        Token Next => Pos+1 >= Input.Count ? null : Input[Pos + 1];

        void Error(string msg, int l = -1, int c = -1)
        {
            Output.Error(Filename, l == -1 ? Line : l, c == -1 ? Col : c, msg);
        }

        Token Advance()
        {
            if (End) return null;
            Token c = Current;
            (Line, Col) = (c.Line, c.Column);
            Pos++;
            return c;
        }

        Token Word(string name = null, bool advance = true)
        {
            Token c = Current;
            if (c && (c.Type == TokenType.Name || c.Type == TokenType.Keyword)
                && (name == null || (c.Value as string) == name))
                return advance ? Advance() : c;
            return null;
        }

        Token Punct(string name = null, bool advance = true)
        {
            Token c = Current;
            if (c && (c.Type == TokenType.Punctuator)
                && (name == null || (c.Value as string) == name))
                return advance ? Advance() : c;
            return null;
        }

        Token OfType(TokenType type, bool advance = true)
        {
            Token c = Current;
            if (c && c.Type == type)
                return advance ? Advance() : c;
            return null;
        }

        void Newline()
        {
            if (!End && !OfType(TokenType.Newline))
            {
                Error("Expected newline");
                SkipUntilNewline();
            }
        }

        Name FullName()
        {
            Name name = null;
            do
            {
                Token t = OfType(TokenType.Name);
                if (t) name = new Name(t.Value as string, name);
                else Error("Expected identifier after '::'");
            }
            while (Punct("::"));
            return name;
        }

        FBType ParseType(bool validation = true)
        {
            int line = Line, col = Col;
            FBType type;
            if (OfType(TokenType.Name, false))
                type = new FBNamedType(FullName(), line, col);
            else if (Word("integer"))
                type = new FBPrimitiveType(FBPrimitiveType.Enum.Integer, line, col);
            else if (Word("string"))
                type = new FBNamedType("string", line, col);
            else if (Word("boolean"))
                type = new FBPrimitiveType(FBPrimitiveType.Enum.Bool, line, col);
            else if (Word("uinteger"))
                type = new FBPrimitiveType(FBPrimitiveType.Enum.UInteger, line, col);
            else if (Word("size"))
                type = new FBPrimitiveType(FBPrimitiveType.Enum.UInteger, line, col);
            else if (Word("float"))
                type = new FBPrimitiveType(FBPrimitiveType.Enum.Float, line, col);
            else if (Word("double"))
                type = new FBPrimitiveType(FBPrimitiveType.Enum.Double, line, col);
            else if (Word("sub"))
            {
                var args = new List<TypedEntity>();
                if (Punct("("))
                {
                    args = TypedEntityGroups(parameters: true);
                    if (!Punct(")")) Error("Expected ')' after parameter list");
                }
                if (!Punct(")")) Error("Expected ')'");
                return new FBFuncPointerType(FBType.Prim(FBPrimitiveType.Enum.Void), args,
                    line, col);
            }
            else if (Word("ulong"))
                type = new FBPrimitiveType(FBPrimitiveType.Enum.ULong, line, col);
            else if (Word("long"))
                type = new FBPrimitiveType(FBPrimitiveType.Enum.Long, line, col);
            else if (Word("char"))
                type = new FBPrimitiveType(FBPrimitiveType.Enum.Char, line, col);
            else if (Word("short"))
                type = new FBPrimitiveType(FBPrimitiveType.Enum.Short, line, col);
            else if (Word("ushort"))
                type = new FBPrimitiveType(FBPrimitiveType.Enum.UShort, line, col);
            else if (Word("char"))
                type = new FBPrimitiveType(FBPrimitiveType.Enum.Char, line, col);
            else if (Word("byte"))
                type = new FBPrimitiveType(FBPrimitiveType.Enum.Byte, line, col);
            else if (Word("wchar"))
                type = new FBPrimitiveType(FBPrimitiveType.Enum.WChar, line, col);
            else if (Word("wstring"))
                type = new FBNamedType("wstring", line, col);
            else if (Word("void"))
                type = new FBPrimitiveType(FBPrimitiveType.Enum.Void, line, col);
            else
                return null;

            for (;;)
            {
                if (Punct("("))
                {
                    Expr len = Expr();
                    if (!Punct(")"))
                        Error("Expected ')' after length");
                    type = new FBArrayType(type, len, line, col);
                }
                else if (Punct("*"))
                    type = new FBPointerType(type, line, col);
                else if (Word("function"))
                {
                    var args = new List<TypedEntity>();
                    if (Punct("("))
                    {
                        CheckForArgValidity(args = TypedEntityGroups(parameters: true, allowMe: true));
                        if (!Punct(")")) Error("Expected ')' after parameter list");
                    }
                    type = new FBFuncPointerType(type, args, line, col);
                }
                else if (Word("sub"))
                {
                    var args = new List<TypedEntity>();
                    if (Punct("("))
                    {
                        args = TypedEntityGroups(parameters: true);
                        if (!Punct(")")) Error("Expected ')' after parameter list");
                    }
                    if (!Punct(")")) Error("Expected ')'");
                    type = new FBFuncPointerType(FBType.Prim(FBPrimitiveType.Enum.Void), args,
                        line, col);
                }
                else
                    break;
            }

            void ValidateType(FBType tp, bool allowPrimVoid = false)
            {
                if (tp is FBPointerType ptr) ValidateType(ptr.To, true);
                else if (tp is FBArrayType arr) ValidateType(arr.Of);
                else if (!allowPrimVoid &&
                    tp is FBPrimitiveType prim &&
                    prim.Type == FBPrimitiveType.Enum.Void)
                    Error("'Void' type without an asterisk");
            }

            if (validation)
                ValidateType(type);

            return type;
        }

        void BodyAndEnd(List<Stmt> list, string stmtName, string endName = null, bool reqNewline = true)
        {
            for (;;)
            {
                if (End)
                {
                    Error($"Unterminated '{stmtName}' statement");
                    break;
                }
                SkipNewlines();
                var state = State;
                if (endName == null)
                {
                    if (Word("end"))
                    {
                        if (!Word(stmtName))
                            SetState(state);
                        else
                        {
                            if (reqNewline) Newline();
                            break;
                        }
                    }
                }
                else if (Word(endName))
                {
                    if (reqNewline) Newline();
                    break;
                }
                Stmt s = Stmt();
                list.Add(s);
            }
        }

        List<TypedEntity> TypedEntityGroup(FBType defaultType = null,
            bool requireInitializer = false,
            bool allowInitializer = true,
            int maxEntities = int.MaxValue,
            bool isForInitializer = false,
            bool allowMe = false)
        {
            FBType type = null;
            int line = Line, col = Col;
            var ret = new List<TypedEntity>();
            bool first = true;
            do
            {
                Token name;
                if (allowMe && Word("me"))
                {
                    name = Previous;
                    if (!first)
                        Error("'Me' argument must be first in comma-separated argument list");
                }
                else
                    name = OfType(TokenType.Name);
                if (!name)
                    Error("Expected identifier here");
                else
                    ret.Add(new TypedEntity(name.Value as string, null, null,
                        line: line, col: col));
                first = false;
            }
            while (Punct(","));

            if (!Word("as"))
            {
                if (defaultType == null)
                    Error("Expected 'As' keyword after identifier list");
            }
            else
            {
                type = ParseType();
                if (type == null)
                {
                    Error("Expected typename after 'As'");
                    type = defaultType;
                }
            }

            foreach (var te in ret)
                te.Type = type;

            if (!Punct("="))
            {
                if (requireInitializer && ret.Count == 1)
                    Error("Expected initializer");
            }
            else
            {
                Expr init = isForInitializer ? Shift() : Expr();
                if (init == null)
                {
                    if (ret.Count == 1)
                        Error("Expected expression after '='");
                }
                else if (ret.Count > 0)
                {
                    if (ret.Count > 1) Error("Variable/parameter group cannot have a single initializer");
                    ret[0].Initializer = init;
                }

                if (!allowInitializer)
                    Error("Initializer not allowed here");
            }

            if (ret.Count > maxEntities)
                Error(maxEntities == 1
                    ? $"Only 1 variable/parameter allowed here"
                    : $"Only {maxEntities} variables/parameters allowed here");

            return ret;
        }

        void SkipUntilNewline()
        {
            while (!End && !OfType(TokenType.Newline)) Advance();
        }

        bool inFunction = false;

        List<TypedEntity> TypedEntityGroups(bool parameters = false, bool allowMe = false)
        {
            bool first = true;
            var result = new List<TypedEntity>();
            do
            {
                if (parameters ? Punct(")", false) : OfType(TokenType.Newline, false))
                {
                    if (!first)
                        Error("Expected identifier after ','");
                    break;
                }
                result.AddRange(TypedEntityGroup(requireInitializer: false,
                    allowMe: allowMe));
                first = false;
                allowMe = false;
            }
            while (Punct(","));
            return result;
        }

        void CheckForArgValidity(List<TypedEntity> args)
        {
            bool gotDefault = false;
            foreach (TypedEntity arg in args)
            {
                if (gotDefault && arg.Initializer == null)
                {
                    Error("Arguments with default values must be the last in a function",
                        arg.Line, arg.Column);
                    break;
                }
                if (arg.Initializer != null) gotDefault = true;
            }
        }

        // The statement might be preceded by 'Private'
        // so the function takes an additional position
        // parameter to pass to the constructor.
        FuncStmt Function(int line, int col, bool @private = false, bool @static = false)
        {
            string lib = null;
            bool needBody = !Word("declare");
            bool sub = Word("sub");
            if (!sub) Word("function");
            if (Word("lib"))
            {
                if (needBody) Error("Function has 'Lib' specifier despite having body");
                Token libToken = OfType(TokenType.String);
                if (!libToken)
                    Error("Expected library name after 'Lib'");
                else
                    lib = libToken.Value as string;
            }
            FBType retType = FBType.Prim(FBPrimitiveType.Enum.Void);
            Token name = OfType(TokenType.Name);
            var body = needBody ? new List<Stmt>() : null;
            var args = new List<TypedEntity>();
            if (!name) Error("Expected function name");
            if (Punct("("))
            {
                args = TypedEntityGroups(parameters: true);
                if (!Punct(")")) Error("Expected ')' after parameter list");
            }
            CheckForArgValidity(args);
            if (!Word("as"))
            {
                if (!sub)
                    Error("Expected return type of function");
            }
            else
            {
                if (sub) Error("Subroutine can't have a return type");
                retType = ParseType();
                if (retType == null && !sub)
                    Error("Expected function return type after 'As'");
            }
            Newline();
            inFunction = true;
            if (needBody) BodyAndEnd(body, sub ? "sub" : "function");
            inFunction = false;
            return new FuncStmt(name.Value as string, retType, args,
                body, @private, @static, lib, !needBody, null, line, col);
        }

        void SkipNewlines()
        {
            while (OfType(TokenType.Newline));
        }

        Stmt Stmt()
        {
start:      SkipNewlines();
            Token t = Word("private"), u;
            Token stat = Word("static");
            if (inFunction && t)
                Error("Unexpected 'Private' keyword");
            if (inFunction && stat)
                Error("Unexpected 'Static' keyword");

            var (line, col) =
                t ? (t.Line, t.Column) :
                stat ? (stat.Line, stat.Column) :
                !End ? (Current.Line, Current.Column) : (0, 0);
            if (Word("sub", false) || Word("function", false) || Word("declare", false))
                return Function(line, col, t != null, stat != null);
            else
            {
                if (stat)
                    Error("Unexpected 'Static' keyword");
                if (Word("structure"))
                {
                    Token name = OfType(TokenType.Name);
                    if (!name) Error("Expected structure name");
                    Newline();
                    var members = new List<Stmt>();
                    var stct = new StructStmt(name?.Value as string, members, t != null, line, col);
                    SkipNewlines();
                    while (!Word("end"))
                    {
                        if (End) { Error("Unterminated 'Structure' statement"); break; }
                        SkipNewlines();
                        Stmt member = Stmt();
                        if (member is FuncStmt func)
                            func.MemberOf = stct;
                        if (!(member is DeclarativeStmt))
                            Error("Expected declaration in structure");
                        members.Add(member);
                    }
                    if (!Word("structure")) Error("Expected 'Structure' after 'End'");
                    return stct;
                }
                else if (Word("enum"))
                {
                    var values = new List<EnumConstant>();
                    Token name = OfType(TokenType.Name);
                    EnumStmt @enum = new EnumStmt(null, name?.Value as string,
                        values, t != null, line, col);
                    if (Word("as") && (@enum.Base = ParseType()) == null)
                        Error("Expected typename after 'As'");
                    @enum.Base ??= new FBPrimitiveType(FBPrimitiveType.Enum.Integer,
                        @enum.Line, @enum.Column);
                    Newline();
                    SkipNewlines();
                    ulong counter = 0;
                    Expr valExpr = null;
                    while (!Word("end"))
                    {
                        if (End) { Error("Unterminated 'Enum' statement"); break; }
                        SkipNewlines();
                        Token valName = OfType(TokenType.Name);
                        if (!valName) Error("Expected value name");
                        else
                        {
                            Expr newExpr = null;
                            if (Punct("=") && (newExpr = Expr()) == null)
                                Error("Expected value after '='");
                            if (newExpr != null) { valExpr = newExpr; counter = 0; }
                            Expr finalExpr = null;
                            if (counter == 0)
                                finalExpr = valExpr ?? new LiteralExpr(LiteralExpr.Enum.Integer, 0UL,
                                    valName.Line, valName.Column);
                            else
                                finalExpr = valExpr == null
                                    ? (Expr)new LiteralExpr(LiteralExpr.Enum.Integer, counter,
                                    valName.Line, valName.Column)
                                    : new BinaryExpr(valExpr, BinaryExpr.Operations.Add,
                                        new LiteralExpr(LiteralExpr.Enum.Integer, counter,
                                            valName.Line, valName.Column),
                                        valName.Line, valName.Column);
                            values.Add(
                                new EnumConstant(
                                    @enum,
                                    valName.Value as string,
                                    finalExpr,
                                    valName.Line, valName.Column
                                )
                            );
                            counter++;
                        }
                        Newline();
                    }
                    if (!Word("enum")) Error("Expected 'Enum' after 'End'");
                    return @enum;
                }
                else if (u = Word("namespace"))
                {
                    Name name = null;
                    bool check = OfType(TokenType.Name, false);
                    if (!check) Error("Expected namespace name");
                    else name = FullName();
                    Newline();
                    var stmts = new List<Stmt>();
                    while (!Word("end"))
                    {
                        if (End) { Error("Unterminated 'Structure' statement"); break; }
                        stmts.Add(Stmt());
                    }
                    if (!Word("namespace")) Error("Expected 'Structure' after 'End'");
                    return new NamespaceStmt(name, stmts, t != null, u.Line, u.Column);
                }
                else if (Word("const", false) ||
                    (inFunction ? Word("dim") : OfType(TokenType.Name, false)))
                {
                    bool @const = Word("const");
                    if (!@const && inFunction) Word("dim");
                    var vars = TypedEntityGroups(parameters: false);
                    Newline();
                    if (vars.Count == 0)
                        Error("Expected variable name");
                    var ds = new VarStmt(null, @const, @private: t != null, line: line, col: col);
                    var dv = vars.Select(x =>
                        new DeclaredVariable(ds, x.Name, x.Type,
                        x.Initializer, line: x.Line, col: x.Column)).ToList();
                    ds.Declared = dv;
                    return ds;
                }
            }
            
            if (t = Word("for"))
            {
                if (!inFunction)
                {
                    Error("'For' statement not in function");
                    SkipUntilNewline();
                    goto start;
                }
                var list = TypedEntityGroup(FBType.Prim(FBPrimitiveType.Enum.Void),
                    true, true, 1, isForInitializer: true);
                list[0].Type ??= new FBPrimitiveType(FBPrimitiveType.Enum.Integer);
                var body = new List<Stmt>();
                var stmt = new ForStmt(list.Count > 0 ? list[0] : null,
                    0, null, null, body, t.Line, t.Column);
                if (!Punct(">", false) && !Punct("<", false) &&
                    !Punct(">=", false) && !Punct("<=", false))
                    Error("Expected '>'/'<'/'>='/'<='");
                else
                {
                    stmt.ComparisonOperator = Advance().Value as string switch
                    {
                        ">" => BinaryExpr.Operations.Gt,
                        "<" => BinaryExpr.Operations.Lt,
                        ">=" => BinaryExpr.Operations.Ge,
                        "<=" => BinaryExpr.Operations.Le,
                        _ => (BinaryExpr.Operations)0
                    };
                    Expr dest = Expr();
                    if (dest == null)
                        Error("Expected expression after operator");
                    stmt.Destination = dest;
                }
                if (Word("step"))
                {
                    Expr step = Expr();
                    if (step == null)
                        Error("Expected expression after 'Step'");
                    stmt.Step = step;
                }
                // Step is CInt(1) by default
                stmt.Step ??= new LiteralExpr(LiteralExpr.Enum.Integer, 1L);
                Newline();
                BodyAndEnd(body, "for", "next");
                return stmt;
            }
            else if (t = Word("end", false))
            {
                if (!inFunction)
                {
                    Error("'End' statement not in function");
                    SkipUntilNewline();
                    goto start;
                }

                if (Next && Next.Type != TokenType.Newline)
                {
                    Error("Newline expected after 'End'");
                    SkipUntilNewline();
                }
                else
                {
                    Advance();
                    Newline();
                }
                return new ExitStmt(ExitStmt.Enum.Program, t.Line, t.Column);
            }
            else if (t = Word("exit"))
            {
                if (!inFunction)
                {
                    Error("'Exit' statement not in function");
                    SkipUntilNewline();
                    goto start;
                }
                if (Word("do") || Word("for") || Word("function") || Word("select")
                    || Word("sub") || Word("while"))
                {
                    Token kw = Previous;
                    Newline();
                    return kw.Value switch
                    {
                        "do" => new ExitStmt(ExitStmt.Enum.Do, t.Line, t.Column),
                        "for" => new ExitStmt(ExitStmt.Enum.For, t.Line, t.Column),
                        "select" => new ExitStmt(ExitStmt.Enum.Select, t.Line, t.Column),
                        "while" => new ExitStmt(ExitStmt.Enum.While, t.Line, t.Column),
                        _ => null,
                    };
                }
                Error("Expected keyword after 'Exit'");
                Newline();
            }
            else if (t = Word("return"))
            {
                if (!inFunction)
                {
                    Error("'Return' statement not in function");
                    SkipUntilNewline();
                    goto start;
                }

                Expr val = Expr();
                Newline();
                return new ReturnStmt(val, line, col);
            }
            else if (t = Word("continue"))
            {
                if (!inFunction)
                {
                    Error("'Continue' statement not in function");
                    SkipUntilNewline();
                    goto start;
                }

                if (Word("while"))
                {
                    Newline();
                    return new ContinueStmt(ContinueStmt.Enum.While, t.Line, t.Column);
                }
                else if (Word("for"))
                {
                    Newline();
                    return new ContinueStmt(ContinueStmt.Enum.For, t.Line, t.Column);
                }
                else if (Word("do"))
                {
                    Newline();
                    return new ContinueStmt(ContinueStmt.Enum.Do, t.Line, t.Column);
                }
                Error("Expected keyword after 'Continue'");
                Newline();
            }
            else if (t = Word("if"))
            {
                if (!inFunction)
                {
                    Error("'If' statement not in function");
                    SkipUntilNewline();
                    goto start;
                }

                List<Stmt> bodyIf = new List<Stmt>(),
                    bodyElse = new List<Stmt>();
                Expr condition = Expr();
                if (condition == null)
                    Error("Expected 'If' statement condition");
                if (!Word("then"))
                    Error("Expected 'Then'");
                if (!OfType(TokenType.Newline))
                {
                    Stmt s = Stmt();
                    if (s == null)
                        Error("Expected 'If' body");
                    bodyIf.Add(s);
                }
                else
                {
                    for (;;)
                    {
                        if (End)
                        {
                            Error("Unterminated 'If' statement");
                            break;
                        }
                        Stmt s = Stmt();
                        bodyIf.Add(s);
                        var state = State;
                        if (Word("end"))
                        {
                            if (!Word("if"))
                                SetState(state);
                            else
                            {
                                Newline();
                                break;
                            }
                        }
                        else if (Word("else", false) || Word("elseif", false))
                            break;
                    }
                }

                if (Word("else"))
                {
                    if (!OfType(TokenType.Newline))
                    {
                        Stmt s = Stmt();
                        if (s == null)
                            Error("Expected 'Else' body");
                        bodyElse.Add(s);
                    }
                    else
                        BodyAndEnd(bodyElse, "if");
                }
                else if (Word("elseif", false))
                {
                    // 'Tis but a scratch
                    Token kw = Input[Pos];
                    kw.Value = "if";
                    bodyElse.Add(Stmt());
                    kw.Value = "elseif";
                }
                return new IfStmt(condition, bodyIf, bodyElse, t.Line, t.Column);
            }
            else if (t = Word("while"))
            {
                if (!inFunction)
                {
                    Error("'End' statement not in function");
                    SkipUntilNewline();
                    goto start;
                }

                Expr condition = Expr();
                if (condition == null)
                    Error("Expected 'While' condition");
                Newline();
                var body = new List<Stmt>();
                BodyAndEnd(body, "while");
                return new WhileStmt(condition, body, line: t.Line, col: t.Column);
            }
            else if (t = Word("select"))
            {
                var cases = new Dictionary<Expr, List<Stmt>>();
                Word("case");
                Expr selected = Expr();
                if (selected == null) Error("Expected expression");
                Newline();
                bool oneBlock()
                {
                    bool metCaseElse = false;
                    SkipNewlines();
                    if (Word("end"))
                    {
                        if (!Word("select"))
                            Error("Expected 'Select' after 'End'");
                        Newline();
                        return false;
                    }
                    else if (!Word("case"))
                    {
                        Error($"Unexpected '{Current.Value}' token");
                        Advance();
                        return true;
                    }
                    else
                    {
                        var values = new List<Expr>();
                        do
                        {
                            Token @else = Word("else");
                            if (metCaseElse && @else)
                                Error("Select...Case statement can only have one 'Else' label");
                            Expr expr = (metCaseElse = @else) ?
                                new SelectCaseElseExpr(@else.Line, @else.Column) : RangeExpr();
                            if (expr == null)
                                Error("Expected expression");
                            else
                                values.Add(expr);
                        }
                        while (Punct(","));
                        Newline();
                        var stmts = new List<Stmt>();
                        bool metEnd = false;
                        while (!Word("case", false))
                        {
                            if (Word("end", false) && Next && Next.Type == TokenType.Keyword
                                && Next.Value as string == "select")
                            {
                                Advance();
                                Advance();
                                Newline();
                                metEnd = true;
                                break;
                            }
                            stmts.Add(Stmt());
                        }
                        foreach (Expr e in values)
                            cases[e] = stmts;
                        return !metEnd;
                    }
                }
                while (oneBlock()) ;
                return new SelectStmt(selected, cases, t.Line, t.Column);
            }
            else if (t = Word("do"))
            {
                if (!inFunction)
                {
                    Error("'Do' statement not in function");
                    SkipUntilNewline();
                    goto start;
                }

                var body = new List<Stmt>();
                Expr condition = null;
                bool not = false, checkAfter = false;
                if (!OfType(TokenType.Newline))
                {
                    not = Word("until");
                    if (!not && !OfType(TokenType.Newline, false) && !Word("while"))
                        Error("Expected 'While'/'Until' here");
                    condition = Expr();
                    if (condition == null)
                        Error("Expected 'Do' condition");
                    Newline();
                    BodyAndEnd(body, "do", "loop", false);
                }
                else
                {
                    checkAfter = true;
                    BodyAndEnd(body, "do", "loop", false);
                    not = Word("until");
                    if (!not && !OfType(TokenType.Newline, false) && !Word("while"))
                        Error("Expected 'While'/'Until' here");
                    condition = Expr();
                    if (condition == null)
                        Error("Expected 'Do' condition");
                }
                return new WhileStmt(condition, body, true, checkAfter, not,
                    t.Line, t.Column);
            }
            else if (!End)
            {
                Expr ex = Unary();
                if (ex == null)
                {
                    if (!End)
                        Error($"Unexpected '{Current.Value}' token");
                    Advance();
                    return null;
                }
                if (Punct("=") || Punct("+=")
                    || Punct("^=") || Punct("*=")
                    || Punct("-=") || Punct("/=")
                    || Punct("<<=") || Punct(">>=")
                    || Punct("&=") || Punct(">>>=")
                    || Punct("<<<="))
                {
                    string op = Previous.Value as string;
                    Expr val = Expr();
                    if (val == null)
                        Error($"Expected expression after '{op}'");
                    else
                    {
                        Newline();
                        return new AssignStmt(ex, val,
                            op[0] switch
                            {
                                '=' => AssignStmt.Operations.None,
                                '+' => AssignStmt.Operations.Add,
                                '&' => AssignStmt.Operations.Concat,
                                '-' => AssignStmt.Operations.Sub,
                                '*' => AssignStmt.Operations.Mul,
                                '/' => AssignStmt.Operations.Div,
                                '^' => AssignStmt.Operations.Xor,
                                '<' => op.Length == 4 ? AssignStmt.Operations.Rol : AssignStmt.Operations.Shl,
                                '>' => op.Length == 4 ? AssignStmt.Operations.Ror : AssignStmt.Operations.Shr,
                                _ => AssignStmt.Operations.None
                            }, ex.Line, ex.Column);
                    }
                }
                Newline();
                return new ExprStmt(ex, ex.Line, ex.Column);
            }
            return null;
        }

        Expr Primary()
        {
            Token t;
            var back = State;
            if (t = Punct("{"))
            {
                var elements = new List<Expr>();
                if (!Punct("}"))
                {
                    do
                    {
                        Expr e = Expr();
                        if (e == null && !Punct("}", false))
                            Error("Expected element");
                        else
                            elements.Add(e);
                    }
                    while (Punct(","));
                    if (!Punct("}"))
                        Error("Expected '}' after initializer list");
                }

                FBType possibleInitTypeHint = ParseType(validation: false);
                return new InitializerExpr(elements, possibleInitTypeHint, t.Line, t.Column);
            }
            else
            {
                SetState(back);
                if (t = OfType(TokenType.Name, false))
                    return new NameExpr(FullName(), t.Line, t.Column);
                else if (t = Word("me"))
                    return new NameExpr("me", t.Line, t.Column);
                else if (t = Word("ctype"))
                {
                    if (!Punct("("))
                    {
                        Error("Expected '(' after 'CType'");
                        return new NameExpr("CType", t.Line, t.Column);
                    }
                    else
                    {
                        Expr expr = Expr();
                        if (expr == null)
                            Error("Expected expression after '('");
                        if (!Punct(","))
                            Error("CType takes exactly 2 arguments");
                        FBType type = ParseType();
                        if (type == null)
                            Error("Expected type");
                        if (!Punct(")")) Error("Expected ')' after CType expression");
                        return new CTypeExpr(expr, type, t.Line, t.Column);
                    }
                }
                else if (
                    (t = Word("cint")) || (t = Word("cuint")) ||
                    (t = Word("clng")) || (t = Word("culng")) ||
                    (t = Word("cshort")) || (t = Word("cushort")) ||
                    (t = Word("cchr")) || (t = Word("cbyte")) ||
                    (t = Word("cwchr")) || (t = Word("cstr")) ||
                    (t = Word("cwstr")))
                {
                    string castName = t.Value as string;
                    Expr casted = Unary();
                    if (casted == null) Error($"Expected expression after '{castName}'");
                    return castName switch
                    {
                        "cint" =>
                            new CTypeExpr(casted, FBType.Prim(FBPrimitiveType.Enum.Integer), t.Line, t.Column),
                        "cuint" =>
                            new CTypeExpr(casted, FBType.Prim(FBPrimitiveType.Enum.UInteger), t.Line, t.Column),
                        "clng" =>
                            new CTypeExpr(casted, FBType.Prim(FBPrimitiveType.Enum.Long), t.Line, t.Column),
                        "culng" =>
                            new CTypeExpr(casted, FBType.Prim(FBPrimitiveType.Enum.ULong), t.Line, t.Column),
                        "cshort" =>
                            new CTypeExpr(casted, FBType.Prim(FBPrimitiveType.Enum.Short), t.Line, t.Column),
                        "cushort" =>
                            new CTypeExpr(casted, FBType.Prim(FBPrimitiveType.Enum.UShort), t.Line, t.Column),
                        "cchr" =>
                            new CTypeExpr(casted, FBType.Prim(FBPrimitiveType.Enum.Char), t.Line, t.Column),
                        "cbyte" =>
                            new CTypeExpr(casted, FBType.Prim(FBPrimitiveType.Enum.Byte), t.Line, t.Column),
                        "cwchr" =>
                            new CTypeExpr(casted, FBType.Prim(FBPrimitiveType.Enum.WChar), t.Line, t.Column),
                        "cstr" =>
                            new CTypeExpr(casted, new FBNamedType("string"), t.Line, t.Column),
                        "cwstr" =>
                            new CTypeExpr(casted, new FBNamedType("wstring"), t.Line, t.Column),
                        _ => null,
                    };
                }
                else if (t = OfType(TokenType.Integer))
                {
                    ulong val = (ulong)t.Value;
                    var type = LiteralExpr.Enum.Integer;
                    if (val <= ulong.MaxValue) type = LiteralExpr.Enum.ULong;
                    if (val <= long.MaxValue) type = LiteralExpr.Enum.Long;
                    if (val <= uint.MaxValue) type = LiteralExpr.Enum.UInteger;
                    if (val <= int.MaxValue) type = LiteralExpr.Enum.Integer;
                    return new LiteralExpr(type, val <= long.MaxValue ? (object)(long)val : val,
                        t.Line, t.Column);
                }
                else if (t = OfType(TokenType.Float))
                    return new LiteralExpr(
                        t.Value is double ? LiteralExpr.Enum.Double : LiteralExpr.Enum.Float,
                        t.Value, t.Line, t.Column);
                else if (t = OfType(TokenType.String))
                    return new LiteralExpr(LiteralExpr.Enum.String, t.Value, t.Line, t.Column);
                else if (t = Word("true"))
                    return new LiteralExpr(LiteralExpr.Enum.Bool, -1L, t.Line, t.Column);
                else if (t = Word("false"))
                    return new LiteralExpr(LiteralExpr.Enum.Bool, 0L, t.Line, t.Column);
                else if (Punct("("))
                {
                    Expr e = Expr();
                    if (!Punct(")")) Error("Expected ')' after expression");
                    return e;
                }
            }
            return null;
        }

        Expr Postfix()
        {
            Expr expr = Primary();
            while (expr != null)
            {
                if (Punct("."))
                {
                    Token t;
                    if (!(t = OfType(TokenType.Name)))
                        Error("Expected identifier after '.'");
                    else
                        expr = new AccessExpr(expr, t.Value as string,
                            expr.Line, expr.Column);
                }
                else if (Punct("("))
                {
                    var args = new List<Expr>();
                    if (!Punct(")"))
                    {
                        do
                        {
                            Expr e = Expr();
                            if (e == null)
                                Error("Expected argument");
                            else
                                args.Add(e);
                        }
                        while (Punct(","));
                        if (!Punct(")"))
                            Error("Expected ')' after call expression");
                    }
                    expr = new CallExpr(expr, args, expr.Line, expr.Column);
                }
                else
                    break;
            }
            return expr;
        }

        Expr Unary()
        {
            Token t;
            int l = Line, c = Col;
            if ((t = Punct("-")) || (t = Punct("*")) ||
                (t = Punct("&")) || (t = Word("sizeof")) || (t = Punct("+")))
            {
                Expr expr = Unary();
                if (expr == null)
                {
                    Error($"Expected expression after '{t.Value}'");
                    return null;
                }
                string s = t.Value as string;
                return new UnaryExpr(s[0] switch {
                    '-' => UnaryExpr.Operations.Minus,
                    '&' => UnaryExpr.Operations.GetAddr,
                    '*' => UnaryExpr.Operations.Deref,
                    'S' => UnaryExpr.Operations.SizeOf,
                    _ => UnaryExpr.Operations.Plus
                }, expr, l, c);
            }
            return Postfix();
        }

        Expr Multiplication()
        {
            Expr left = Unary();
            Token t;
            if (left == null) return null;
            while ((t = Punct("*")) || (t = Punct("/")))
            {
                Expr right = Unary();
                if (right == null)
                    Error($"Expected expression after '{t.Value}'");
                else
                    left = new BinaryExpr(left,
                        (t.Value as string)[0] == '*' ? BinaryExpr.Operations.Mul :
                        BinaryExpr.Operations.Div, right,
                        left.Line, left.Column);
            }
            return left;
        }

        Expr Modulo()
        {
            Expr left = Multiplication();
            Token t;
            if (left == null) return null;
            while (t = Word("mod"))
            {
                Expr right = Multiplication();
                if (right == null)
                    Error($"Expected expression after '{t.Value}'");
                else
                    left = new BinaryExpr(left, BinaryExpr.Operations.Mod, right,
                        left.Line, left.Column);
            }
            return left;
        }

        Expr Addition()
        {
            Expr left = Modulo();
            Token t;
            if (left == null) return null;
            while ((t = Punct("+")) || (t = Punct("-")))
            {
                Expr right = Modulo();
                if (right == null)
                    Error($"Expected expression after '{t.Value}'");
                else
                    left = new BinaryExpr(left,
                        (t.Value as string)[0] == '+' ? BinaryExpr.Operations.Add :
                        BinaryExpr.Operations.Sub, right,
                        left.Line, left.Column);
            }
            return left;
        }

        Expr Concat()
        {
            Expr left = Addition();
            if (left == null) return null;
            while (Punct("&"))
            {
                Expr right = Addition();
                if (right == null)
                    Error($"Expected expression after '&'");
                else
                    left = new BinaryExpr(left, BinaryExpr.Operations.Concat, right,
                        left.Line, left.Column);
            }
            return left;
        }

        Expr Shift()
        {
            Expr left = Concat();
            Token t;
            if (left == null) return null;
            while ((t = Punct(">>")) || (t = Punct("<<")) ||
                (t = Punct(">>>")) || (t = Punct("<<<")))
            {
                Expr right = Concat();
                string op = t.Value as string;
                if (right == null)
                    Error($"Expected expression after '{t.Value}'");
                else
                    left = new BinaryExpr(left,
                        op[0] == '>'
                            ? (op.Length == 3 ? BinaryExpr.Operations.Ror : BinaryExpr.Operations.Shr)
                            : (op.Length == 3 ? BinaryExpr.Operations.Rol : BinaryExpr.Operations.Shl),
                        right, left.Line, left.Column);
            }
            return left;
        }

        Expr Comparison()
        {
            Expr left = Shift();
            Token t;
            if (left == null) return null;
            while ((t = Punct("=")) || (t = Punct("<>")) || (t = Punct("<")) || (t = Punct(">"))
                || (t = Punct(">=")) || (t = Punct("<=")))
            {
                Expr right = Shift();
                string op = t.Value as string;
                if (right == null)
                    Error($"Expected expression after '{t.Value}'");
                else
                    left = new BinaryExpr(left, op switch {
                        "="  => BinaryExpr.Operations.Eq,
                        "<>" => BinaryExpr.Operations.Neq,
                        "<"  => BinaryExpr.Operations.Lt,
                        ">"  => BinaryExpr.Operations.Gt,
                        ">=" => BinaryExpr.Operations.Ge,
                        "<=" => BinaryExpr.Operations.Le,
                        _    => (BinaryExpr.Operations)0
                    }, right, left.Line, left.Column);
            }
            return left;
        }

        Expr NOT()
        {
            int l = Line, c = Col;
            if (Word("not"))
            {
                Expr expr = NOT();
                if (expr == null)
                {
                    Error("Expected expression after 'Not'");
                    return null;
                }
                return new UnaryExpr(UnaryExpr.Operations.Not, expr, l, c);
            }
            return Comparison();
        }

        Expr AND()
        {
            Expr left = NOT();
            if (left == null) return null;
            while (Word("and"))
            {
                Expr right = NOT();
                if (right == null)
                    Error("Expected expression after 'And'");
                else
                    left = new BinaryExpr(left, BinaryExpr.Operations.And, right,
                        left.Line, left.Column);
            }
            return left;
        }

        Expr OR()
        {
            Expr left = AND();
            if (left == null) return null;
            while (Word("or"))
            {
                Expr right = AND();
                if (right == null)
                    Error("Expected expression after 'Or'");
                else
                    left = new BinaryExpr(left, BinaryExpr.Operations.Or, right,
                        left.Line, left.Column);
            }
            return left;
        }

        Expr XOR()
        {
            Expr left = OR();
            if (left == null) return null;
            while (Word("xor") || Punct("^"))
            {
                Expr right = OR();
                if (right == null)
                    Error("Expected expression after XOR operator");
                else
                    left = new BinaryExpr(left, BinaryExpr.Operations.Xor, right,
                        left.Line, left.Column);
            }
            return left;
        }

        Expr Expr() => XOR();

        Expr RangeExpr()
        {
            Expr left = Expr();
            if (left == null) return null;
            if (Word("to"))
            {
                Expr right = Expr();
                if (right == null)
                    Error("Expected expression after 'To'");
                else
                    return new RangeExpr(left, right, left.Line, left.Column);
            }
            return left;
        }

        public Unit Parse()
        {
            var statements = new List<Stmt>();
            SetState((0, 1, 1));
            for (; !End; statements.Add(Stmt()));
            return new Unit(Filename, statements);
        }
    }
}

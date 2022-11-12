using System;

namespace firebasic.AST
{
    public class Name : IEquatable<Name>
    {
        public Name Parent { get; set; }

        public string Child { get; set; }

        public Name(string child, Name parent = null)
        {
            Child = child;
            Parent = parent;
        }

        public override string ToString()
            => Parent == null ? Child : $"{Parent}::{Child}";

        public bool Equals(Name other) => Child == other.Child && Parent == other.Parent;

        public static implicit operator string(Name name) => name.ToString();

        public static implicit operator Name(string name)
        {
            if (!name.Contains(":")) return new Name(name);
            int lio = name.LastIndexOf(':') - 1;
            return new Name(name.Substring(lio), name.Substring(0, lio));
        }
    }
}

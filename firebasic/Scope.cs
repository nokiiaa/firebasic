using System;
using System.Collections.Generic;

namespace firebasic
{

    public class Scope
    {
        public Scope Parent { get; set; }

        public Dictionary<string, object> Objects { get; set; }

        public Scope(Scope parent = null,
            Dictionary<string, object> objects = null)
        {
            Parent = parent;
            Objects = objects ?? new Dictionary<string, object>();
        }

        public T Get<T>(string name, bool recurse = true) where T : class
        {
            object got = Objects.ContainsKey(name) ? Objects[name] :
                recurse ? Parent?.Get<T>(name) : null;
            if (got is T) return (T)got;
            return null;
        }

        public T Declare<T>(string name, T value) where T : class
        {
            if (Get<object>(name, recurse: false) != null)
                throw new ArgumentException($"'{name}' already declared");
            return (T)(Objects[name] = value);
        }

        public T Set<T>(string name, T value, bool recurse = true) where T : class
            => (T)(Objects.ContainsKey(name) ? Objects[name] = value :
                recurse ? Parent?.Set(name, value) : null);

        public Scope Up => Parent;

        public Scope Down => new Scope(this);
    }
}

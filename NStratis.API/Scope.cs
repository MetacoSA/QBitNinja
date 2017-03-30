using System;
using System.Linq;

namespace QBitNinja
{
    public class Scope
    {
        public Scope()
        {
            _parents = new string[0];
        }
        public Scope(string[] parents)
        {
            if (parents == null)
                parents = new string[0];
            if (parents.Any(parent => parent.Contains("/")))
            {
                throw new ArgumentException("'/' is not authorized for naming a scope");
            }
            _parents = parents;
        }

        private readonly string[] _parents;
        public string[] Parents
        {
            get
            {
                return _parents;
            }
        }

        public Scope GetChild(params string[] names)
        {
            var parents = _parents.ToList();
            parents.AddRange(names);
            return new Scope(parents.ToArray());
        }

        public override string ToString()
        {
            return String.Join("/", _parents);
        }
    }
}

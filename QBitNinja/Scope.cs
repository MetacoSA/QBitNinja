using System;
using System.Collections.Generic;
using System.Linq;

namespace QBitNinja
{
    public class Scope
    {
        public Scope()
        {
            Parents = new string[0];
        }

        public Scope(string[] parents)
        {
            if (parents == null)
            {
                parents = new string[0];
            }

            if (parents.Any(parent => parent.Contains("/")))
            {
                throw new ArgumentException("'/' is not authorized for naming a scope");
            }

            Parents = parents;
        }

        public string[] Parents { get; }

        public Scope GetChild(params string[] names)
        {
            List<string> parents = Parents.ToList();
            parents.AddRange(names);
            return new Scope(parents.ToArray());
        }

        public override string ToString() => string.Join("/", Parents);
    }
}

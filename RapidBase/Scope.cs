using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RapidBase
{
    public class Scope
    {
        public Scope()
        {
            _Parents = new string[0];
        }
        public Scope(string[] parents)
        {
            if (parents == null)
                parents = new string[0];
            foreach (var parent in parents)
                if (parent.Contains("/"))
                    throw new ArgumentException("'/' is not authorized for naming a scope");
            _Parents = parents;
        }

        private readonly string[] _Parents;
        public string[] Parents
        {
            get
            {
                return _Parents;
            }
        }

        public Scope GetChild(params string[] names)
        {
            var parents = _Parents.ToList();
            foreach(var name in names)
                parents.Add(name);
            return new Scope(parents.ToArray());
        }

        public override string ToString()
        {
            return String.Join("/", _Parents);
        }
    }
}

using System;
using System.Linq;

namespace QBitNinja
{
    /// <summary>
    /// Represents a scope for some part of a hierarchy, where each scope is represented by a number of names that make up a path
    /// within that hierarchy. A scope covers all children in that path.
    /// </summary>
    public class Scope
    {
        /// <summary>
        /// Create a scope for an empty path.
        /// </summary>
        public Scope()
        {
            Parents = new string[0];
        }

        /// <summary>
        /// Create a scope for a given path.
        /// </summary>
        /// <param name="parents">A path represented by a number of names, the name at the first index being the root.</param>
        public Scope(string[] parents)
        {
            if (parents == null)
                parents = new string[0];
            if (parents.Any(parent => parent.Contains("/")))
            {
                throw new ArgumentException("'/' is not authorized for naming a scope");
            }
            Parents = parents;
        }

        /// <summary>
        /// A path represented by a number of names, the name at the first index being the root.
        /// </summary>
        public string[] Parents
        {
            get;
            private set;
        }

        /// <summary>
        /// Get a child scope, which will be represented by the path of the current scope, extended with 
        /// the given subpath.
        /// </summary>
        /// <param name="names">An array of names that make up a path rooted in the current scope.</param>
        /// <returns></returns>
        public Scope GetChild(params string[] names)
        {
            var parents = Parents.ToList();
            parents.AddRange(names);
            return new Scope(parents.ToArray());
        }

        /// <summary>
        /// Join the parts of the path with forward slashes to get a string representation of the scope.
        /// </summary>
        public override string ToString()
        {
            return String.Join("/", Parents);
        }
    }
}

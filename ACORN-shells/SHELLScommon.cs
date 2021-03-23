using Grasshopper;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;
using Rhino;
using Rhino.Geometry;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ACORN_shells
{
    /// <summary>
    /// 
    /// Class containing functions used in components
    /// The ones written by ACORN at least
    /// 
    /// </summary>
    class SHELLScommon
    {
        /*
        public static DataTree<T> TrimTreeD2(DataTree<T> tree) where T : System.IComparable<T>
        {
            DataTree<GeometryBase> trimmedTree = new DataTree<GeometryBase>();
            foreach (GH_Path path in tree.Paths)
            {
                // look at first index corresponding to segments
                int currIndex = path.Indices[0];
                // creates a branch to store the modules in current segment in flatExtendedSegments
                GH_Path flatPath = new GH_Path(currIndex);
                // gets corresponding item
                GeometryBase currItem = tree.Branch(path)[0];
                trimmedTree.Add(currItem, flatPath);
            }
            return trimmedTree;
        }
        */
    }
}

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
        
        public static DataTree<object> TrimTreeD2(DataTree<object> tree)
        {
            DataTree<object> trimmedTree = new DataTree<object>();
            foreach (GH_Path path in tree.Paths)
            {
                // look at first index corresponding to segments
                int currIndex = path.Indices[0];
                // creates a branch to store the modules in current segment in flatExtendedSegments
                GH_Path flatPath = new GH_Path(currIndex);
                // gets corresponding item
                object currItem = tree.Branch(path)[0];
                trimmedTree.Add(currItem, flatPath);
            }
            return trimmedTree;
        }

        /// <summary>
        /// Extract corners and edges from shell surface, corners being the 4 shortest boundary edges
        /// </summary>
        /// <param name="shell">Shell surface</param>
        /// <param name="corners">Shell corner curves</param>
        /// <param name="edges">Shell edge curves</param>
        public static void GetShellEdges(Brep shell, out List<Curve> corners, out List<Curve> edges)
        {
            var shellAllEdges = shell.Edges;
            // sort edges by length
            List<BrepEdge> sortedAllEdges = shellAllEdges.OrderBy(s => s.GetLength()).ToList();
            // get 50% shortest edges
            corners = new List<Curve>();
            edges = new List<Curve>();
            int numAllEdges = sortedAllEdges.Count;
            for (int i = 0; i < numAllEdges / 2; i++) corners.Add(sortedAllEdges[i].EdgeCurve); // equivalent to GetRange(0,halfEdgeCount)
            for (int i = numAllEdges / 2; i < numAllEdges; i++) edges.Add(sortedAllEdges[i].EdgeCurve);

        }



    }
}

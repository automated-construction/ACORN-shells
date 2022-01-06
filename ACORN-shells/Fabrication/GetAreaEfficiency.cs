using System;
using System.Collections.Generic;

using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;
using Rhino.Geometry;
using System.Linq;
using Rhino.Geometry.Collections;
using Rhino.Collections;
using GH.MiscToolbox.Components.Utilities;
using Grasshopper;

namespace ACORN_shells
{
    /// <summary>
    /// Calculates efficiency
    /// </summary>
    public class GetAreaEfficiency : GH_Component
    {
        public GetAreaEfficiency()
          : base("Get Area Efficiency", "A:AreaEff",
              "Calculates area efficiency of segment / modules",
              "ACORN Shells", " Fabrication")
        // adding spaces to category names as per https://www.grasshopper3d.com/forum/topics/change-order-of-plugin-sub-category-c 
        {
        }

        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddBrepParameter("Segment", "S", "Shell segment", GH_ParamAccess.tree);
            pManager.AddRectangleParameter("Modules", "M", "Rectangles corresponding to modules", GH_ParamAccess.tree);
        }

        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddNumberParameter("Area efficiency", "E", "Area efficiency of segment / modules.", GH_ParamAccess.tree);
            //pManager.AddRectangleParameter("Modules", "M", "Rectangles corresponding to modules (flattened tree)", GH_ParamAccess.tree); //debug
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {

            GH_Structure<GH_Brep> ghSegmentTree = new GH_Structure<GH_Brep>();
            GH_Structure<GH_Rectangle> ghModuleTree = new GH_Structure<GH_Rectangle>();

            if (!DA.GetDataTree<GH_Brep>(0, out ghSegmentTree)) return;
            if (!DA.GetDataTree<GH_Rectangle>(1, out ghModuleTree)) return;

            
            // collect all rectangles per segment into a DataTree
            // since the input is GH_Structure, needs casting to RhinoCommon
            DataTree<Rectangle3d> flatModuleTree = new DataTree<Rectangle3d>();

            foreach (GH_Path path in ghModuleTree.Paths)
            {
                // look at first index corresponding to segments
                int currSegment = path.Indices[0];
                // creates a branch to store the modules in current segment in flatModuleTree
                GH_Path flatPath = new GH_Path(currSegment);

                // casting GrassHopper to RhinoCommon objects
                Rectangle3d rcModule = new Rectangle3d();
                GH_Rectangle ghModule = ghModuleTree.get_Branch(path)[0] as GH_Rectangle;
                GH_Convert.ToRectangle3d(ghModule, ref rcModule, GH_Conversion.Both);

                flatModuleTree.Add(rcModule, flatPath);
            }


            // convert segmentTree: GH_Structure (Grasshopper) to DataTree (RhinoCommon)
            DataTree<Brep> rcSegmentTree = new DataTree<Brep>();
            foreach (GH_Path path in ghSegmentTree.Paths)
            {
                GH_Brep ghSegment = ghSegmentTree.get_Branch(path)[0] as GH_Brep;
                Brep rcSegment = new Brep();
                GH_Convert.ToBrep(ghSegment, ref rcSegment, GH_Conversion.Both);
                rcSegmentTree.Add(rcSegment, path);
            }

            DataTree<double> efficiencyTree = new DataTree<double>();

            // assuming tree structure in rcSegmentTree and flatModuleTree is the same: {one}(many)
            foreach (GH_Path path in flatModuleTree.Paths)             
            {
                //get area for module set 
                double modulesTotalArea = 0;                              
                foreach (Rectangle3d module in flatModuleTree.Branch(path)) 
                    modulesTotalArea += module.Area;

                // get segment edge curve
                List<Curve> edges = new List<Curve>();
                Brep currSegment = rcSegmentTree.Branch(path)[0];
                foreach (BrepEdge edge in currSegment.Edges) edges.Add(edge.ToNurbsCurve());
                Curve joinedSegmentEdges = Curve.JoinCurves(edges)[0];

                // get segment area, projected onto module plane
                List<Rectangle3d> modules = flatModuleTree.Branch(path);
                Curve projectedSegment = Curve.ProjectToPlane (joinedSegmentEdges, modules[0].Plane);
                double segmentProjectedArea = AreaMassProperties.Compute(projectedSegment).Area;

                double currEfficiency = segmentProjectedArea / modulesTotalArea;
                efficiencyTree.Add(currEfficiency, path);
            }


            DA.SetDataTree(0, efficiencyTree);
            //DA.SetDataTree(1, flatModuleTree); //debug
        }

        protected override System.Drawing.Bitmap Icon
        {
            get
            {
                return ACORN_shells.Properties.Resources.areaEff;
            }
        }

        public override Guid ComponentGuid
        {
            get { return new Guid("192ec099-12a5-45ad-86b1-68f8e9a24581"); }
        }
    }
}
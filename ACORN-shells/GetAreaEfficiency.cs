using System;
using System.Collections.Generic;

using Grasshopper.Kernel;
using Rhino.Geometry;
using System.Linq;
using Rhino.Geometry.Collections;
using Rhino.Collections;
using GH.MiscToolbox.Components.Utilities;

namespace ACORN_shells
{
    /// <summary>
    /// Fits a bounding box to a shell segment, minimizing volume.
    /// </summary>
    public class GetAreaEfficiency : GH_Component
    {
        public GetAreaEfficiency()
          : base("Get Area Efficiency", "A:AreaEff",
              "Calculates area efficiency of segment / modules",
              "ACORN", "Pinbed")
        {
        }

        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddBrepParameter("Segment", "S", "Shell segment.", GH_ParamAccess.item);
            pManager.AddRectangleParameter("Modules", "M", "Rectangles corresponding to modules", GH_ParamAccess.list); //change to tree?

        }

        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddNumberParameter("Area efficiency", "E", "Area efficiency of segment / modules.", GH_ParamAccess.item);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            Brep segment = null;
            List<Rectangle3d> modules = new List<Rectangle3d>();

            if (!DA.GetData(0, ref segment)) return;
            if (!DA.GetDataList(1, modules)) return;

            double modulesTotalArea = 0;
            
            //get area for module set 
            foreach (Rectangle3d module in modules) modulesTotalArea += module.Area;

            // get segment edge curve
            List<Curve> edges = new List<Curve>();
            foreach (BrepEdge edge in segment.Edges) edges.Add(edge.ToNurbsCurve());
            Curve joinedSegmentEdges = Curve.JoinCurves(edges)[0];

            // get segment area, projected onto module plane
            Curve projectedSegment = Curve.ProjectToPlane (joinedSegmentEdges, modules[0].Plane);
            double segmentProjectedArea = AreaMassProperties.Compute(projectedSegment).Area;

            double efficiency = segmentProjectedArea / modulesTotalArea;

            DA.SetData(0, efficiency);
        }

        protected override System.Drawing.Bitmap Icon
        {
            get
            {
                return ACORN_shells.Properties.Resources.ACORN_24;
            }
        }

        public override Guid ComponentGuid
        {
            get { return new Guid("192ec099-12a5-45ad-86b1-68f8e9a24581"); }
        }
    }
}
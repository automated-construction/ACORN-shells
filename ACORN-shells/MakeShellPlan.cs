using System;
using System.Collections.Generic;

using Grasshopper.Kernel;
using Rhino.Geometry;
using System.Linq;

namespace ACORN_shells
{
    public class MakeShellPlan : GH_Component
    {
        public MakeShellPlan()
          : base("MakeShellPlan", "A:MakeShellPlan",
              "Creates a 2D shell plan on the XY plane.",
              "ACORN", "Shells")
        {
        }

        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddCurveParameter("Outline", "O", "Outline curve. Should be a PolyLine.", GH_ParamAccess.item);
            pManager.AddNumberParameter("CornerRadius", "R", "Radius to bevel corners by.", GH_ParamAccess.item);
        }

        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddCurveParameter("Plan", "P", "Outlinie curve of generated plan.", GH_ParamAccess.item);
            pManager.AddCurveParameter("Corners", "C", "Curves of plan corners.", GH_ParamAccess.list);
            pManager.AddCurveParameter("Edges", "E", "Curves of plan Edges.", GH_ParamAccess.list);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            Curve outline = null;
            double cornerRadius = 0;

            if (!DA.GetData(0, ref outline)) return;
            if (!DA.GetData(1, ref cornerRadius)) return;

            // Project onto XY plane
            outline = Curve.ProjectToPlane(outline, Plane.WorldXY);

            // Explode to lines
            var explodedOutline = outline.DuplicateSegments();

            // Find centroid
            var areaMassProp = AreaMassProperties.Compute(outline);
            var centroid = areaMassProp.Centroid;

            // Trim edges to create plan edges
            var edges = explodedOutline.Select(l => l.Trim(CurveEnd.Both, cornerRadius)).ToList();

            // Create corners
            List<Curve> corners = new List<Curve>();

            for (int i = 0; i < edges.Count; i++)
            {
                var cornerPoint = explodedOutline[i].PointAtStart;
                var arcStart = explodedOutline[i].PointAtLength(cornerRadius);
                var prevIndex = i == 0 ? edges.Count - 1 : i - 1;
                var arcEnd = explodedOutline[prevIndex].PointAtLength(explodedOutline[prevIndex].GetLength() - cornerRadius);
                var toCentroid = new Vector3d(centroid - cornerPoint);
                toCentroid.Unitize();
                var arcInterior = cornerPoint + toCentroid * cornerRadius;
                var arc = new Arc(arcStart, arcInterior, arcEnd);
                corners.Add(new ArcCurve(arc));
            }

            // Join edges and corners
            var zippedCurves = corners.Zip(edges, (x, y) => new List<Curve>() { x, y }).SelectMany(x => x);
            var plan = Curve.JoinCurves(zippedCurves).FirstOrDefault();

            DA.SetData(0, plan);
            DA.SetDataList(1, corners);
            DA.SetDataList(2, edges);
        }

        protected override System.Drawing.Bitmap Icon
        {
            get
            {
                return null;
            }
        }

        public override Guid ComponentGuid
        {
            get { return new Guid("05f0f2e6-e8cf-49fe-b1ff-961b3f29a523"); }
        }
    }
}
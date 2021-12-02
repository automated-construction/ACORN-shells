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
              "ACORN Shells", "Formfinding")
        {
        }

        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddCurveParameter("Outline", "O", "Outline curve. Should be a PolyLine.", GH_ParamAccess.item);
            pManager.AddNumberParameter("CornerRadius", "R", "Radius to bevel corners by.", GH_ParamAccess.item);
            pManager.AddBooleanParameter("CurvedCorner", "C", "If True, corner is circular; else, corner is straight", GH_ParamAccess.item);
            pManager.AddNumberParameter("TargetArea", "TA", "Target area for scaling outline (optional).", GH_ParamAccess.item);

            pManager[3].Optional = true; // target area
        }

        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddCurveParameter("Plan", "P", "Outline curve of generated plan.", GH_ParamAccess.item);
            //pManager.AddCurveParameter("Corners", "C", "Curves of plan corners.", GH_ParamAccess.list);
            //pManager.AddCurveParameter("Edges", "E", "Curves of plan Edges.", GH_ParamAccess.list);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            Curve outline = null;
            double cornerRadius = 0;
            bool curvedCorner = false;
            double targetArea = 0;

            if (!DA.GetData(0, ref outline)) return;
            if (!DA.GetData(1, ref cornerRadius)) return;
            if (!DA.GetData(2, ref curvedCorner)) return;
            DA.GetData(3, ref targetArea);

            // Project onto XY plane
            outline = Curve.ProjectToPlane(outline, Plane.WorldXY);

            // scale to target area (optional)
            if (targetArea != 0)
            {
                AreaMassProperties outlineProp = AreaMassProperties.Compute(outline);
                double scaleFactor = Math.Sqrt(targetArea / outlineProp.Area);
                outline.Transform(Transform.Scale(outlineProp.Centroid, scaleFactor));
            }

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
                if (curvedCorner) 
                { 
                    var toCentroid = new Vector3d(centroid - cornerPoint);
                    toCentroid.Unitize();
                    var arcInterior = cornerPoint + toCentroid * cornerRadius;
                    var arc = new Arc(arcStart, arcInterior, arcEnd);
                    corners.Add(new ArcCurve(arc));
                }
                else // straight corner
                {
                    var line = new Line(arcStart, arcEnd);
                    corners.Add(new LineCurve(line));
                }
            }

            // Join edges and corners
            var zippedCurves = corners.Zip(edges, (x, y) => new List<Curve>() { x, y }).SelectMany(x => x);
            var plan = Curve.JoinCurves(zippedCurves).FirstOrDefault();

            DA.SetData(0, plan);
            //DA.SetDataList(1, corners);
            //DA.SetDataList(2, edges);
        }

        protected override System.Drawing.Bitmap Icon
        {
            get
            {
                return ACORN_shells.Properties.Resources.makeplan;
            }
        }

        public override Guid ComponentGuid
        {
            get { return new Guid("05f0f2e6-e8cf-49fe-b1ff-961b3f29a523"); }
        }
    }
}
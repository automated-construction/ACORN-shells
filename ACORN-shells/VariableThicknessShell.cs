using System;
using System.Collections.Generic;
using System.Linq;

using Grasshopper.Kernel;
using Rhino.Geometry;

using Karamba.GHopper.Geometry;
using Karamba.Geometry;
using Karamba.Elements;
using Karamba.Supports;
using Karamba.GHopper.Elements;
using Karamba.GHopper.Supports;

namespace ACORN_shells
{
    /// <summary>
    /// Creates a shell with variable thickness, with three layers: 
    /// medial layer to pass to Kaamba, top and bottom to calculate local thicknesses
    /// 
    /// ToDo:
    /// change name
    /// coordinate with MakeShell
    /// </summary>
    public class VariableThicknessShell : GH_Component
    {

        public VariableThicknessShell()
          : base("Makes Variable Thickness Shell Surface", "A:VarThickShell",
              "Creates a shell with variable thickness, with three layers: medial layer to pass to Kaamba, top and bottom to calculate local thicknesses",
              "ACORN", "Shells")
        {
        }

        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddBrepParameter("Shell surface", "S", "Shell surface.", GH_ParamAccess.item);
            pManager.AddNumberParameter("Thickness at apex", "Ta", "Thickness of shell at apex.", GH_ParamAccess.item);
            pManager.AddNumberParameter("Thickness at supports", "Ts", "Thickness of shell at supports.", GH_ParamAccess.item);
            pManager.AddNumberParameter("Spread length", "L", "Spread length.", GH_ParamAccess.item);
        }

        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddBrepParameter("Shell Layers", "L", "Shell elements for Karamba", GH_ParamAccess.list);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            Brep shell = null;
            double thickApex = 0;
            double thickSupp = 0;
            double length = 0;

            if (!DA.GetData(0, ref shell)) return;
            if (!DA.GetData(1, ref thickApex)) return;
            if (!DA.GetData(2, ref thickSupp)) return;
            if (!DA.GetData(3, ref length)) return;

            // get trim information from S, to re-apply to all with Brep.CreateTrimmedSurface
            BrepFace trimmedSurface = shell.Faces[0];
            NurbsSurface origSrf = trimmedSurface.ToNurbsSurface();
            Curve trimCurve = trimmedSurface.OuterLoop.To3dCurve();

            // get uv coordinates for corner curves midpoints
            List<Point2d> suppUVs = new List<Point2d>();
            SHELLScommon.GetShellEdges(shell, out List<Curve> corners, out _); //edges not used, but out _ was not working
            foreach (Curve corner in corners)
            {
                Point3d thickPt = corner.PointAtNormalizedLength(.5);
                double maximumDistance = 1.0;
                origSrf.ToBrep().ClosestPoint (thickPt, out _, out _, out double s, out double t, maximumDistance, out _);
                suppUVs.Add(new Point2d(s, t));
            }

            List<Brep> layers = new List<Brep>();
            int layerCount = 3; // includes original

            //Vector3d delta = new Vector3d(0, 0, -thickSupp); // calculate surface normal instead...
            double uLength = length; // calculate from shell span instead of input
            double vLength = length;
            double tolerance = Rhino.RhinoDoc.ActiveDoc.ModelAbsoluteTolerance;

            //List<Point3d> pushedPTs = new List<Point3d>(); // TEST
            for (int i = 0; i < layerCount; i++)
            {
                double layerThickness = i * thickApex / (layerCount - 1);
                Vector3d layerDelta = new Vector3d(0, 0, -(i * thickSupp / (layerCount - 1)));
                Surface currSrf = origSrf as Surface;

                //foreach (Point3d suppPT in suppPTs) // TEST
                //{
                //  Point3d pushPT = suppPT + delta;
                //  //pushPT = delta));
                //  pushPTs.Add(pushPT);
                //}

                foreach (Point2d suppUV in suppUVs)
                    currSrf = Surface.CreateSoftEditSurface(currSrf, suppUV, layerDelta, uLength, vLength, tolerance, false);

                currSrf.Translate(0, 0, -layerThickness);
                Curve[] projectedTrimCurves = Curve.ProjectToBrep(trimCurve, currSrf.ToBrep(), new Vector3d(0, 0, -1), tolerance);
                Brep currSurfTrimmed = currSrf.ToBrep().Split(projectedTrimCurves, tolerance)[1];
                layers.Add(currSurfTrimmed);
            }

            DA.SetDataList(0, layers);
            //DA.SetDataList(1, pushPTs); // TEST
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
            get { return new Guid("a75d44eb-a7c9-4933-82f5-1b347a18a3e6"); }
        }
    }
}
using System;
using System.Collections.Generic;

using Grasshopper.Kernel;
using Rhino.Geometry;

namespace ACORN_shells
{
    /// <summary>
    /// Creates a shell with variable thickness, with three layers: 
    /// medial layer to pass onto Karamba, top and bottom to calculate local thicknesses
    /// </summary>
    public class VariableThicknessShell : GH_Component
    {

        public VariableThicknessShell()
          : base("Variable Thickness Shell", "VarThickShell",
              "Creates a shell with variable thickness, with three layers: medial layer to pass onto Karamba, top and bottom to calculate local thicknesses",
              "ACORN Shells", "  Shape")
        // adding spaces to category names as per https://www.grasshopper3d.com/forum/topics/change-order-of-plugin-sub-category-c 
        {
        }

        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddBrepParameter("Shell surface", "S", "Shell surface.", GH_ParamAccess.item);
            pManager.AddNumberParameter("Thickness at apex", "Ta", "Thickness of shell at apex.", GH_ParamAccess.item);
            pManager.AddNumberParameter("Thickness at supports", "Ts", "Thickness of shell at supports.", GH_ParamAccess.item);
            pManager.AddBooleanParameter("Quadratic?", "Q", "Quadratic or Linear interpolation?", GH_ParamAccess.item);
        }

        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddBrepParameter("Top surface", "T", "Shell top surface (extrados)", GH_ParamAccess.item);
            pManager.AddBrepParameter("Medial surface", "M", "Shell medial surface", GH_ParamAccess.item);
            pManager.AddBrepParameter("Bottom surface", "B", "Shell bottom surface (intrados)", GH_ParamAccess.item);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            Brep shell = null;
            double thickApex = 0;
            double thickSupp = 0;
            bool quadratic = false;

            if (!DA.GetData(0, ref shell)) return;
            if (!DA.GetData(1, ref thickApex)) return;
            if (!DA.GetData(2, ref thickSupp)) return;
            if (!DA.GetData(3, ref quadratic)) return;

            double tolerance = Rhino.RhinoDoc.ActiveDoc.ModelAbsoluteTolerance;
            double thickExtra = thickSupp - thickApex;

            // get trim information from S, to re-apply to all with Brep.CreateTrimmedSurface
            BrepFace trimmedSurface = shell.Faces[0];
            NurbsSurface origSrf = trimmedSurface.ToNurbsSurface();
            Curve trimCurve = trimmedSurface.OuterLoop.To3dCurve();

            List<Point3d> suppPts = new List<Point3d>();            
            SHELLScommon.GetShellEdges(shell, out List<Curve> corners, out _);
            foreach (Curve corner in corners)
            {
                Point3d thickPt = corner.PointAtNormalizedLength(.5);
                suppPts.Add(thickPt);
            }
            

            // for simplification, use distance apex-support, assuming symmetry
            // get apex point
            Point3d apex = origSrf.PointAt (
                 origSrf.Domain(0).ParameterAt(.5),
                 origSrf.Domain(1).ParameterAt(.5));
            double semidiagonal = apex.DistanceTo(suppPts[0]);
            

            List<Brep> layers = new List<Brep>();
            List<double> layerFactors = new List<double> { .5, 0, -.5};

            foreach (double layerFactor in layerFactors)
            {
                NurbsSurface currSrf = new NurbsSurface (origSrf);                
                // iterate surface control points in U and V
                for (int v = 0; v < currSrf.Points.CountV; v++)
                    for (int u = 0; u < currSrf.Points.CountU; u++)
                    {
                        Point3d currCtrlPt = currSrf.Points.GetControlPoint(u, v).Location;

                        // calculate extra thickness, interpolating between input thickness values for edge and apex
                        // hardcoded option between linear and quadratic interpolation
                        double positionFactor = currCtrlPt.DistanceTo(apex) / semidiagonal; //linear interpolation
                        if (quadratic)
                            positionFactor = Math.Pow (positionFactor, 2); //quadratic interpolation
                            

                        double currThickness = (thickApex + (thickExtra * positionFactor)) * layerFactor;

                        // move them
                        currSrf.Points.SetPoint(u, v, currCtrlPt + new Vector3d (0, 0, currThickness));
                    }

                Curve[] projectedTrimCurves = Curve.ProjectToBrep(trimCurve, currSrf.ToBrep(), new Vector3d(0, 0, -1), tolerance);
                Brep currSurfTrimmed = currSrf.ToBrep().Split(projectedTrimCurves, tolerance)[1];
                layers.Add(currSurfTrimmed);
            }

            DA.SetData(0, layers[0]);
            DA.SetData(1, layers[1]);
            DA.SetData(2, layers[2]);
        }

        protected override System.Drawing.Bitmap Icon
        {
            get
            {
                return ACORN_shells.Properties.Resources.varThick;
            }
        }

        public override Guid ComponentGuid
        {
            get { return new Guid("a75d44eb-a7c9-4933-82f5-1b347a18a3e6"); }
        }


        public override GH_Exposure Exposure
        {
            get { return GH_Exposure.primary; }
        }

    }
}
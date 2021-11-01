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
using Rhino.Geometry.Collections;
using Grasshopper;
using System.IO;
using Grasshopper.Kernel.Data;
using Rhino.DocObjects;

namespace ACORN_shells
{
    /// <summary>
    /// Creates a shell with variable thickness, with three layers: 
    /// medial layer to pass to Karamba, top and bottom to calculate local thicknesses
    /// 
    /// ToDo:
    /// change name
    /// coordinate with MakeShell
    /// </summary>
    public class VariableThicknessShell : GH_Component
    {

        public VariableThicknessShell()
          : base("Makes Variable Thickness Shell Surface", "A:VarThickShell",
              "Creates a shell with variable thickness, with three layers: medial layer to pass to Karamba, top and bottom to calculate local thicknesses",
              "ACORN Shells", "Analysis")
        {
        }

        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddBrepParameter("Shell surface", "S", "Shell surface.", GH_ParamAccess.item);
            pManager.AddNumberParameter("Thickness at apex", "Ta", "Thickness of shell at apex.", GH_ParamAccess.item);
            pManager.AddNumberParameter("Thickness at supports", "Ts", "Thickness of shell at supports.", GH_ParamAccess.item);
            pManager.AddMeshParameter("Shell meshes", "M", "Shell meshes to calculate thickness per face.", GH_ParamAccess.list);
            //pManager.AddIntegerParameter("Number of layers", "L", "Number of layers", GH_ParamAccess.item);

            pManager[3].Optional = true;
        }

        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddBrepParameter("Top surface", "T", "Shell top surface (extrados)", GH_ParamAccess.item);
            pManager.AddBrepParameter("Medial surface", "M", "Shell medial surface", GH_ParamAccess.item);
            pManager.AddBrepParameter("Bottom surface", "B", "Shell bottom surface (intrados)", GH_ParamAccess.item);
            pManager.AddNumberParameter("Mesh thicknesses", "MT", "Thicknesses peer face for input meshes", GH_ParamAccess.tree);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            Brep shell = null;
            double thickApex = 0;
            double thickSupp = 0;
            List<Mesh> meshes = new List<Mesh>();
            //int layerCount = 0;

            if (!DA.GetData(0, ref shell)) return;
            if (!DA.GetData(1, ref thickApex)) return;
            if (!DA.GetData(2, ref thickSupp)) return;
            DA.GetDataList(3, meshes);
            //if (!DA.GetData(3, ref layerCount)) return;

            double tolerance = Rhino.RhinoDoc.ActiveDoc.ModelAbsoluteTolerance;
            double thickExtra = thickSupp - thickApex;

            // get trim information from S, to re-apply to all with Brep.CreateTrimmedSurface
            BrepFace trimmedSurface = shell.Faces[0];
            NurbsSurface origSrf = trimmedSurface.ToNurbsSurface();
            Curve trimCurve = trimmedSurface.OuterLoop.To3dCurve();

            
            // get uv coordinates for corner curves midpoints (OBSOLETE, for SoftEdit)
            List<Point2d> suppUVs = new List<Point2d>();
            List<Point3d> suppPts = new List<Point3d>();
            SHELLScommon.GetShellEdges(shell, out List<Curve> corners, out _); //edges not used, but out _ was not working
            foreach (Curve corner in corners)
            {
                Point3d thickPt = corner.PointAtNormalizedLength(.5);
                suppPts.Add(thickPt);
                double maximumDistance = 1.0;
                origSrf.ToBrep().ClosestPoint (thickPt, out _, out _, out double s, out double t, maximumDistance, out _);
                suppUVs.Add(new Point2d(s, t));
            }
            

            // for simplification, use distance apex-support, assuming symmetry
            // get apex point

            Point3d apex = origSrf.PointAt (
                 origSrf.Domain(0).ParameterAt(.5),
                 origSrf.Domain(1).ParameterAt(.5));
            double semidiagonal = apex.DistanceTo(suppPts[0]);
            

            List<Brep> layers = new List<Brep>();

            List<double> layerFactors = new List<double> { .5, 0, -.5};


            //List<Point3d> pushedPTs = new List<Point3d>(); // TEST
            //for (int i = 0; i < layerCount; i++)
            foreach (double layerFactor in layerFactors)
            {
                //double layerFactor = (double)i / (layerCount - 1);
                NurbsSurface currSrf = new NurbsSurface (origSrf);

                // alternative to soft edit: moving surface control points // then mesh?

                
                // iterate surface control points in U and V
                for (int v = 0; v < currSrf.Points.CountV; v++)
                    for (int u = 0; u < currSrf.Points.CountU; u++)
                    {
                        Point3d currCtrlPt = currSrf.Points.GetControlPoint(u, v).Location;

                        // calculate extra thickness
                        //double positionFactor = currCtrlPt.DistanceTo(apex) / semidiagonal; //linear
                        double positionFactor = Math.Pow (currCtrlPt.DistanceTo(apex) / semidiagonal, 2); //quadratic

                        double currThickness = (thickApex + (thickExtra * positionFactor)) * layerFactor;

                        // move them
                        currSrf.Points.SetPoint(u, v, currCtrlPt + new Vector3d (0, 0, currThickness));
                    }

                Curve[] projectedTrimCurves = Curve.ProjectToBrep(trimCurve, currSrf.ToBrep(), new Vector3d(0, 0, -1), tolerance);
                Brep currSurfTrimmed = currSrf.ToBrep().Split(projectedTrimCurves, tolerance)[1];
                layers.Add(currSurfTrimmed);
            }

            // calculate thicknesses from meshes

            Brep topSrf = layers[0];
            Brep bottomSrf = layers[2];

            DataTree<double> thicknesses = new DataTree<double>();
            int currMesh = 0;
            foreach (Mesh mesh in meshes)
            {
                List<double> currMeshThicknesses = new List<double>();
                
                for (int i = 0; i<mesh.Faces.Count; i++)
                {
                    Point3d currPt = mesh.Faces.GetFaceCenter(i);
                    Point3d topPt = topSrf.ClosestPoint(currPt);
                    Point3d bottomPt = bottomSrf.ClosestPoint(currPt);
                    double currThickness = topPt.DistanceTo(bottomPt);
                    currMeshThicknesses.Add(currThickness);
                }

                // add to tree
                GH_Path currPath = new GH_Path(currMesh);
                thicknesses.AddRange(currMeshThicknesses, currPath);
                currMesh++;
            }




            DA.SetData(0, layers[0]);
            DA.SetData(1, layers[1]);
            DA.SetData(2, layers[2]);
            DA.SetDataTree(3, thicknesses);
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
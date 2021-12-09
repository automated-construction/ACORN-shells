using System;
using System.Collections.Generic;
using System.Linq;

using Grasshopper.Kernel;
using Rhino.Geometry;
using Rhino.Geometry.Intersect;

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
    public class VariableThicknessMesh : GH_Component
    {

        public VariableThicknessMesh()
          : base("Variable Thickness Mesh", "A:VarThickMesh",
              "Calculates mesh face heights form Variable Thickness Surface",
              "ACORN Shells", "  Structure")
        // adding spaces to category names as per https://www.grasshopper3d.com/forum/topics/change-order-of-plugin-sub-category-c 
        {
        }

        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddBrepParameter("Top surface", "T", "Shell top surface (extrados)", GH_ParamAccess.item);
            pManager.AddBrepParameter("Medial surface", "M", "Shell medial surface", GH_ParamAccess.item);
            pManager.AddBrepParameter("Bottom surface", "B", "Shell bottom surface (intrados)", GH_ParamAccess.item);
            pManager.AddMeshParameter("Shell meshes", "M", "Shell meshes to calculate thickness per face.", GH_ParamAccess.list);
            pManager.AddBooleanParameter("Quick?", "Q", "Quick measuring", GH_ParamAccess.item);

        }

        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddNumberParameter("Mesh thicknesses", "MT", "Thicknesses [cm] per face for input meshes", GH_ParamAccess.tree);
            //pManager.AddPointParameter("Top points", "TP", "Intersection points with surfaces (TEST)", GH_ParamAccess.tree); //TEST
            //pManager.AddPointParameter("Bottom points", "BP", "Intersection points with surfaces (TEST)", GH_ParamAccess.tree); //TEST
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            Brep topSrf = null;
            Brep medialSrf = null;
            Brep bottomSrf = null;
            List<Mesh> meshes = new List<Mesh>();
            bool quick = false;

            if (!DA.GetData(0, ref topSrf)) return;
            if (!DA.GetData(1, ref medialSrf)) return;
            if (!DA.GetData(2, ref bottomSrf)) return;
            if (!DA.GetDataList(3, meshes)) return;
            if (!DA.GetData(4, ref quick)) return;

            double tolerance = Rhino.RhinoDoc.ActiveDoc.ModelAbsoluteTolerance;

            // calculate thickness values per mesh face by vertically projecting face centers onto top and bottom layers
            // then multiply by cosine of vector angle between vertical and normal, since thickness in Karamba is normal to mesh

            //List<Point3d> pts = new List<Point3d>(); //TEST
            //DataTree<Point3d> topPts = new DataTree<Point3d>(); //TEST
            //DataTree<Point3d> bottomPts = new DataTree<Point3d>(); //TEST

            DataTree<double> thicknesses = new DataTree<double>();
            int currMesh = 0;
            foreach (Mesh mesh in meshes)
            {
                GH_Path currPath = new GH_Path(currMesh);
                
                for (int i = 0; i<mesh.Faces.Count; i++)
                {
                    Point3d currPt = mesh.Faces.GetFaceCenter(i);

                    // get point in medial surface and corresponding normal direction vector
                    medialSrf.ClosestPoint(currPt, out Point3d medialPt, out _, 
                        out double medialU, out double medialV, // UV coordinates 
                        double.PositiveInfinity, // docs: "Using a positive value of maximumDistance can substantially speed up the search"
                        out Vector3d medialNormal);

                    Point3d topPt = new Point3d();
                    Point3d bottomPt = new Point3d();

                    if (quick)
                    {
                        // projecting points takes a long time, evaluating surfaces instead
                        // since top and bottom surface are constructed by moving control points vertically, 
                        // evaluating points should return vertically aligned points
                        topPt = topSrf.Surfaces[0].PointAt(medialU, medialV);
                        bottomPt = bottomSrf.Surfaces[0].PointAt(medialU, medialV);
                    } 
                    else
                    {
                        // get distances by projecting points vertically onto top and bottom surfaces 
                        topPt = Intersection.ProjectPointsToBreps
                            (new List<Brep>() { topSrf }, new List<Point3d>() { medialPt }, Vector3d.ZAxis, tolerance)[0];
                        bottomPt = Intersection.ProjectPointsToBreps
                            (new List<Brep>() { bottomSrf }, new List<Point3d>() { medialPt }, -Vector3d.ZAxis, tolerance)[0];
                    }

                    // calculate distance
                    double currThickness = topPt.DistanceTo(bottomPt);
                    // adjusted to normal direction
                    currThickness = currThickness * Math.Cos(Vector3d.VectorAngle(Vector3d.ZAxis, medialNormal));
                    // add to tree
                    thicknesses.Add(currThickness, currPath);
                    //topPts.Add(topPt, currPath);
                    //bottomPts.Add(bottomPt, currPath);
                }

                currMesh++;
            }

            DA.SetDataTree(0, thicknesses);
            //DA.SetDataTree(1, topPts);
            //DA.SetDataTree(2, bottomPts);
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
            get { return new Guid("2719ab3a-f1e7-4a40-94db-624699be72e9"); }
        }
    }
}
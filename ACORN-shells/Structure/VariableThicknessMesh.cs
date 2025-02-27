﻿using System;
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
          : base("Variable Thickness Mesh", "VarThickMesh",
              "Calculates mesh face heights form Variable Thickness Shell",
              "ACORN Shells", "  Structure")
        // adding spaces to category names as per https://www.grasshopper3d.com/forum/topics/change-order-of-plugin-sub-category-c 
        {
        }

        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddBrepParameter("Top surface", "T", "Shell top surface (extrados)", GH_ParamAccess.list);
            pManager.AddBrepParameter("Medial surface", "M", "Shell medial surface", GH_ParamAccess.list);
            pManager.AddBrepParameter("Bottom surface", "B", "Shell bottom surface (intrados)", GH_ParamAccess.list);
            pManager.AddMeshParameter("Shell meshes", "M", "Shell meshes to calculate thickness per face.", GH_ParamAccess.list);
            pManager.AddBooleanParameter("Quick?", "Q", "Quick measuring (only works for smooth surfaces)", GH_ParamAccess.item);

            pManager[4].Optional = true;
        }

        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddNumberParameter("Mesh thicknesses", "MT", "Thicknesses [cm] per face for input meshes", GH_ParamAccess.tree);
            //pManager.AddPointParameter("Top points", "TP", "Intersection points with surfaces (TEST)", GH_ParamAccess.tree); //TEST
            //pManager.AddPointParameter("Bottom points", "BP", "Intersection points with surfaces (TEST)", GH_ParamAccess.tree); //TEST
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            List<Brep> topSrfs = new List<Brep>();
            List<Brep> medialSrfs = new List<Brep>();
            List<Brep> bottomSrfs = new List<Brep>();
            
            List<Mesh> meshes = new List<Mesh>();
            bool quick = false; 

            if (!DA.GetDataList(0, topSrfs)) return;
            if (!DA.GetDataList(1, medialSrfs)) return;
            if (!DA.GetDataList(2, bottomSrfs)) return;
            if (!DA.GetDataList(3, meshes)) return;
            DA.GetData(4, ref quick);

            double tolerance = Rhino.RhinoDoc.ActiveDoc.ModelAbsoluteTolerance;

            // untrims first brep - should work for both single and segmented surfaces 
            Surface topSrf = topSrfs[0].Faces[0].UnderlyingSurface();
            Surface medialSrf = medialSrfs[0].Faces[0].UnderlyingSurface();
            Surface bottomSrf = bottomSrfs[0].Faces[0].UnderlyingSurface();

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

                    //  IF medialSrf is SURFACE: get point in medial surface and corresponding normal direction vector
                    medialSrf.ClosestPoint(currPt, out double medialU, out double medialV);
                    Point3d medialPt = medialSrf.PointAt(medialU, medialV);
                    Vector3d medialNormal = medialSrf.NormalAt(medialU, medialV);

                    // IF medialSrf is BREP: get point in medial surface and corresponding normal direction vector
                    // Brep.ClosestPoint outputs the same as Surface.PointAt and Surface.NormalAt
                    /*
                    medialSrf.ClosestPoint(currPt, out Point3d medialPt, out _,
                        out double medialU, out double medialV, // UV coordinates 
                        double.PositiveInfinity, // docs: "Using a positive value of maximumDistance can substantially speed up the search"
                        out Vector3d medialNormal);
                    */

                    Point3d topPt = new Point3d();
                    Point3d bottomPt = new Point3d();

                    //topPt = topSrf.PointAt(medialU, medialV);
                    //bottomPt = bottomSrf.PointAt(medialU, medialV);

                    
                    if (quick)
                    {
                        // projecting points takes a long time, evaluating surfaces instead - only works on single surfaced breps
                        // since top and bottom surface are constructed by moving control points vertically, 
                        // evaluating points should return vertically aligned points
                        topPt = topSrf.PointAt(medialU, medialV);
                        bottomPt = bottomSrf.PointAt(medialU, medialV);
                    } 
                    else
                    {
                        // get distances by projecting points vertically onto top and bottom surfaces 
                        topPt = Intersection.ProjectPointsToBreps
                            (topSrfs, new List<Point3d>() { medialPt }, Vector3d.ZAxis, tolerance)[0];
                        bottomPt = Intersection.ProjectPointsToBreps
                            (bottomSrfs, new List<Point3d>() { medialPt }, -Vector3d.ZAxis, tolerance)[0];
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
                return ACORN_shells.Properties.Resources.varThickMesh;
            }
        }

        public override Guid ComponentGuid
        {
            get { return new Guid("2719ab3a-f1e7-4a40-94db-624699be72e9"); }
        }
    }
}
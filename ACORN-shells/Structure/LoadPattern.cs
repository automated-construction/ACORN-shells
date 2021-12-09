using System;
using System.Collections.Generic;

using Grasshopper;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;
using Rhino.Geometry;
using Rhino.Geometry.Collections;
using Karamba.GHopper.Geometry;
using Karamba.Geometry;
using Karamba.GHopper.Loads;
using Karamba.Utilities;
using Karamba.Loads;
using KarambaCommon;

namespace ACORN_shells
{
    public class LoadPattern : GH_Component
    {
        public LoadPattern()
          : base("Load Pattern", "A:LoadPattern",
              "Creates load pattern",
              "ACORN Shells", "  Structure")
        // adding spaces to category names as per https://www.grasshopper3d.com/forum/topics/change-order-of-plugin-sub-category-c 
        {
        }

        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddMeshParameter("Shell mesh", "SM", "Meshed shell.", GH_ParamAccess.list);
            pManager.AddNumberParameter("Arc", "A", "Arc", GH_ParamAccess.item);
            pManager.AddNumberParameter("Offset", "O", "Offset", GH_ParamAccess.item);
            pManager.AddIntegerParameter("Predefined", "D", "Predefined pattern (optional)", GH_ParamAccess.item);

            pManager[3].Optional = true;
        }

        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddGenericParameter("Load Pattern Tuple", "T", "Indexes for faces in the pattern", GH_ParamAccess.item);
            pManager.AddPointParameter("CheckPoints", "P", "Points for testing load patterns", GH_ParamAccess.list);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            List<Mesh> shellMeshes = new List<Mesh>();
            double arc = 0;
            double offset = 0;
            int predef = 0;

            if (!DA.GetDataList(0, shellMeshes)) return;
            if (!DA.GetData(1, ref arc)) return;
            if (!DA.GetData(2, ref offset)) return; 
            DA.GetData(3, ref predef);

            //GH_Structure<GH_Point> checkPointTree = new GH_Structure<GH_Point>();
            //GH_Structure<GH_Integer> indexTree = new GH_Structure<GH_Integer>();
            DataTree<Tuple<double, double>> loadPatternTuples= new DataTree<Tuple<double, double>> ();
            List<Point3d> checkPoints = new List<Point3d>();

            // if predefined pattern, overrides input arc and offset
            switch (predef)
            {
                case 1:
                    arc = Math.PI * 2.00;
                    offset = Math.PI * 2.00;
                    break;
                case 2:
                    arc = Math.PI * 1.00;
                    offset = Math.PI * 0.50;
                    break;
                case 3:
                    arc = Math.PI * 1.00;
                    offset = Math.PI * 0.25;
                    break;
                case 4:
                    arc = Math.PI * 0.50;
                    offset = Math.PI * 0.50;
                    break;
                case 5:
                    arc = Math.PI * 0.50;
                    offset = Math.PI * 0.25;
                    break;
                case 6:
                    arc = Math.PI * 0.25;
                    offset = Math.PI * 0.00;
                    break;
                case 7:
                    arc = Math.PI * 2.00;
                    offset = Math.PI * 0.00;
                    break;
                
            }

            // safety conditions
            if (arc <= 0) return;

            Tuple<double, double> loadPatternTuple = new Tuple<double,double> (arc, offset);

            // illustrate current pattern

            // determine shell center for relative polar coordinates of face centers FOR LIVELOAD
            BoundingBox shellBox = BoundingBox.Unset;
            foreach (Mesh shellMesh in shellMeshes)
                shellBox = BoundingBox.Union (shellBox, shellMesh.GetBoundingBox(false));
            Point3d shellCenter = shellBox.Center;


            for (int i = 0; i<shellMeshes.Count;i++)
            {
                Mesh shellMesh = shellMeshes[i];

                //GH_Path path = new GH_Path(i);


                MeshFaceList k3dMeshFaces = shellMesh.Faces;

                for (int faceIndex = 0; faceIndex < shellMesh.Faces.Count; faceIndex ++)
                {
                    // get face center polar coordinate
                    Point3d faceCenter = k3dMeshFaces.GetFaceCenter(faceIndex);

                    // ensures angle always positive, [0, 2Pi] 
                    Vector3d faceCenterOrientation = new Vector3d(faceCenter) - new Vector3d(shellCenter);
                    double faceAngle = Math.Atan2(faceCenterOrientation.Y, faceCenterOrientation.X);
                    if (faceAngle < 0) faceAngle += Math.PI * 2; 

                    // check if polar coordinate of face center is within load patterndomain, 
                    


                    if (AngleInPattern (faceAngle, loadPatternTuple))
                    {
                        //indexTree.Append(new GH_Integer(faceIndex), path);
                        checkPoints.Add(faceCenter);

                    }

                }
            }
            
            
            DA.SetData(0, loadPatternTuple);
            DA.SetDataList(1, checkPoints);
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
            get { return new Guid("fe8a728b-4aba-430f-a614-e9b2ef20cf4f"); }
        }

        public bool AngleInPattern(double angle, Tuple<double, double> loadPatternTuple)
        {
            double arc = loadPatternTuple.Item1;
            double offset = loadPatternTuple.Item2;
            double start = offset;
            double end;
            while (start < Math.PI * 2) // limits verification to one "lap"
            {
                end = start + arc;
                // angle within interval
                if ( (angle >= start && angle <= end) || (angle <= offset - arc)) 
                    return true;
                start += (2 * arc);
            }
            return false;
        }
    }
}
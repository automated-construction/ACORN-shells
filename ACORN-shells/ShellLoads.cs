using System;
using System.Collections.Generic;

using Grasshopper.Kernel;
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
    public class ShellLoads : GH_Component
    {
        // BUG: Analysing in this file causes a crash. Output unanalysed model instead.

        // Default material properties
        // NOTE: The units are based on kN for force and document units for length
        // Assume the document is in meters
        double E = 35000000;
        double G_12 = 12920000;
        double G_3 = 12920000;
        double DENSITY = 25;
        double F_Y = 25000;
        double ALPHA_T = 0.00001;

        double DL_FACTOR = 1.35; // deal load design safety factor        
        double LL_FACTOR = 1.50; // deal load design safety factor

        public ShellLoads()
          : base("ShellLoads", "A:ShellLoads",
              "Create loads for Karamba FEA.",
              "ACORN", "Shells")
        {
        }

        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddMeshParameter("Shell mesh", "SM", "Meshed shell.", GH_ParamAccess.item);
            pManager.AddNumberParameter("Dead Load value", "DLV", "Dead Load value [kN/m2].", GH_ParamAccess.item);
            pManager.AddNumberParameter("Live Load value", "LLV", "Live Load value [kN/m2].", GH_ParamAccess.item);
            pManager.AddIntegerParameter("Live Load pattern", "LLP", "Load pattern.", GH_ParamAccess.item);


            //pManager[3].Optional = true;
        }

        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddGenericParameter("Loads", "L", "Loads for Karamba3D model.", GH_ParamAccess.list);
            pManager.AddPointParameter("CheckPoints", "CP", "Points for testing load patterns", GH_ParamAccess.list);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            Mesh shellMesh = null;
            double deadLoadValue = 0;
            double liveLoadValue = 0;
            int loadPattern = 0;

            if (!DA.GetData(0, ref shellMesh)) return;
            if (!DA.GetData(1, ref deadLoadValue)) return;
            if (!DA.GetData(2, ref liveLoadValue)) return;
            if (!DA.GetData(3, ref loadPattern)) return;

            //var fileTol = Rhino.RhinoDoc.ActiveDoc.ModelAbsoluteTolerance;
            //var logger = new Karamba.Utilities.MessageLogger();
            //Toolkit k3dKit = new KarambaCommon.Toolkit();
            var k3dFL = new KarambaCommon.Factories.FactoryLoad();

            // convert Rhino.Geometry.Mesh to Karamba.IMesh + stuff
            Mesh3 k3dMesh = shellMesh.Convert();
            UnitsConversionFactory ucf = UnitsConversionFactories.Conv();
            UnitConversion m = ucf.m();
            Mesh3 baseMesh = m.toBaseMesh(k3dMesh);

            // Create Karamba loads
            List<Load> k3dLoads = new List<Karamba.Loads.Load>();

            // Gravitational load
            Load gravity = new GravityLoad(new Vector3(0, 0, -1 * DL_FACTOR));
            k3dLoads.Add(gravity);

            // Dead load mesh load
            // same as Loads component > MeshLoad Const            
            //MeshLoad deadLoad = k3dKit.Load.MeshLoad(new List<Vector3>() { new Vector3(0, 0, -(deadLoadValue * DL_FACTOR)) }, baseMesh); // missing LoadOrientation.proj
            MeshLoad deadLoad = k3dFL.MeshLoad(new List<Vector3>() { new Vector3(0, 0, -(deadLoadValue * DL_FACTOR)) }, baseMesh, LoadOrientation.proj);
            k3dLoads.Add(deadLoad);

            // Live load mesh load, based on asymmetrical load pattern

            // get angles included in pattern
            // get shell center for plar coordinates

            // add active stresses per face
            List<Vector3> liveLoadVectors = new List<Vector3>();
            MeshFaceList k3dMeshFaces = shellMesh.Faces;

            // detemine shell center for relative polar coordinates of face centers
            Point3d shellCenter = shellMesh.GetBoundingBox(false).Center;

            // for testing loadPatterns
            List<Point3d> checkPoints = new List<Point3d>();

            for (int faceIndex = 0; faceIndex < shellMesh.Faces.Count; faceIndex ++)
            {
                // get face center polar coordinate
                Point3d faceCenter = k3dMeshFaces.GetFaceCenter(faceIndex);
                Vector3d faceCenterOrientation = new Vector3d(faceCenter) - new Vector3d(shellCenter);
                double faceAngle = Math.Atan2(faceCenterOrientation.Y, faceCenterOrientation.X);
                if (faceAngle < 0) faceAngle += Math.PI * 2; // ensures angle always positive, [0, 2Pi] 

                // check if polar coordinate of face center is within load patterndomain,                
                if (AngleInPattern (faceAngle, loadPattern))
                {
                    liveLoadVectors.Add(new Vector3(0, 0, -(liveLoadValue * LL_FACTOR)));
                    checkPoints.Add(faceCenter); // for testing - remove in the end
                }
                    
                else
                    liveLoadVectors.Add(Vector3.Zero);
            }

            MeshLoad liveLoad = k3dFL.MeshLoad(liveLoadVectors, baseMesh, LoadOrientation.proj);
            k3dLoads.Add(liveLoad);

            // convert from Karamba Loads to Karamba.GHopper Loads
            // might convert when created...
            List<GH_Load> ghLoads = new List<GH_Load>();
            foreach (Load k3dLoad in k3dLoads)
                ghLoads.Add (new Karamba.GHopper.Loads.GH_Load(k3dLoad));
            
            DA.SetDataList(0, ghLoads);
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
            get { return new Guid("4e59b772-fbd4-4253-9d1b-c4c4b5b841af"); }
        }

        public bool AngleInPattern(double angle, int loadPattern)
        {
            // initialize for pattern 1
            double arc = Math.PI * 2;
            double offset = Math.PI * 2;

            switch (loadPattern)
            {
                case 1:
                    arc =    Math.PI * 2.00;
                    offset = Math.PI * 2.00;
                    break;
                case 2:
                    arc =    Math.PI * 1.00;
                    offset = Math.PI * 0.50;
                    break;
                case 3:
                    arc =    Math.PI * 1.00;
                    offset = Math.PI * 0.25;
                    break;
                case 4:
                    arc =    Math.PI * 0.50;
                    offset = Math.PI * 0.50;
                    break;
                case 5:
                    arc =    Math.PI * 0.50;
                    offset = Math.PI * 0.25;
                    break;
                case 6:
                    arc =    Math.PI * 0.25;
                    offset = Math.PI * 0.00;
                    break;
                case 7:
                    arc =    Math.PI * 2.00;
                    offset = Math.PI * 0.00;
                    break;
            }

            double start = offset;
            double end;
            while (start < Math.PI * 2) // limits verification to one "lap"
            {
                end = start + arc;
                if (angle >= start && angle <= end) // angle within interval
                    return true;
                start += (2 * arc);
            }
            return false;
        }
    }
}
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

        double DL_FACTOR = 1.35; // dead load design safety factor        
        double LL_FACTOR = 1.50; // live load design safety factor

        public ShellLoads()
          : base("Shell Loads", "ShellLoads",
              "Create loads for Karamba FEA.",
              "ACORN Shells", " Analysis")
        // adding spaces to category names as per https://www.grasshopper3d.com/forum/topics/change-order-of-plugin-sub-category-c 
        {
        }

        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddMeshParameter("Shell mesh", "SM", "Meshed shell.", GH_ParamAccess.list);
            pManager.AddNumberParameter("Dead Load value", "DLV", "Dead Load value [kN/m2].", GH_ParamAccess.item);
            pManager.AddNumberParameter("Live Load value", "LLV", "Live Load value [kN/m2].", GH_ParamAccess.item);
            pManager.AddGenericParameter("Live Load pattern", "LLP", "Load pattern.", GH_ParamAccess.list);
            //pManager.AddNumberParameter("Area adjustment factors", "AAF", "Area adjustment factors for testing gap", GH_ParamAccess.list); //TEST for area adjustements
            pManager.AddBooleanParameter("Load State", "ULS?", " True: ULS, apply safety factors; False: SLS, do not apply safety factors", GH_ParamAccess.item);


            //pManager[4].Optional = true;
        }

        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddGenericParameter("Loads", "L", "Loads for Karamba3D model.", GH_ParamAccess.list);
            pManager.AddPointParameter("CheckPoints", "CP", "Points for testing load patterns", GH_ParamAccess.tree);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            List<Mesh> shellMeshes = new List<Mesh>();
            double deadLoadValue = 0;
            double liveLoadValue = 0;
            List<Tuple<double, double>> loadPatterns = new List<Tuple<double, double>>();
            //List<int> loadPatterns = new List<int>(); // for multiple load patterns as load cases
            List<double> areaFactors= new List<double>();// TEST for area adjustements

            bool ULS = false; // if ULS, apply safety factors; else, SLS so do not apply

            if (!DA.GetDataList(0, shellMeshes)) return;
            if (!DA.GetData(1, ref deadLoadValue)) return;
            if (!DA.GetData(2, ref liveLoadValue)) return;
            if (!DA.GetDataList(3, loadPatterns)) return;
            //DA.GetDataList(4, areaFactors); //TEST for area adjustements
            if (!DA.GetData(4, ref ULS)) return;


            // Create Karamba loads
            List<Load> k3dLoads = new List<Karamba.Loads.Load>();
            GH_Structure <GH_Point> checkPointTree = new GH_Structure<GH_Point>();

            int loadCase = 0;
            foreach (Tuple<double, double> loadPattern in loadPatterns)
            {
                GH_Path path = new GH_Path(loadCase); // for checkPoints, for testing


                // Gravitational load

                // if area factors, average them out for gravity - 
                // should be done individually, but that makes the component complicated, needing volume and density
                // to be revised for variable thickness
                // OBSOLETE?
                double areaFactorGravity = 0;
                if (areaFactors.Count > 0)
                {
                    foreach (double areaFactor in areaFactors)
                        areaFactorGravity += areaFactor;
                    areaFactorGravity /= areaFactors.Count; 
                }
                else
                    areaFactorGravity = 1;

                // one for each load case
                Load gravity;
                if (ULS) 
                    gravity = new GravityLoad (new Vector3(0, 0, -1 * areaFactorGravity * DL_FACTOR));
                else
                    gravity = new GravityLoad (new Vector3(0, 0, -1 * areaFactorGravity));

                gravity.loadcase = loadCase;
                k3dLoads.Add(gravity);


                var k3dFL = new KarambaCommon.Factories.FactoryLoad();
                UnitsConversionFactory ucf = UnitsConversionFactories.Conv();
                UnitConversion m = ucf.m();

                // determine shell center for relative polar coordinates of face centers FOR LIVELOAD
                BoundingBox shellBox = BoundingBox.Unset;
                foreach (Mesh shellMesh in shellMeshes)
                    shellBox = BoundingBox.Union (shellBox, shellMesh.GetBoundingBox(false));
                Point3d shellCenter = shellBox.Center;

                //List<Point3d> checkPoints = new List<Point3d>(); // for testing loadPatterns FOR LIVELOAD

                //foreach (Mesh shellMesh in shellMeshes)
                for (int i = 0; i<shellMeshes.Count;i++)
                {
                    Mesh shellMesh = shellMeshes[i];

                    //TEST for area adjustements
                    if (areaFactors.Count > 0)
                    {
                        double areaFactor = areaFactors[i];
                        deadLoadValue *= areaFactor;
                        liveLoadValue *= areaFactor;
                    }

                    Mesh3 baseMesh = m.toBaseMesh(shellMesh.Convert());

                    //----------- Gravity?


                    //----------- Dead load mesh load - same as Loads component > MeshLoad Const   

                    MeshLoad deadLoad;
                    if (ULS)
                        deadLoad = k3dFL.MeshLoad(new List<Vector3>() { new Vector3(0, 0, -(deadLoadValue * DL_FACTOR)) }, baseMesh, LoadOrientation.proj);
                    else
                        deadLoad = k3dFL.MeshLoad(new List<Vector3>() { new Vector3(0, 0, -(deadLoadValue)) }, baseMesh, LoadOrientation.proj);
                    deadLoad.loadcase = loadCase;
                    k3dLoads.Add(deadLoad);



                    //----------- Live load mesh load, based on asymmetrical load pattern

              
                    // get angles included in pattern
                    List<Vector3> liveLoadVectors = new List<Vector3>();
                    MeshFaceList k3dMeshFaces = shellMesh.Faces;

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
                            if (ULS)
                                liveLoadVectors.Add(new Vector3(0, 0, -(liveLoadValue * LL_FACTOR)));
                            else
                                liveLoadVectors.Add(new Vector3(0, 0, -(liveLoadValue)));
                            //checkPoints.Add(faceCenter); // for testing - remove in the end
                            checkPointTree.Append(new GH_Point (faceCenter), path);

                        }

                        else
                            liveLoadVectors.Add(Vector3.Zero);
                    }

                    MeshLoad liveLoad = k3dFL.MeshLoad(liveLoadVectors, baseMesh, LoadOrientation.proj);
                    liveLoad.loadcase = loadCase;
                    k3dLoads.Add(liveLoad);

                    // increments loadCase

                }
                loadCase++;
            }
            

            // convert from Karamba Loads to Karamba.GHopper Loads
            // might convert when created...
            List<GH_Load> ghLoads = new List<GH_Load>();
            foreach (Load k3dLoad in k3dLoads)
                ghLoads.Add (new Karamba.GHopper.Loads.GH_Load(k3dLoad));
            
            DA.SetDataList(0, ghLoads);
            DA.SetDataTree(1, checkPointTree);
        }

        protected override System.Drawing.Bitmap Icon
        {
            get
            {
                return ACORN_shells.Properties.Resources.makeLoads;
            }
        }

        public override Guid ComponentGuid
        {
            get { return new Guid("4e59b772-fbd4-4253-9d1b-c4c4b5b841af"); }
        }

        public override GH_Exposure Exposure
        {
            get { return GH_Exposure.primary; }
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
                if ((angle >= start && angle <= end) || (angle <= offset - arc))
                    return true;
                start += (2 * arc);
            }
            return false;
        }
    }
}
using System;
using System.Collections.Generic;
using System.Linq;

using Rhino.Geometry;
using Rhino.Geometry.Collections;
using Grasshopper;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Types;
using Grasshopper.Kernel.Data;

using Karamba.GHopper.Geometry;
using Karamba.Geometry;
using Karamba.Elements;
using Karamba.Supports;
using Karamba.Materials;
using Karamba.CrossSections;
using Karamba.GHopper.Elements;
using Karamba.GHopper.Supports;
using Karamba.GHopper.Materials;

namespace ACORN_shells
{
    public class MakeShell : GH_Component
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

        public MakeShell()
          : base("Make Karamba Shell Element", "A:MakeShell",
              "Create Karamba Shell element for analysis.",
              "ACORN Shells", "Analysis")
        {
        }

        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddBrepParameter("Shell surface", "S", "Shell surface.", GH_ParamAccess.item);
            pManager.AddMeshParameter("Meshes", "M", "Shell mesh(es).", GH_ParamAccess.list);
            pManager.AddNumberParameter("Thickness", "T", "Thickness(es) of shell.", GH_ParamAccess.tree);
            pManager.AddGenericParameter("Material", "MAT", "Shell material. Default is concrete.", GH_ParamAccess.list);
            pManager.AddBooleanParameter("FixedSupport", "F", "True = fixed supports; False (default) = pinned supports.", GH_ParamAccess.item); // to remove?
            pManager.AddBooleanParameter("Oriented support", "O", "Oriented support", GH_ParamAccess.item); // to remove?

            pManager[3].Optional = true;
            pManager[4].Optional = true;
            pManager[5].Optional = true;
        }

        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddGenericParameter("Shell Elements", "E", "Shell elements for Karamba", GH_ParamAccess.list);
            pManager.AddGenericParameter("Shell Supports", "S", "Shell supports for Karamba", GH_ParamAccess.list);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            Brep shell = null;
            List<Mesh> meshes = new List<Mesh>();
            //GH_Structure <GH_Mesh> ghMeshes = new GH_Structure<GH_Mesh>();
            //List<double> thicknesses = new List<double>();
            GH_Structure<GH_Number> ghThicknesses = new GH_Structure<GH_Number>();
            List<GH_FemMaterial> ghMats = new List<GH_FemMaterial>();
            bool fixedSupport = false;
            bool orientedSupport = false;

            if (!DA.GetData(0, ref shell)) return;
            if (!DA.GetDataList(1, meshes)) return;
            if (!DA.GetDataTree(2, out ghThicknesses)) return;
            DA.GetDataList(3, ghMats);
            DA.GetData(4, ref fixedSupport);
            DA.GetData(5, ref orientedSupport);

            var fileTol = Rhino.RhinoDoc.ActiveDoc.ModelAbsoluteTolerance; // for extracting supports
            var logger = new Karamba.Utilities.MessageLogger();
            var k3dKit = new KarambaCommon.Toolkit();



            // -------------- MATERIALS: accepts multiple materials for each segment
            FemMaterial k3dDefaultMaterial = k3dKit.Material.IsotropicMaterial("CONC", "CONC", E, G_12, G_3, DENSITY, F_Y, ALPHA_T);
            List<FemMaterial> k3dMaterials = new List<FemMaterial>(); // to match meshes list, works if only one mesh

            switch (ghMats.Count)
            {
                case 0:
                    // use default material
                    for (int i = 0; i < meshes.Count; i++)
                        k3dMaterials.Add(k3dDefaultMaterial);
                    break;
                case 1:
                    for (int i = 0; i < meshes.Count; i++)
                        k3dMaterials.Add(ghMats[0].Value);
                    break;
                default:
                    foreach (GH_FemMaterial ghMat in ghMats) // assuming that number of materials and number of meshes match 
                        k3dMaterials.Add (ghMat.Value);
                    break;
            }


            // --------------- CROSS SECTION

            // -------------- CROSS SECTION: decision tree based on number of materials and thicknesses

            //CroSec_Shell k3dSingleSection = null;
            List<CroSec_Shell> k3dSections = new List<CroSec_Shell>();

            bool singleThickness = (ghThicknesses.FlattenData().Count == 1); // constant thickness for whole shell
            bool constantThickness = (ghThicknesses.FlattenData().Count == meshes.Count); // constant thickness per segment
            bool variableThickness = !(singleThickness || constantThickness); // variable thickness per segment

            if (singleThickness)
                for (int i = 0; i < k3dMaterials.Count; i++)
                    k3dSections.Add(k3dKit.CroSec.ShellConst(ghThicknesses.FlattenData()[0].Value, 0, k3dMaterials[i], "SHELL", "SHELL", ""));

            if (constantThickness)
                for (int i = 0; i < k3dMaterials.Count; i++)
                    k3dSections.Add(k3dKit.CroSec.ShellConst(ghThicknesses.FlattenData()[i].Value, 0, k3dMaterials[i], "SHELL", "SHELL", ""));

            if (variableThickness)
                for (int i = 0; i < k3dMaterials.Count; i++)
                {
                    List<GH_Number> ghSegmentThicknesses = ghThicknesses.get_Branch(new GH_Path(i)) as List<GH_Number>; // needs to be converted into doubles
                    List<double> segmentThicknesses = new List<double>();
                    foreach (GH_Number number in ghSegmentThicknesses)
                        segmentThicknesses.Add(number.Value);
                    k3dSections.Add (new CroSec_Shell ("", "", "", null, new List<FemMaterial> { k3dMaterials[i] }, new List<double>() { 0 }, segmentThicknesses));
                }


            //------------------- SHELL ELEMENTS
            // Create shell element
            List<BuilderShell> k3dShells = new List<BuilderShell>();

            for (int i = 0; i < meshes.Count; i++)
            {
                Mesh mesh = meshes[i];
                var k3dShell = k3dKit.Part.MeshToShell(new List<Mesh3>() { mesh.Convert() },
                    new List<string>() { "ACORNSHELL" },
                    new List<CroSec>() { k3dSections[i] },
                    logger, out _);

                k3dShells.AddRange(k3dShell);

            }


            // ------------- SUPPORTS

            // extract shell corners
            SHELLScommon.GetShellEdges(shell, out List<Curve> corners, out _); // discarding shell edges


            // Fixed support
            List<Support> k3dSupports = new List<Support>();
            foreach (var c in corners) 
            {

                // find mesh that is closest to corner

                Point3d cornerCenter = c.PointAtNormalizedLength(0.5);
                Mesh cornerMesh = meshes[0];
                double bestDistance = 10000000;
                foreach (Mesh currMesh in meshes)
                {
                    double currDistance = cornerCenter.DistanceTo(currMesh.GetBoundingBox(false).Center);
                    if (currDistance < bestDistance)
                    {
                        cornerMesh = currMesh;
                        bestDistance = currDistance;
                    }
                }

                Plane supportOrientation = Plane.WorldXY;
                if (orientedSupport)
                {
                    // determine support orientation plane (for straight support line; consider curved in the future)
                    Vector3d supportXAxis = c.TangentAt(0.5);
                    Vector3d supportYAxis = Vector3d.CrossProduct(Vector3d.ZAxis, supportXAxis);
                    supportOrientation = new Plane(cornerCenter, supportXAxis, supportYAxis);
                }


                // filter corner mesh for vertices on naked edge
                //Polyline[] cornerMeshNakedEdges = cornerMesh.GetNa

                bool fixedRotation = fixedSupport;

                // custom tolerance for finding support points // could be out of the loop
                MeshTopologyEdgeList meshEdges = cornerMesh.TopologyEdges;
                List<double> edgeLengths = new List<double>();
                for (int i = 0; i < meshEdges.Count; i++)
                    edgeLengths.Add(meshEdges.EdgeLine(i).Length);
                double customTol = edgeLengths.Average() * 0.10;


                // find vertices in corner mesh on the corner edge
                Point3d[] cornerMeshVertices = cornerMesh.Vertices.ToPoint3dArray();

                // for performance, only use vertices on naked edges!
                Point3d[] cornerMeshEdgeVertices = new Point3d[cornerMeshVertices.Length];
                bool[] cornerMeshEdgePointStatus = cornerMesh.GetNakedEdgePointStatus();
                for (int i = 0; i < cornerMeshVertices.Length; i++)
                    if (cornerMeshEdgePointStatus[i]) 
                        cornerMeshEdgeVertices[i] = cornerMeshVertices[i];

                // remove duplicate mesh vertices
                //Point3d[] uniqueMeshVertices = Point3d.CullDuplicates(cornerMeshVertices, customTol);
                Point3d[] uniqueMeshVertices = Point3d.CullDuplicates(cornerMeshEdgeVertices, customTol);
                foreach (Point3d v in uniqueMeshVertices)
                {
                    var test = c.ClosestPoint(v, out _, customTol);
                    if (test)
                    {
                        if (orientedSupport)
                            k3dSupports.Add(k3dKit.Support.Support(v.Convert(), new bool[] { false, true, true, fixedRotation, fixedRotation, fixedRotation }, supportOrientation.Convert()));
                        else
                            k3dSupports.Add(k3dKit.Support.Support(v.Convert(), new bool[] { true, true, true, fixedRotation, fixedRotation, fixedRotation }));

                        //break;
                    }
                }
            }

            // convert from Karamba lists to Karamba.GHopper lists
            // might convert when created...
            // do this at creation?

            List<GH_Element> ghElements = new List<GH_Element>();
            foreach (BuilderShell k3dShell in k3dShells)
                ghElements.Add(new GH_Element(k3dShell));

            List<GH_Support> ghSupports = new List<GH_Support>();
            foreach (Support k3dSupport in k3dSupports)
                ghSupports.Add(new GH_Support(k3dSupport));

            DA.SetDataList(0, ghElements);
            DA.SetDataList(1, ghSupports);
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
            get { return new Guid("4fdecd89-c357-457e-ae71-e1100fd9660d"); }
        }
    }
}
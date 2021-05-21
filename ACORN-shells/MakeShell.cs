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
              "ACORN", "Shells")
        {
        }

        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddBrepParameter("Shell surface", "S", "Shell surface.", GH_ParamAccess.item);
            pManager.AddMeshParameter("Meshes", "M", "Shell mesh(es).", GH_ParamAccess.list);
            pManager.AddNumberParameter("Thickness", "T", "Thickness(es) of shell.", GH_ParamAccess.list);
            pManager.AddGenericParameter("Material", "MAT", "Shell material. Default is concrete.", GH_ParamAccess.item);
            pManager.AddBooleanParameter("FixedSupport", "F", "True = fixed supports; False (default) = pinned supports.", GH_ParamAccess.item);

            pManager[3].Optional = true;
            pManager[4].Optional = true;
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
            //List<Curve> corners = new List<Curve>();
            List<double> thicknesses = new List<double>();
            //double thickness = 0;
            Karamba.GHopper.Materials.GH_FemMaterial ghMat = null;
            bool fixedSupport = false;

            if (!DA.GetData(0, ref shell)) return;
            if (!DA.GetDataList(1, meshes)) return;
            if (!DA.GetDataList(2, thicknesses)) return;
            DA.GetData(3, ref ghMat);
            DA.GetData(4, ref fixedSupport);

            var fileTol = Rhino.RhinoDoc.ActiveDoc.ModelAbsoluteTolerance;
            var logger = new Karamba.Utilities.MessageLogger();
            var k3dKit = new KarambaCommon.Toolkit();

            // Make a default concrete material for Karamba
            Karamba.Materials.FemMaterial k3dMaterial = null;

            if (ghMat == null)
                k3dMaterial = k3dKit.Material.IsotropicMaterial("CONC", "CONC", E, G_12, G_3, DENSITY, F_Y, ALPHA_T);
            else
                k3dMaterial = ghMat.Value;

            // Cross section (predefined, allow input) OR component to make shell
            Karamba.CrossSections.CroSec_Shell k3dSection = null;
            if (thicknesses.Count == 1)
                k3dSection = k3dKit.CroSec.ShellConst(thicknesses[0], 0, k3dMaterial, "SHELL", "SHELL", "");
            else
                k3dSection = new Karamba.CrossSections.CroSec_Shell
                    ("", "", "", null, new List<Karamba.Materials.FemMaterial> { k3dMaterial }, new List<double>() { 0 }, thicknesses);

            // Create shell element
            //var k3dNodes = new List<Point3>();
            List<BuilderShell> k3dShells = new List<BuilderShell>();
            foreach (Mesh shellMesh in meshes)
            {
                var k3dShell = k3dKit.Part.MeshToShell(new List<Mesh3>() { shellMesh.Convert() },
                    new List<string>() { "ACORNSHELL" },
                    new List<Karamba.CrossSections.CroSec>() { k3dSection },
                    logger, out _);

                k3dShells.AddRange(k3dShell);
            }




            // extract shell corners
            SHELLScommon.GetShellEdges(shell, out List<Curve> corners, out _); // discarding shell edges


            // Fixed support
            List<Support> k3dSupports = new List<Support>();
            foreach (var c in corners) 
            {
                // find mesh that is closest to corner

                Point3d cornerCenter = c.PointAtNormalizedLength(0.5);
                Mesh cornerMesh;
                //int meshIndex;
                //bool found = c.ClosestPoints(meshes, out _, out _, out meshIndex, 0.05);
                //cornerMesh = meshes[meshIndex];

                cornerMesh = meshes[0];
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

                foreach (var v in cornerMesh.Vertices)
                {
                    var test = c.ClosestPoint(v, out _, fileTol);
                    if (test)
                    {
                        if (fixedSupport)
                            k3dSupports.Add(k3dKit.Support.Support(v.Convert(), new bool[] { true, true, true, true, true, true }));
                        else
                            k3dSupports.Add(k3dKit.Support.Support(v.Convert(), new bool[] { true, true, true, false, false, false }));

                        //break;
                    }
                }
            }

            // convert from Karamba Loads to Karamba.GHopper Loads
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
using System;
using System.Collections.Generic;
using System.Linq;

using Grasshopper.Kernel;
using Rhino.Geometry;
using Karamba.GHopper.Geometry;
using Karamba.Geometry;
using Karamba.GHopper.Loads;

namespace ACORN_shells
{
    public class PreliminaryModel : GH_Component
    {

        // Default material properties
        // NOTE: The units are based on kN for force and document units for length
        // Assume the document is in meters
        double E = 35000000;
        double G_12 = 12920000;
        double G_3 = 12920000;
        double DENSITY = 25;
        double F_Y = 25000;
        double ALPHA_T = 0.00001;

        public PreliminaryModel()
          : base("PreliminaryModel", "A:PreliminaryModel",
              "Create preliminary shell analysis Karamba3D model for segmentation purposes.",
              "ACORN Shells", "Segmentation")
        {
        }

        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddBrepParameter("Shell surface", "S", "Shell surface.", GH_ParamAccess.item);
            pManager.AddMeshParameter("Mesh", "M", "Meshed shell.", GH_ParamAccess.item);
        }

        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddGenericParameter("Model", "M", "Karamba3D model.", GH_ParamAccess.item);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            Brep shell = null;
            Mesh mesh = null;

            if (!DA.GetData(0, ref shell)) return;
            if (!DA.GetData(1, ref mesh)) return;

            var fileTol = Rhino.RhinoDoc.ActiveDoc.ModelAbsoluteTolerance;
            var logger = new Karamba.Utilities.MessageLogger();
            var k3dKit = new KarambaCommon.Toolkit();



            // Make a default concrete material for Karamba
            Karamba.Materials.FemMaterial k3dMaterial = null;
            k3dMaterial = k3dKit.Material.IsotropicMaterial("CONC", "CONC", E, G_12, G_3, DENSITY, F_Y, ALPHA_T);

            // Cross section (predefined, allow input) OR component to make shell
            // Use default thickness 1/100 of span
            double span = mesh.GetBoundingBox(false).Diagonal.X;
            double thickness = span / 100;
            Karamba.CrossSections.CroSec_Shell k3dSection = null;
            k3dSection = k3dKit.CroSec.ShellConst(thickness, 0, k3dMaterial, "SHELL", "SHELL", "");


            // Create shell element
            var k3dNodes = new List<Point3>();
            var k3dShell = k3dKit.Part.MeshToShell(new List<Mesh3>() { mesh.Convert() },
                new List<string>() { "ACORNSHELL" },
                new List<Karamba.CrossSections.CroSec>() { k3dSection },
                logger, out k3dNodes);

            // extract shell corners
            SHELLScommon.GetShellEdges(shell, out List<Curve> corners, out _); // discarding shell edges

            // Create supports
            var k3dSupports = new List<Karamba.Supports.Support>();
            foreach (var v in mesh.Vertices)
            {
                foreach (var c in corners)
                {
                    var test = c.ClosestPoint(v, out _, fileTol);
                    if (test)
                    {
                        k3dSupports.Add(k3dKit.Support.Support(v.Convert(), new bool[] { true, true, true, false, false, false }));
                        break;
                    }
                }
            }

            // Create loads
            List<Karamba.Loads.Load> k3dLoads = new List<Karamba.Loads.Load>();

            // Default gravitational load
            var k3dLoad = new Karamba.Loads.GravityLoad(new Vector3(0, 0, -1));
            k3dLoads.Add (k3dLoad);

            // Assemble model
            var k3dModel = k3dKit.Model.AssembleModel(
                k3dShell,
                k3dSupports,
                k3dLoads,
                out _, out _, out _, out _, out _);

            DA.SetData(0, new Karamba.GHopper.Models.GH_Model(k3dModel));
        }

        protected override System.Drawing.Bitmap Icon
        {
            get
            {
                return ACORN_shells.Properties.Resources.prelim;
            }
        }

        public override Guid ComponentGuid
        {
            get { return new Guid("0c7a26eb-e043-438b-8989-08f471669a99"); }
        }
    }
}
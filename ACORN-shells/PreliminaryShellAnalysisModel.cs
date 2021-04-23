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
    public class PreliminaryShellAnalysisModel : GH_Component
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

        public PreliminaryShellAnalysisModel()
          : base("PreliminaryShellAnalysisModel", "A:PreliminaryShellAnalysisModel",
              "Create preliminary shell analysis Karamba3D model with gravitational loads.",
              "ACORN", "Shells")
        {
        }

        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddBrepParameter("Shell surface", "S", "Shell surface.", GH_ParamAccess.item);
            pManager.AddMeshParameter("Mesh", "M", "Meshed shell.", GH_ParamAccess.item);
            //pManager.AddCurveParameter("Corners", "C", "Support curves.", GH_ParamAccess.list);
            pManager.AddNumberParameter("Thickness", "T", "Thickness(es) of shell.", GH_ParamAccess.list);
            pManager.AddGenericParameter("Material", "MAT", "Shell material. Default is concrete.", GH_ParamAccess.item);
            pManager.AddGenericParameter("Loads", "L", "Loads. Default is gravity (no safety factor).", GH_ParamAccess.list);
            pManager.AddBooleanParameter("FixedSupport", "F", "True = fixed supports; False (default) = pinned supports.", GH_ParamAccess.item);


            pManager[3].Optional = true;
            pManager[4].Optional = true;
            pManager[5].Optional = true;
        }

        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddGenericParameter("Model", "M", "Analysed Karamba3D model.", GH_ParamAccess.item);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            Brep shell = null;
            Mesh mesh = null;
            //List<Curve> corners = new List<Curve>();
            List<double> thicknesses = new List<double>();
            //double thickness = 0;
            Karamba.GHopper.Materials.GH_FemMaterial ghMat = null;
            List <Karamba.GHopper.Loads.GH_Load> ghLoads = new List<Karamba.GHopper.Loads.GH_Load>();
            bool fixedSupport = false;

            if (!DA.GetData(0, ref shell)) return;
            if (!DA.GetData(1, ref mesh)) return;
            //if (!DA.GetDataList(1, corners)) return;
            //if (!DA.GetData(2, ref thickness)) return;
            if (!DA.GetDataList(2, thicknesses)) return;
            DA.GetData(3, ref ghMat);
            DA.GetDataList(4, ghLoads);
            DA.GetData(5, ref fixedSupport);

            var fileTol = Rhino.RhinoDoc.ActiveDoc.ModelAbsoluteTolerance;
            var logger = new Karamba.Utilities.MessageLogger();
            var k3dKit = new KarambaCommon.Toolkit();

            // Make a default concrete material for Karamba
            Karamba.Materials.FemMaterial k3dMaterial = null;

            if (ghMat == null)
                k3dMaterial = k3dKit.Material.IsotropicMaterial("CONC", "CONC", E, G_12, G_3, DENSITY, F_Y, ALPHA_T);
            else
                k3dMaterial = ghMat.Value;

            // Cross section
            Karamba.CrossSections.CroSec_Shell k3dSection = null;
            if (thicknesses.Count == 1)
                k3dSection = k3dKit.CroSec.ShellConst(thicknesses[0], 0, k3dMaterial, "SHELL", "SHELL", "");
            else
                k3dSection = new Karamba.CrossSections.CroSec_Shell
                    ("", "", "", null, new List<Karamba.Materials.FemMaterial> { k3dMaterial }, new List<double>() { 0 }, thicknesses);

            // Create shell element
            var k3dNodes = new List<Point3>();
            var k3dShell = k3dKit.Part.MeshToShell(new List<Mesh3>() { mesh.Convert() },
                new List<string>() { "ACORNSHELL" },
                new List<Karamba.CrossSections.CroSec>() { k3dSection },
                logger, out k3dNodes);

            // extract shell corners
            SHELLScommon.GetShellEdges(shell, out List<Curve> corners, out _); // discarding shell edges



            // Fixed support
            var k3dSupports = new List<Karamba.Supports.Support>();
            foreach (var v in mesh.Vertices)
            {
                foreach (var c in corners)
                {
                    var test = c.ClosestPoint(v, out _, fileTol);
                    if (test)
                    {
                        if (fixedSupport)
                            k3dSupports.Add(k3dKit.Support.Support(v.Convert(), new bool[] { true, true, true, true, true, true }));
                        else
                            k3dSupports.Add(k3dKit.Support.Support(v.Convert(), new bool[] { true, true, true, false, false, false }));

                        break;
                    }
                }
            }

            // Create loads
            List<Karamba.Loads.Load> k3dLoads = new List<Karamba.Loads.Load>();

            // Default gravitational load
            var k3dLoad = new Karamba.Loads.GravityLoad(new Vector3(0, 0, -1));

            if (ghLoads.Count == 0)
                k3dLoads.Add (k3dLoad);
            else
                foreach (GH_Load ghLoad in ghLoads)
                    k3dLoads.Add(ghLoad.Value);

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
                return ACORN_shells.Properties.Resources.ACORN_24;
            }
        }

        public override Guid ComponentGuid
        {
            get { return new Guid("f4902d41-66de-4c47-9779-e254936d6320"); }
        }
    }
}
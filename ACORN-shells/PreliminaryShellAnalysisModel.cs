using System;
using System.Collections.Generic;

using Grasshopper.Kernel;
using Rhino.Geometry;
using Karamba.GHopper.Geometry;
using Karamba.Geometry;

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
        double F_C = -35000;
        double ALPHA_T = 0.00001;

        public PreliminaryShellAnalysisModel()
          : base("PreliminaryShellAnalysisModel", "A:PreliminaryShellAnalysisModel",
              "Create preliminary shell analysis Karamba3D model with gravitational loads.",
              "ACORN", "Shells")
        {
        }

        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddMeshParameter("Mesh", "M", "Meshed shell.", GH_ParamAccess.item);
            pManager.AddCurveParameter("Corners", "C", "Support curves.", GH_ParamAccess.list);
            pManager.AddNumberParameter("Thickness", "T", "Thickness of shell.", GH_ParamAccess.item);
            pManager.AddGenericParameter("Material", "MAT", "Shell material. Default is concrete.", GH_ParamAccess.item);

            pManager[3].Optional = true;
        }

        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddGenericParameter("Model", "M", "Analysed Karamba3D model.", GH_ParamAccess.item);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            Mesh mesh = null;
            List<Curve> corners = new List<Curve>();
            double thickness = 0;
            Karamba.GHopper.Materials.GH_FemMaterial ghMat = null;

            if (!DA.GetData(0, ref mesh)) return;
            if (!DA.GetDataList(1, corners)) return;
            if (!DA.GetData(2, ref thickness)) return;
            DA.GetData(3, ref ghMat);

            var fileTol = Rhino.RhinoDoc.ActiveDoc.ModelAbsoluteTolerance;
            var logger = new Karamba.Utilities.MessageLogger();
            var k3dKit = new KarambaCommon.Toolkit();

            // Make a default concrete material for Karamba
            Karamba.Materials.FemMaterial k3dMaterial = null;

            if (ghMat == null)
                k3dMaterial = k3dKit.Material.IsotropicMaterial("CONC", "CONC", E, G_12, G_3, DENSITY, F_Y, F_C, Karamba.Materials.FemMaterial.FlowHypothesis.mises, ALPHA_T);
            else
                k3dMaterial = ghMat.Value;

            // Cross section
            var k3dSection = k3dKit.CroSec.ShellConst(thickness, 0, k3dMaterial, "SHELL", "SHELL", "");

            // Create shell element
            var k3dNodes = new List<Point3>();
            var k3dShell = k3dKit.Part.MeshToShell(new List<Mesh3>() { mesh.Convert() },
                new List<string>() { "ACORNSHELL" },
                new List<Karamba.CrossSections.CroSec>() { k3dSection },
                logger, out k3dNodes);

            // Fixed support
            var k3dSupports = new List<Karamba.Supports.Support>();
            foreach (var v in mesh.Vertices)
            {
                foreach (var c in corners)
                {
                    var test = c.ClosestPoint(v, out _, fileTol);
                    if (test)
                    {
                        k3dSupports.Add(k3dKit.Support.Support(v.Convert(), new bool[] { true, true, true, true, true, true }));
                        break;
                    }
                }
            }

            // Gravitational load
            var k3dLoad = new Karamba.Loads.GravityLoad(new Vector3(0, 0, -1));

            // Assemble model
            var k3dModel = k3dKit.Model.AssembleModel(
                k3dShell,
                k3dSupports,
                new List<Karamba.Loads.Load>() { k3dLoad },
                out _, out _, out _, out _, out _);

            DA.SetData(0, new Karamba.GHopper.Models.GH_Model(k3dModel));
        }

        protected override System.Drawing.Bitmap Icon
        {
            get
            {
                return null;
            }
        }

        public override Guid ComponentGuid
        {
            get { return new Guid("f4902d41-66de-4c47-9779-e254936d6320"); }
        }
    }
}
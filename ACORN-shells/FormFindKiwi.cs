﻿using System;
using System.Collections.Generic;

using Grasshopper.Kernel;
using Rhino.Geometry;
using System.Linq;
using System.IO;

namespace ACORN_shells
{
    public class FormFindKiwi : GH_Component
    {
        // Kiwi3D parameters
        int MAT_CONC = 5;
        int SURF_DEGREE = 3;
        int SURF_SUBDIV = 10;
        int[] ANAL_OUTPUT = new int[] { 1, 2, 3 };

        Brep FormFoundShell = null;
        string KiwiErrors = "";

        public FormFindKiwi()
          : base("FormFindKiwi", "A:FormFindKiwi",
              "Use Kiwi3D to form-find the shell. Uses Kiwi3D v0.5.0.",
              "ACORN", "Shells")
        {
        }

        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddBrepParameter("PlanShell", "P", "Flat brep to brep to be form found.", GH_ParamAccess.item);
            pManager.AddCurveParameter("Corners", "C", "Support curves.", GH_ParamAccess.list);
            pManager.AddNumberParameter("Height", "H", "Target height of shell.", GH_ParamAccess.item);
            pManager.AddBooleanParameter("Run", "R", "Toggle to run analysis.", GH_ParamAccess.item);
        }

        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddBrepParameter("FormFoundShell", "S", "Form found shell.", GH_ParamAccess.item);
            pManager.AddTextParameter("KiwiErrors", "E", "Errors from Kiwi3D.", GH_ParamAccess.item);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            Brep planShell = null;
            List<Curve> corners = new List<Curve>();
            double height = 0;
            bool run = false;

            if (!DA.GetData(0, ref planShell)) return;
            if (!DA.GetDataList(1, corners)) return;
            if (!DA.GetData(2, ref height)) return;
            if (!DA.GetData(3, ref run)) return;

            if (run)
            {
                // Get all Kiwi3d components
                var componentNames = new List<string>() { "Kiwi3d.MaterialDefaults", "Kiwi3d.SurfaceRefinement", "Kiwi3d.ShellElement",
                    "Kiwi3d.SupportPoint", "Kiwi3d.SurfaceLoad", "Kiwi3d.LinearAnalysis", "Kiwi3d.AnalysisModel",
                    "Kiwi3d.IGASolver", "Kiwi3d.DeformedModel" };

                var componentInfos = componentNames.ToDictionary(x => x, x => Rhino.NodeInCode.Components.FindComponent(x));

                var missingComponents = componentInfos.Where(kvp => kvp.Value == null).Select(x => x.Key).ToList();
                if (missingComponents.Count > 0)
                {
                    throw new Exception("Cannot find component: " + String.Join(", ", missingComponents));
                }

                //var componentFuncs = new Dictionary<string, dynamic>();
                //foreach (var kvp in componentInfos)
                //    componentFuncs[kvp.Key] = kvp.Value.Delegate as dynamic;

                // Create temporary directory
                var tmpDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
                if (Directory.Exists(tmpDir))
                    Directory.Delete(tmpDir);
                Directory.CreateDirectory(tmpDir);

                // Define model
                var thickness = height * 0.1; // Use 10% of target height

                var kiwiMaterial = (CallComponent(componentInfos, "Kiwi3d.MaterialDefaults", new object[] { MAT_CONC })[0] as IList<object>)[0];
                var kiwiRefinement = (CallComponent(componentInfos, "Kiwi3d.SurfaceRefinement", new object[] {SURF_DEGREE,
                    SURF_DEGREE, SURF_SUBDIV, SURF_SUBDIV })[0] as IList<object>)[0];
                var kiwiShell = (CallComponent(componentInfos, "Kiwi3d.ShellElement", new object[] { planShell, kiwiMaterial,
                    thickness, kiwiRefinement, null, false })[0] as IList<object>)[0];

                // Pinned support at corners
                // Divide the curves into points since the support curve seems to not work as expected
                var kiwiSupports = new List<object>();
                foreach (var c in corners)
                {
                    Point3d[] points = new Point3d[0];
                    c.DivideByCount(100, true, out points);

                    foreach (var p in points)
                        kiwiSupports.Add((CallComponent(componentInfos, "Kiwi3d.SupportPoint", new object[] { p, true,
                            true, true, false, false })[0] as IList<object>)[0]);
                }

                // Uniform load pushing upwards
                var kiwiLoads = (CallComponent(componentInfos, "Kiwi3d.SurfaceLoad", new object[] { planShell, "1",
                    Vector3d.ZAxis, 100, null, null, 1 })[0] as IList<object>)[0];

                // Run Kiwi analysis
                var kiwiAnalOptions = (CallComponent(componentInfos, "Kiwi3d.LinearAnalysis", new object[] { ANAL_OUTPUT })[0] as IList<object>)[0];
                var kiwiModel = (CallComponent(componentInfos, "Kiwi3d.AnalysisModel",
                    new object[] { kiwiAnalOptions, kiwiShell, kiwiSupports, kiwiLoads })[0] as IList<object>)[0];
                var kiwiResult = CallComponent(componentInfos, "Kiwi3d.IGASolver", new object[] { kiwiModel, tmpDir, true });
                KiwiErrors = kiwiResult[1] == null ? "" : (kiwiResult[1] as IList<object>)[0] as string;
                var kiwiModelRes = (kiwiResult[0] as IList<object>)[0];

                // Scale deformed shell to target height
                FormFoundShell = (CallComponent(componentInfos, "Kiwi3d.DeformedModel", new object[] { kiwiModelRes })[1] as IList<object>)[0] as Brep;
                if (FormFoundShell != null)
                {
                    var bounds = FormFoundShell.GetBoundingBox(Plane.WorldXY);
                    var scale = height / (bounds.Max.Z - bounds.Min.Z);
                    FormFoundShell.Transform(Transform.Scale(new Plane(bounds.Min, Vector3d.ZAxis), 1, 1, scale));
                }
            }
            
            DA.SetData(0, FormFoundShell);
            DA.SetData(1, KiwiErrors);
        }

        private object[] CallComponent(Dictionary<string, Rhino.NodeInCode.ComponentFunctionInfo> components, string componentName, object[] args)
        {
            var tmpOut = new string[0];
            return components[componentName].Evaluate(args.ToList(), false, out tmpOut);
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
            get { return new Guid("76b43da6-2626-4157-a30e-22960c4d5f77"); }
        }
    }
}
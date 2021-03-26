using System;
using System.Collections.Generic;

using Grasshopper.Kernel;
using Rhino.Geometry;
using System.Linq;
using System.IO;
using System.Runtime.InteropServices.ComTypes;

namespace ACORN_shells
{
    public class FormFindMembrane : GH_Component
    {
        // Kiwi3D parameters
        int MAT_CONC = 5; // CONCRETE
        int MAT_ETFE = 4; // ETFE (Membrane)
        int MAT_STEEL = 1; // STEEL
        int CRV_DEGREE = 3;
        int CRV_SUBDIV = 8;
        int SURF_DEGREE = 3;
        int SURF_SUBDIV = 10;
        int[] ANAL_OUTPUT = new int[] { 1, 2, 3 };
        int FF_STEPS = 5; // make input?
        int FF_ITERS = 5; // make input?

        Brep FormFoundShell = null;
        List<Curve> FormFoundCorners = new List<Curve>();
        List<Curve> FormFoundEdges = new List<Curve>();
        string KiwiErrors = "";

        // bool SOLUTION_CHANGED = true; // for event handling fantasy

        // change name if it becomes only formfinding component
        public FormFindMembrane()
          : base("FormFindMembrane", "A:FormFindMembrane",
              "Use Kiwi3D to form-find the shell using membrane component. Uses Kiwi3D v0.5.0.",
              "ACORN", "Shells")
        {
        }

        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddBrepParameter("PlanShell", "P", "Flat brep to brep to be form found.", GH_ParamAccess.list);
            pManager.AddCurveParameter("Corners", "C", "Support curves.", GH_ParamAccess.list);
            pManager.AddCurveParameter("Edges", "E", "Edge curves.", GH_ParamAccess.list);
            pManager.AddNumberParameter("Height", "H", "Target height of shell.", GH_ParamAccess.item);
            pManager.AddNumberParameter("SubDiv", "SD", "Number of subdivisions of shell for analysis.", GH_ParamAccess.item);
            pManager.AddBooleanParameter("Run", "R", "Toggle to run analysis.", GH_ParamAccess.item);
            pManager.AddBooleanParameter("CurveSupports", "CS", "Use Corner curves as supports", GH_ParamAccess.item); // test
            pManager.AddBooleanParameter("ScaleDeform", "SD", "Use DefMod to scale, otherwise, uses Geomtry Scaling.", GH_ParamAccess.item); // test
            pManager.AddNumberParameter("ThicknessFactor", "TF", "Thickness = Shell Height * TF", GH_ParamAccess.item);
            pManager.AddNumberParameter("MembranePrestressU", "MPu", "Membrane prestress force [kN/m] in local u-direction", GH_ParamAccess.item);
            pManager.AddNumberParameter("MembranePrestressV", "MPv", "Membrane prestress force [kN/m] in local v-direction", GH_ParamAccess.item);
            pManager.AddNumberParameter("CablePrestressV", "CP", "Cable prestress force [kN]", GH_ParamAccess.item);
            pManager.AddNumberParameter("Load", "L", "Load [kN]", GH_ParamAccess.item);
            pManager.AddBooleanParameter("planarEdges", "PE", "Force edges to remain in their vertical plane.", GH_ParamAccess.item); // test
            pManager.AddBooleanParameter("updateOnChange", "U", "Run IGA Solver on [parameter change]", GH_ParamAccess.item); // test

            // TODO: add optionals
            pManager[4].Optional = true;
        }

        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddTextParameter("KiwiErrors", "O", "Errors from Kiwi3D.", GH_ParamAccess.item);
            pManager.AddBrepParameter("FormFoundShell", "S", "Form found shell.", GH_ParamAccess.item);
            pManager.AddCurveParameter("FormFoundCorners", "C", "Form found shell corners.", GH_ParamAccess.list);
            pManager.AddCurveParameter("FormFoundEdges", "E", "Form found shell edges.", GH_ParamAccess.list);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {

            List<Brep> planShell = new List<Brep>();
            List<Curve> corners = new List<Curve>();
            List<Curve> edges = new List<Curve>();
            double height = 0;
            double subDiv = 0;
            bool run = false;
            bool curveSupports = false; // test
            bool scaleDeform = false; // test
            double thickFact = 1.0; // test

            var membPrestress1 = 0.1; // P1 is u-direction
            var membPrestress2 = 0.1; // P2 is v-direction
            var cablePrestress = 0.1;
            var srfLoad = 0.010;
            bool planarEdges = false; // test
            bool updateOnChange = false; // test


            if (!DA.GetDataList(0, planShell)) return;
            if (!DA.GetDataList(1, corners)) return;
            if (!DA.GetDataList(2, edges)) return;
            if (!DA.GetData(3, ref height)) return;
            DA.GetData(4, ref subDiv);
            if (!DA.GetData(5, ref run)) return;
            if (!DA.GetData(6, ref curveSupports)) return; // test
            if (!DA.GetData(7, ref scaleDeform)) return; // test
            if (!DA.GetData(8, ref thickFact)) return; // test
            if (!DA.GetData(9, ref membPrestress1)) return; // test
            if (!DA.GetData(10, ref membPrestress2)) return; // test
            if (!DA.GetData(11, ref cablePrestress)) return; // test
            if (!DA.GetData(12, ref srfLoad)) return; // test
            if (!DA.GetData(13, ref planarEdges)) return; // test
            if (!DA.GetData(14, ref updateOnChange)) return; // test

            if (subDiv == 0)
                subDiv = SURF_SUBDIV;

            /*
            // extract corners from surface, corners being the 4 shortest boundary edges, instead of being an input
            // should go to SHELLScommon, if it ever exists
            var planAllEdges = planShell[0].Edges; // this only works if planShell is single item
            // sort edges by length
            List<BrepEdge> sortedAllEdges = planAllEdges.OrderBy(s => s.GetLength()).ToList();
            // get 50% shortest edges
            corners = new List<Curve>(); // removeE
            edges = new List<Curve>(); // removeE
            int numAllEdges = sortedAllEdges.Count;
            for (int i = 0; i < numAllEdges / 2; i++) corners.Add(sortedAllEdges[i].EdgeCurve); // equivalent to GetRange(0,4)
            for (int i = numAllEdges / 2; i < numAllEdges; i++) edges.Add(sortedAllEdges[i].EdgeCurve);
            */

            bool calculate = updateOnChange | run; // runs Kiwi analysis if run button or input change

            //if (run | SOLUTION_CHANGED)
            if (calculate)
                {
                    var fileTol = Rhino.RhinoDoc.ActiveDoc.ModelAbsoluteTolerance;

                // Get all Kiwi3d components
                // TODO: Delete unused components: LinearAnalysis, ShellElement?
                var componentNames = new List<string>() { "Kiwi3d.MaterialDefaults", "Kiwi3d.CurveRefinement", "Kiwi3d.SurfaceRefinement", "Kiwi3d.ShellElement", "Kiwi3d.MembraneElement", "Kiwi3d.CableElement",
                    "Kiwi3d.SupportPoint", "Kiwi3d.SupportCurve", "Kiwi3d.SurfaceLoad", "Kiwi3d.Formfinding", "Kiwi3d.LinearAnalysis", "Kiwi3d.AnalysisModel",
                    "Kiwi3d.IGASolver", "Kiwi3d.DeformedModel" };

                var componentInfos = componentNames.ToDictionary(x => x, x => Rhino.NodeInCode.Components.FindComponent(x));

                var missingComponents = componentInfos.Where(kvp => kvp.Value == null).Select(x => x.Key).ToList();
                if (missingComponents.Count > 0)
                {
                    throw new Exception("Cannot find component: " + String.Join(", ", missingComponents));
                }

                // Create temporary directory
                var tmpDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
                if (Directory.Exists(tmpDir))
                    Directory.Delete(tmpDir);
                Directory.CreateDirectory(tmpDir);

                // Define model
                //var thickness = height * 0.1; // Use 10% of target height
                //var thickness = height * thickFact; // default thickness value in Kiwi component, used for LoR = 0.1 (absolute, not factor)
                var thickness = 0.001; // for membrane

                var kiwiMembraneMaterial = (CallComponent(componentInfos, "Kiwi3d.MaterialDefaults", new object[] { MAT_ETFE })[0] as IList<object>)[0];
                var kiwiCableMaterial = (CallComponent(componentInfos, "Kiwi3d.MaterialDefaults", new object[] { MAT_STEEL })[0] as IList<object>)[0];

                var kiwiMembraneRefinement = (CallComponent(componentInfos, "Kiwi3d.SurfaceRefinement", new object[] {
                    SURF_DEGREE, SURF_DEGREE, (int)subDiv, (int)subDiv })[0] as IList<object>)[0];
                var kiwiCableRefinement = (CallComponent(componentInfos, "Kiwi3d.CurveRefinement", new object[] {
                    CRV_DEGREE, CRV_SUBDIV })[0] as IList<object>)[0];


                var kiwiElements = new List<object>();

                // Define membrane patches
                foreach (var m in planShell)
                    kiwiElements.Add ((CallComponent(componentInfos, "Kiwi3d.MembraneElement", new object[] {
                        m, kiwiMembraneMaterial, thickness, membPrestress1, membPrestress2, kiwiMembraneRefinement, null, true, null })[0] as IList<object>)[0]);

                // Define edge cables
                double cableDiameter = 0.01; // default value, move to FIELD?
                foreach (var e in edges)
                    kiwiElements.Add((CallComponent(componentInfos, "Kiwi3d.CableElement", new object[] { 
                        e, kiwiCableMaterial, cableDiameter, cablePrestress, false, kiwiCableRefinement, null, true })[0] as IList<object>)[0]);

                // Pinned support at corners
                // Divide the curves into points since the support curve seems to not work as expected
                var kiwiSupports = new List<object>();

                // TESTING
                // with curve supports, to confirm same Karamba analysis results in LoR report
                
                if (curveSupports)

                //kiwiSupports = new List<object>(); // clears previous kiwiSupports, using points
                foreach (var c in corners)
                {
                    kiwiSupports.Add((CallComponent(componentInfos, "Kiwi3d.SupportCurve", new object[] { c, true,
                        true, true, false })[0] as IList<object>)[0]);
                }

                else          
                    
                foreach (var c in corners)
                {
                    Point3d[] points = new Point3d[0];
                    c.DivideByCount(CRV_SUBDIV, true, out points);

                    foreach (var p in points)
                        kiwiSupports.Add((CallComponent(componentInfos, "Kiwi3d.SupportPoint", new object[] { p, true,
                            true, true, false, false })[0] as IList<object>)[0]);
                }


                // constrain edges to plane = additional supports
                if (planarEdges)
                {
                    foreach (var e in edges)
                    {
                        Point3d[] points = new Point3d[0]; // support points
                        double[] ts = e.DivideByCount(CRV_SUBDIV, false, out points); // divide in equal parameters?
                        // get vectors perpendicular to line for determining which directions to constrain
                        Plane[] pFrames = e.GetPerpendicularFrames(ts);
                        // constrains using dot product of "outward direction" with the corresponding degree of freedom
                        bool DX = (pFrames[0].YAxis * Vector3d.XAxis) != 0; // if 0, outward and X are colinear, so we must constrain
                        bool DY = (pFrames[0].YAxis * Vector3d.YAxis) != 0; // if 0, outward and Y are colinear, so we must constrain
                        bool DZ = false; // needs to be able to move vertically

                        // add support points
                        foreach (var p in points)
                            kiwiSupports.Add((CallComponent(componentInfos, "Kiwi3d.SupportPoint", new object[] { 
                            p, DX, DY, DZ, false, false })[0] as IList<object>)[0]);
                    }
                }


                // Uniform load pushing upwards - supporting multiple membrane patches
                //var kiwiLoads = (CallComponent(componentInfos, "Kiwi3d.SurfaceLoad", new object[] {
                //  planShell, "1", Vector3d.ZAxis, srfLoad, null, null, 1 })[0] as IList<object>)[0];
                var kiwiLoads = new List<object>();
                foreach (var m in planShell)
                    kiwiLoads.Add((CallComponent(componentInfos, "Kiwi3d.SurfaceLoad", new object[] {
                        m, "1", Vector3d.ZAxis, srfLoad, null, null, 1 })[0] as IList<object>)[0]);

                // Run Kiwi analysis
                //var kiwiAnalOptions = (CallComponent(componentInfos, "Kiwi3d.LinearAnalysis", new object[] { ANAL_OUTPUT })[0] as IList<object>)[0];
                var kiwiAnalOptions = (CallComponent(componentInfos, "Kiwi3d.Formfinding", new object[] { 
                    "Formfinding", FF_STEPS, FF_ITERS, ANAL_OUTPUT })[0] as IList<object>)[0];
                var kiwiModel = (CallComponent(componentInfos, "Kiwi3d.AnalysisModel",
                    new object[] { kiwiAnalOptions, kiwiElements, kiwiSupports, kiwiLoads })[0] as IList<object>)[0];
                var kiwiResult = CallComponent(componentInfos, "Kiwi3d.IGASolver", new object[] { kiwiModel, tmpDir, true });
                KiwiErrors = kiwiResult[1] == null ? "" : (kiwiResult[1] as IList<object>)[0] as string;
                var kiwiModelRes = (kiwiResult[0] as IList<object>)[0];

                // Scale deformed shell to target height
                if (planShell.Count == 1)
                    FormFoundShell = (CallComponent(componentInfos, "Kiwi3d.DeformedModel", new object[] { kiwiModelRes })[1] as IList<object>)[0] as Brep;
                else //multiple membrane patches
                {
                    var FormFoundGeo = (CallComponent(componentInfos, "Kiwi3d.DeformedModel", new object[] { kiwiModelRes })[1] as IList<object>);                    
                    List<Brep> FormFoundSrfs = new List<Brep>();
                    for (int i = 0; i < planShell.Count; i++) FormFoundSrfs.Add(FormFoundGeo[i] as Brep);
                    FormFoundShell = Brep.JoinBreps(FormFoundSrfs, fileTol)[0];
                }


                if (FormFoundShell != null)
                {
                    var bounds = FormFoundShell.GetBoundingBox(Plane.WorldXY);
                    var scale = height / (bounds.Max.Z - bounds.Min.Z);

                    if (planShell.Count == 1)
                        FormFoundShell = (CallComponent(componentInfos, "Kiwi3d.DeformedModel", new object[] { kiwiModelRes, null, scale })[1] as IList<object>)[0] as Brep;
                    else //multiple membrane patches
                    {
                        var FormFoundGeo = (CallComponent(componentInfos, "Kiwi3d.DeformedModel", new object[] { kiwiModelRes, null, scale })[1] as IList<object>);
                        List<Brep> FormFoundSrfs = new List<Brep>();
                        for (int i = 0; i < planShell.Count; i++) FormFoundSrfs.Add(FormFoundGeo[i] as Brep);
                        FormFoundShell = Brep.JoinBreps(FormFoundSrfs, fileTol)[0];
                    }

                    // get edges from deformed geometry, add them to FormFoundEdges
                    FormFoundEdges = new List<Curve>(); // reset FIELD
                    var FFgeo = (CallComponent(componentInfos, "Kiwi3d.DeformedModel", new object[] { kiwiModelRes, null, scale })[1] as IList<object>);
                    List<Brep> FFedges = new List<Brep>();
                    for (int i = planShell.Count; i < FFgeo.Count; i++) FormFoundEdges.Add(FFgeo[i] as Curve);

                    FormFoundCorners = new List<Curve>(); // reset FIELD
                    // project corners onto deformed surface
                    foreach (var c in corners)
                    {
                        Curve[] projCorner = Curve.ProjectToBrep(c, FormFoundShell, Vector3d.ZAxis, fileTol);
                        if (projCorner == null || projCorner.Length == 0) FormFoundCorners.Add(c); // case of straight corners?
                        else FormFoundCorners.Add(projCorner[0]);
                    }
                }

                // reset run = false; needed?
                //run = false;
                //SOLUTION_CHANGED = false; // event hndling
            }
            
            DA.SetData(0, KiwiErrors);
            DA.SetData(1, FormFoundShell);
            DA.SetDataList(2, FormFoundCorners);
            DA.SetDataList(3, FormFoundEdges);
        }

        private object[] CallComponent(Dictionary<string, Rhino.NodeInCode.ComponentFunctionInfo> components, string componentName, object[] args)
        {
            var tmpOut = new string[0];
            return components[componentName].Evaluate(args.ToList(), false, out tmpOut);
        }

        /*
        // This method will be subscribed to the SolutionStart event.
        private void OnSolutionStart(object sender, GH_SolutionEventArgs e)
        {
            SOLUTION_CHANGED = true;

            // testing event firing
            GH_ActiveObject GHobj = this; // this component
            GHobj.AddRuntimeMessage(GH_RuntimeMessageLevel.Remark, "Solution change detected!");
            //RhinoApp.WriteLine(args.TheObject.ObjectType + ": " + args.ObjectId);
        }
        */


        protected override System.Drawing.Bitmap Icon
        {
            get
            {
                return ACORN_shells.Properties.Resources.ACORN_24;
            }
        }

        public override Guid ComponentGuid
        {
            get { return new Guid("59b0224d-0f8d-4782-9995-d22acea7de9c"); }
        }
    }
}
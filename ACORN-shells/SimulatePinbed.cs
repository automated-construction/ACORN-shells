using System;
using System.Collections.Generic;

using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;
using Rhino.Geometry;
using System.Linq;
using Rhino.Geometry.Collections;
using Rhino.Collections;
using GH.MiscToolbox.Components.Utilities;
using Grasshopper;

namespace ACORN_shells
{
    /// <summary>
    /// Simulates the pinbed mould.
    /// </summary>
    public class SimulatePinbed : GH_Component
    {
        public SimulatePinbed()
          : base("Simulate Pinbed", "A:SimPinbed",
              "Simulates the pinbed mould.",
              "ACORN", "Pinbed")
        {
        }

        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddSurfaceParameter("Shell", "S", "Shell.", GH_ParamAccess.item);
            pManager.AddRectangleParameter("Modules", "M", "Rectangles corresponding to modules", GH_ParamAccess.tree);

        }

        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddBrepParameter("Extended segments", "ES", "Extended segments", GH_ParamAccess.tree);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            Surface shell = null;
            GH_Structure<GH_Rectangle> ghModuleTree = new GH_Structure<GH_Rectangle>();

            if (!DA.GetData(0, ref shell)) return;
            if (!DA.GetDataTree<GH_Rectangle>(1, out ghModuleTree)) return;


            // convert moduleTree: GH_Structure (Grasshopper) to DataTree (RhinoCommon)
            // repeated, move to COMMON
            DataTree<Rectangle3d> rcModuleTree = new DataTree<Rectangle3d>();
            foreach (GH_Path path in ghModuleTree.Paths)
            {
                GH_Rectangle ghModule = ghModuleTree.get_Branch(path)[0] as GH_Rectangle;
                Rectangle3d rcModule = new Rectangle3d();
                GH_Convert.ToRectangle3d(ghModule, ref rcModule, GH_Conversion.Both);
                rcModuleTree.Add(rcModule, path);
            }

            // segments are extended for covering whole pinbed module, for shape continuity
            DataTree<Brep> extendedSegments = new DataTree<Brep>();
            foreach (GH_Path path in rcModuleTree.Paths)
            {
                Rectangle3d currModule = rcModuleTree.Branch(path)[0];
                //project module onto surface
                Curve projectedModule = 
                    Curve.JoinCurves(
                        Curve.ProjectToBrep(currModule.ToNurbsCurve(), shell.ToBrep(), currModule.Plane.ZAxis, DocumentTolerance())
                    )[0];

                //split shell using module projection - SLOW!!!
                Brep[] shellSplinters = shell.ToBrep().Split(new List<Curve> { projectedModule }, DocumentTolerance());
                // determine correct split result: sort by area
                Brep currExtendedSegment = shellSplinters.ToList<Brep>().OrderBy(o => o.GetArea()).ToList()[0];

                extendedSegments.Add(currExtendedSegment, path);
            }

            // adjust module heights


            DA.SetDataTree(0, extendedSegments);
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
            get { return new Guid("3137bd7c-0f11-4688-b9dc-c57f003eb552"); }
        }
    }
}
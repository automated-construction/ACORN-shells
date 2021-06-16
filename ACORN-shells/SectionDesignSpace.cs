using System;
using System.Collections.Generic;

using Grasshopper;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;
using Rhino.Geometry;
using System.Linq;
using Rhino.Geometry.Collections;
using Rhino.Collections;
using GH.MiscToolbox.Components.Utilities;
using System.Drawing;
using Grasshopper.Kernel.Types;

namespace ACORN_shells
{
    /// <summary>
    /// Sections a design space from DSE
    /// 
    /// NOT BEING COMPILED
    /// </summary>
    public class SectionDesignSpace : GH_Component
    {
        public SectionDesignSpace()
          : base("Section Design Space", "A:SectionDS",
              "Sections a Design Space according to fixed dimensions",
              "ACORN", "DSV")
        {
        }

        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddNumberParameter("Varying dimensions", "VD", "Varying dimensions that define section", GH_ParamAccess.list);
            pManager.AddNumberParameter("Design space", "DS", "All data points in the design space", GH_ParamAccess.tree);
            pManager.AddNumberParameter("Reference vector", "RV", "Reference vector components", GH_ParamAccess.list);

        }

        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddPathParameter("Data points in section", "DPS", "Paths of data points in section", GH_ParamAccess.list);
            pManager.AddPathParameter("Closest data point", "CDP", "Path of data point closest to reference vector", GH_ParamAccess.item);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {

            List<double> varyingDimensions = new List<double>();
            GH_Structure<List<GH_Number>> ghDesignSpace = new GH_Structure<GH_Number>();
            List<double> referenceVector = new List<double>();

            if (!DA.GetDataList(0, varyingDimensions)) return;
            if (!DA.GetDataTree<GH_Number>(1, out ghDesignSpace)) return;
            if (!DA.GetDataList(2, referenceVector)) return;

            // convert DesignSpace: GH_Structure (Grasshopper) to DataTree (RhinoCommon)
            DataTree<double> rhDesignSpace = new DataTree<double>();
            foreach (GH_Path path in ghDesignSpace.Paths)
            {
                GH_Number ghDataPoint = ghDesignSpace.get_Branch(path)[0] as GH_Number;
                double module = new List<double>();
                GH_Convert.ToRectangle3d(ghModule, ref module, GH_Conversion.Both);
                rhDesignSpace.Add(module, path);
            }



            DA.SetDataTree(0, moduleTree);
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
            get { return new Guid("d836c9e6-8f68-4709-aec0-0b262e178d4e"); }
        }
    }
}
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

using DSVcommon;

namespace ACORN_shells
{
    /// <summary>
    /// Assembles a Design Space by creating a list of Design Vectors
    /// </summary>
    public class ExplodeDesignSpace : GH_Component
    {
        public ExplodeDesignSpace()
          : base("Explode Design Space", "A:ExplodeDS",
              "Deconstructs a Design Space into Design Map, Objective Values and respective labels",
              "ACORN", "DSV")
        {
        }

        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddGenericParameter("Design Space", "DS", "List of DSV Design Vectors", GH_ParamAccess.list);
        }

        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddNumberParameter("Design Map", "DM", "Parameter values used for generating designs", GH_ParamAccess.tree);
            pManager.AddNumberParameter("Objective Values", "O", "Values obtained from analysis", GH_ParamAccess.tree);
            pManager.AddTextParameter("Design Map Labels", "DML", "Design Map Labels", GH_ParamAccess.list);
            pManager.AddTextParameter("Objective Value Labels", "OL", "Objective Value Labels", GH_ParamAccess.list);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            List<DesignVector> designSpace = new List<DesignVector>();
            if (!DA.GetDataList(0, designSpace)) return;



            DataTree <double> designMap = new DataTree<double>();
            DataTree<double> objValues = new DataTree<double>();
            List<string> designMapLabels = DesignVector.DesignMapLabels;
            List<string> objValueLabels = DesignVector.ObjValuesLabels;

            foreach (DesignVector dv in designSpace)
            {
                GH_Path currPath = new GH_Path(dv.ID);
                designMap.AddRange(dv.DesignMap, currPath);
                objValues.AddRange(dv.ObjValues, currPath);
            }

            DA.SetDataTree(0, designMap);
            DA.SetDataTree(1, objValues);
            DA.SetDataList(2, designMapLabels);
            DA.SetDataList(3, objValueLabels);
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
            get { return new Guid("ed64841d-eadb-411f-acf1-84d6dacd3c40"); }
        }
    }
}
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
    public class MakeDesignSpace : GH_Component
    {
        public MakeDesignSpace()
          : base("Make Design Space", "A:MakeDS",
              "Assembles a Design Space by creating a list of Design Vectors",
              "ACORN", "DSV")
        {
        }

        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddNumberParameter("Design Map", "DM", "Parameter values used for generating designs", GH_ParamAccess.tree);
            pManager.AddNumberParameter("Objective Values", "O", "Values obtained from analysis", GH_ParamAccess.tree);
            pManager.AddTextParameter("Design Map Labels", "DML", "Design Map Labels", GH_ParamAccess.list);
            pManager.AddTextParameter("Objective Value Labels", "OL", "Objective Value Labels", GH_ParamAccess.list);
        }

        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddGenericParameter("Design Space", "DS", "List of DSV Design Vectors", GH_ParamAccess.list);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            GH_Structure <GH_Number> ghDesignMap = new GH_Structure<GH_Number>();
            GH_Structure<GH_Number> ghObjValues = new GH_Structure<GH_Number>();
            List<string> designMapLabels = new List<string>();
            List<string> objValueLabels = new List<string>();

            if (!DA.GetDataTree<GH_Number>(0, out ghDesignMap)) return;
            if (!DA.GetDataTree<GH_Number>(1, out ghObjValues)) return;
            if (!DA.GetDataList(2, designMapLabels)) return;
            if (!DA.GetDataList(3, objValueLabels)) return;

            // set labels (static properties)
            DesignVector.DesignMapLabels = designMapLabels;
            DesignVector.ObjValuesLabels = objValueLabels;

            // creates list of DVs
            List<DesignVector> designSpace = new List<DesignVector>();
            for (int i = 0; i < ghDesignMap.PathCount; i++) // assuming tree as {0}, {1}, ... each path being a Design Vector
            {
                GH_Path currPath = new GH_Path(i);

                DesignVector currVector = new DesignVector();
                currVector.ID = i;

                //convert GH_Numbers to double - make conversion function?
                List<double> currDesignMap = new List<double>();
                foreach (GH_Number ghDesignCoord in ghDesignMap.get_Branch(currPath))
                {
                    GH_Convert.ToDouble(ghDesignCoord, out double rhDesignCoord, GH_Conversion.Both);
                    currDesignMap.Add(rhDesignCoord);
                }
                currVector.DesignMap = currDesignMap;

                List<double> currObjValues = new List<double>();
                foreach (GH_Number ghObjValue in ghObjValues.get_Branch(currPath))
                {
                    GH_Convert.ToDouble(ghObjValue, out double rhObjValue, GH_Conversion.Both);
                    currObjValues.Add(rhObjValue);
                }
                currVector.ObjValues = currObjValues;

                designSpace.Add(currVector);
            }

            DA.SetDataList(0, designSpace);
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
            get { return new Guid("38bfb4c5-127c-411f-825f-49a9f57c36bb"); }
        }
    }
}
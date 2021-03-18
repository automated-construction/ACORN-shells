using System;
using System.Collections;
using System.Collections.Generic;

using Rhino;
using Rhino.Geometry;

using Grasshopper;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;
using Karamba.GHopper.Models;
using Karamba.Results;
using Karamba.Models;
using System.Linq;

namespace ACORN
{
    /// <summary>
    /// Generates demo building geometry
    /// </summary>
    public class AnalysisResults : GH_Component
    {
        public AnalysisResults()
          : base("Analysis Results", "A:Results",
              "Gets analysis results for whole shell",
              "ACORN", "Shells")
        {
        }


        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddGenericParameter("Karamba model", "M", "Karamba model for whole shell", GH_ParamAccess.item);
            pManager.AddNumberParameter("Percentile", "P", "Percentage of non-extreme stress elements", GH_ParamAccess.item);
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddNumberParameter("MaxComp", "MC", "Maximum compression [MPa]", GH_ParamAccess.item);
            pManager.AddNumberParameter("MaxComp%", "MC%", "Maximum compression of non-extreme stress elements [MPa]", GH_ParamAccess.item);
            pManager.AddNumberParameter("MaxTens%", "MT%", "Maximum tension of non-extreme stress elements [MPa]", GH_ParamAccess.item);
            pManager.AddNumberParameter("MaxTens", "MT", "Maximum tension [MPa]", GH_ParamAccess.item);
            pManager.AddMeshParameter("Extreme elements", "EE", "Extreme stress elements (viz)", GH_ParamAccess.item);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            GH_Model ghModel = new GH_Model();
            double percentile = 100;
            if (!DA.GetData(0, ref ghModel)) return;
            if (!DA.GetData(1, ref percentile)) return;

            double maxComp = 0;
            double maxCompP = 0;
            double maxTensP = 0;
            double maxTens = 0;
            Mesh extremeElements = new Mesh();

            // convert GH_Model to Model
            Model k3dModel = ghModel.Value;

            // get first and second principal stress values for all elements, top and bottom layers
            PrincipalStressDirs.solve(k3dModel, 0, -1, new feb.VectReal(), out _, out _, out _, 
                out List<double> bottomPS1s, out List<double> bottomPS2s);
            PrincipalStressDirs.solve(k3dModel, 0, 1, new feb.VectReal(), out _, out _, out _, 
                out List<double> topPS1s, out List<double> topPS2s);

            // merge all stresses - create stressValue lists before? yes if we need them separately
            List<List<double>> PSlists = new List<List<double>> { bottomPS1s, bottomPS2s, topPS1s, topPS2s };
            List<double> allPSs = PSlists.SelectMany(e => e).ToList();

            // create list of stressValue objects, pairing stress values and element indexes,  to sort "asynchronously" as in GH sort component
            List<StressValue> stressValues = new List<StressValue>();
            foreach (List<double> PSlist in PSlists)
                for (int i = 0; i < PSlist.Count; i++)
                    stressValues.Add(new StressValue { Value = PSlist[i], Element = i});

            // sort stress Values by value, from negative compression to positive tension
            List<StressValue> sortedElementIndexes = stressValues.OrderBy(s => s.Value).ToList();

            maxComp = sortedElementIndexes[0].Value;





            // for now, nothing happens

            DA.SetData(0, maxComp);
            DA.SetData(1, maxCompP);
            DA.SetData(2, maxTensP);
            DA.SetData(3, maxTens);
            DA.SetData(4, extremeElements);
        }

        public override GH_Exposure Exposure
        {
            get { return GH_Exposure.primary; }
        }

        protected override System.Drawing.Bitmap Icon
        {
            get
            {
                // You can add image files to your project resources and access them like this:
                return ACORN_shells.Properties.Resources.ACORN_24;
            }
        }

        public override Guid ComponentGuid
        {
            get { return new Guid("20adf443-15e2-4d7c-85f8-63cf7d7b42bc"); }
        }

        class StressValue
        {
            public double Value { get; set; }
            public int Element { get; set; }
        }
    }
}

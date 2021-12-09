using System;
using System.Collections.Generic;
using System.Linq;

using Rhino.Geometry;

using Grasshopper.Kernel;
using Karamba.Geometry;
using Karamba.GHopper.Models;
using Karamba.Results;
using Karamba.Models;
using Karamba.GHopper.Geometry;


namespace ACORN_shells
{
    /// <summary>
    /// Should render AnalysisResults and AnalysisResultsExternal obsolete
    /// Only uses AnalyzeThI if FAST is on, and if it does not work reverts back to ThII
    /// Change name after testing
    /// </summary>
    public class StressResultsNEW : GH_Component
    {
        public StressResultsNEW()
          : base("Stress Results NEW", "A:StressResultsNEW",
              "Gets stress results from shell analysis",
              "ACORN Shells", " Analysis")
        // adding spaces to category names as per https://www.grasshopper3d.com/forum/topics/change-order-of-plugin-sub-category-c 
        {
        }


        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddGenericParameter("Analysed Model", "AM", "Analysed Model (ThI or ThII)", GH_ParamAccess.item);
            pManager.AddNumberParameter("Percentile", "P", "Percentage of non-extreme stress elements (0-100). Default: 95%", GH_ParamAccess.item);

            pManager[1].Optional = true;
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddNumberParameter("MaxComp", "C", "Maximum compression stress [MPa]", GH_ParamAccess.item);
            pManager.AddNumberParameter("MaxComp%", "C%", "Maximum compression of non-extreme stress elements [MPa]", GH_ParamAccess.item);
            pManager.AddMeshParameter("MaxComp% mesh(es)", "CM", "Extreme compression stress elements (viz)", GH_ParamAccess.list);
            pManager.AddNumberParameter("MaxTens", "T", "Maximum tension stress [MPa]", GH_ParamAccess.item);
            pManager.AddNumberParameter("MaxTens%", "T%", "Maximum tension of non-extreme stress elements [MPa]", GH_ParamAccess.item);
            pManager.AddMeshParameter("MaxTens% mesh", "TM", "Extreme tension stress elements (viz)", GH_ParamAccess.list);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            GH_Model ghModelAnalysis = new GH_Model();
            double percentile = 95;

            if (!DA.GetData(0, ref ghModelAnalysis)) return;
            DA.GetData(1, ref percentile);

            // convert GH_Model to Model
            Model k3dModelAnalysis = ghModelAnalysis.Value;

            List<ElementStress> elementStresses = ElementStress.GetElementStresses(k3dModelAnalysis, out List<Mesh> rhMeshes);

            //sort all elements for Compression by looking at minimum stress value
            List<ElementStress> sortedForCompression = elementStresses.OrderBy(s => s.CalculateMaximumCompression()).ToList();
            List<ElementStress> sortedForTension = elementStresses.OrderByDescending(s => s.CalculateMaximumTension()).ToList();
   
            // extract number of extreme elements based on input percentile
            int countExtremeElements = (int) Math.Round (elementStresses.Count * (100 - percentile) / 100); 
            List<ElementStress> extremeElementsCompression = sortedForCompression.GetRange(0, countExtremeElements);
            List<ElementStress> extremeElementsTension = sortedForTension.GetRange(0, countExtremeElements);

            // get values for output, converted from kN/cm2 to MPa
            double maxComp = extremeElementsCompression.First().CalculateMaximumCompression() * 10;
            double maxCompP = extremeElementsCompression.Last().CalculateMaximumCompression() * 10;
            double maxTens = extremeElementsTension.First().CalculateMaximumTension() * 10;
            double maxTensP = extremeElementsTension.Last().CalculateMaximumTension() * 10;


            // generate meshes with extreme stress elements
            // for visualisation purposes, even if analysing multiple meshes (eg, segmented shell)
            List<Mesh> meshesComp = ElementStress.MakeExtremeMeshes(rhMeshes, extremeElementsCompression);
            List<Mesh> meshesTens = ElementStress.MakeExtremeMeshes(rhMeshes, extremeElementsTension);

            DA.SetData(0, maxComp);
            DA.SetData(1, maxCompP);
            DA.SetDataList(2, meshesComp);
            DA.SetData(3, maxTens);
            DA.SetData(4, maxTensP);
            DA.SetDataList(5, meshesTens);


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
            get { return new Guid("37196fa0-d39d-42cb-bb5e-245f880583e8"); }
        }
    }
}

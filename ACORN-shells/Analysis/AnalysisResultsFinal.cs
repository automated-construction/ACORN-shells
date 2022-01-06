using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

using Rhino;
using Rhino.Geometry;

using Grasshopper;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;
using Karamba.Geometry;
using Karamba.GHopper.Models;
using Karamba.Results;
using Karamba.Models;
using Karamba.GHopper.Geometry;
using Karamba.Algorithms;
using Grasshopper.Kernel.Geometry.Delaunay;
using Rhino.Runtime.InteropWrappers;
using Rhino.Render.Fields;

namespace ACORN
{
    /// <summary>
    /// Should render AnalysisResults and AnalysisResultsExternal obsolete
    /// Only uses AnalyzeThI if FAST is on, and if it does not work reverts back to ThII
    /// Change name after testing
    /// </summary>
    public class AnalysisResultsFinal : GH_Component
    {
        public AnalysisResultsFinal()
          : base("Analysis Results Final", "A:ResultsFinal",
              "Gets analysis results for shell",
              "ACORN Shells", " Analysis")
        // adding spaces to category names as per https://www.grasshopper3d.com/forum/topics/change-order-of-plugin-sub-category-c 
        {
        }


        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddGenericParameter("Karamba Model", "M", "Assembled Karamba model (prior to Analysis)", GH_ParamAccess.item);
            pManager.AddBooleanParameter("Fast?", "F", "If True, uses ThI but does not calculate Buckling modes using ThII (faster)", GH_ParamAccess.item);
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddTextParameter("Output", "out", "Error messages and warnings", GH_ParamAccess.list);

            pManager.AddGenericParameter("Analysed Model", "AM", "Analysed Model (ThI or ThII)", GH_ParamAccess.item);
            pManager.AddGenericParameter("Buckling Model", "BM", "Buckling Model (using ThII)", GH_ParamAccess.item); 
                
            pManager.AddNumberParameter("Maximum displacement", "D", "Maximum displacement [cm]", GH_ParamAccess.item);
            pManager.AddNumberParameter("Buckling load factors", "BF", "Buckling load factors", GH_ParamAccess.list);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            GH_Model ghModel = new GH_Model();
            bool fast = false;

            if (!DA.GetData(0, ref ghModel)) return;
            if (!DA.GetData(1, ref fast)) return;

            // convert GH_Model to Model
            Model k3dModel = ghModel.Value;

            // errorMsgs collects error messages from analysis algorithms
            List<string> errorMsgs = new List<string>();


            //------------ ANALYSIS (displacement and buckling)
            // perform analysis: for fast calculations uses AnalyzeThI

            Model k3dModelAnalysis = new Model();
            Model k3dModelBuckling = new Model();
            double maxDisp = 0;
            List<double> bucklingFactors = new List<double>();
            if (fast)
            {
                ThIAnalyze.solve(k3dModel, out List<double> maxDispsThI, out _, out _, out string warning, out k3dModelAnalysis);
                errorMsgs.Add(warning);
                maxDisp = maxDispsThI[0] * 100; // assuming results in [m], even though component outputs in [cm]
            }
            else
            {
                // using defaults from GH AnalyzeThII component
                AnalyzeThII.solve(k3dModel, -1, 1.0e-7, 50, false, out List<double> maxDispsThII, out _, out _, out k3dModelAnalysis, out string warning);
                errorMsgs.Add(warning);
                maxDisp = maxDispsThII[0] * 100; // assuming results in [m], even though component outputs in [cm];

                // calculate buckling model and safety factors
                // using defaults from GH BModes component; 300 MaxIter might be reduced for performance; like GH component, returns positive load factors only
                Buckling.solve(k3dModelAnalysis, 1, 1, 300, 1.0e-7, 1, out bucklingFactors, out k3dModelBuckling, out _); 
            }

            DA.SetDataList(0, errorMsgs);
            DA.SetData(1, new GH_Model(k3dModelAnalysis));
            DA.SetData(2, new GH_Model(k3dModelBuckling));
            DA.SetData(3, maxDisp);
            DA.SetDataList(4, bucklingFactors);
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
                return ACORN_shells.Properties.Resources.analysisRes;
            }
        }

        public override Guid ComponentGuid
        {
            get { return new Guid("aa37d121-5369-40db-8b97-dcc43abf5292"); }
        }
        /// <summary>
        /// Creates meshes with the elements top % stress values
        /// Support and outputs multiple meshes
        /// </summary>
        /// <param name="origMeshes"></param>
        /// <param name="SVs"></param>
        /// <returns></returns>
    }
}

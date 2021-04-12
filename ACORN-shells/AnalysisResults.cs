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
            pManager.AddGenericParameter("Karamba model", "M", "Karamba model for whole shell (pre-Analyze)", GH_ParamAccess.item);
            pManager.AddNumberParameter("Percentile", "P", "Percentage of non-extreme stress elements (0-100)", GH_ParamAccess.item);
            pManager.AddBooleanParameter("First Principal Stress only?", "1", "If True, considers First Principal Stress only", GH_ParamAccess.item);

            pManager[2].Optional = true;
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddNumberParameter("MaxComp", "MC", "Maximum compression stress [MPa]", GH_ParamAccess.item);
            pManager.AddNumberParameter("MaxComp%", "MC%", "Maximum compression of non-extreme stress elements [MPa]", GH_ParamAccess.item);
            pManager.AddMeshParameter("MaxComp% mesh", "CM", "Extreme compression stress elements (viz)", GH_ParamAccess.item);
            pManager.AddNumberParameter("MaxTens", "MT", "Maximum tension stress [MPa]", GH_ParamAccess.item);
            pManager.AddNumberParameter("MaxTens%", "MT%", "Maximum tension of non-extreme stress elements [MPa]", GH_ParamAccess.item);
            pManager.AddMeshParameter("MaxTens% mesh", "TM", "Extreme tension stress elements (viz)", GH_ParamAccess.item);
            pManager.AddNumberParameter("Maximum displacement", "Disp", "Maximum displacement[cm]", GH_ParamAccess.item);
            pManager.AddNumberParameter("Buckling factor", "BF", "Buckling factor", GH_ParamAccess.item);
            pManager.AddGenericParameter("Analysed Model ThI", "MThI", "Analysed Model ThI", GH_ParamAccess.item); 
            pManager.AddGenericParameter("Analysed Model ThII", "MThII", "Analysed Model ThII with buckling", GH_ParamAccess.item);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            GH_Model ghModel = new GH_Model();
            double percentile = 100;
            bool firstPS = false; //default

            if (!DA.GetData(0, ref ghModel)) return;
            if (!DA.GetData(1, ref percentile)) return;
            DA.GetData(2, ref firstPS);

            // convert GH_Model to Model
            Model k3dModel = ghModel.Value;

            // Analyze ThI for stresses

            Model k3dModelThI = new Model(); // for stress calc + output for visualization with ShellView
            List<double> maxDispsThI = new List<double>(); // output
            ThIAnalyze.solve(k3dModel, out maxDispsThI, out _, out _, out _, out k3dModelThI);
            double maxDispThI = maxDispsThI[0] * 100; // assuming results in [m], even though component outputs in [cm]

            // extract original shell mesh from k3dModel
            List<IMesh> k3dMeshes = new List<IMesh>();
            k3dModel.Disassemble(out _, out _, out k3dMeshes, out _, out _, out _, out _, out _, out _, out _, out _);
            Mesh originalMesh = k3dMeshes[0].Convert();

            // get first and second principal stress values for all elements, top and bottom layers
            var superimp_factors = new feb.VectReal { 1 }; // according to https://discourse.mcneel.com/t/shell-principal-stresses-in-karamba-api/120629
            PrincipalStressDirs.solve(k3dModelThI, 0, -1, superimp_factors, out _, out _, out _, 
                out List<double> bottomPS1s, out List<double> bottomPS2s);
            PrincipalStressDirs.solve(k3dModelThI, 0, 1, superimp_factors, out _, out _, out _, 
                out List<double> topPS1s, out List<double> topPS2s);

            // merge all stresses - create stressValue lists before? yes if we need them separately
            List<List<double>> PSlists = new List<List<double>>();
            if (firstPS)
                PSlists = new List<List<double>> { bottomPS1s, topPS1s };
            else
                PSlists = new List<List<double>> { bottomPS1s, bottomPS2s, topPS1s, topPS2s };
            List<double> allPSs = PSlists.SelectMany(e => e).ToList();

            // create list of stressValue objects, pairing stress values and element indexes,  to sort "asynchronously" as in GH sort component
            List<StressValue> stressValues = new List<StressValue>();
            foreach (List<double> PSlist in PSlists)
                for (int i = 0; i < PSlist.Count; i++)
                    stressValues.Add(new StressValue { Value = PSlist[i], Element = i});

            // sort StressValues by value, from negative (compression) to positive (tension)
            List<StressValue> sortedForCompression = stressValues.OrderBy(s => s.Value).ToList();
            List<StressValue> sortedForTension = new List<StressValue>(sortedForCompression);
            sortedForTension.Reverse();
           
            // extract number of extreme elements based on input percentile
            int countExtremeElements = (int) Math.Round (sortedForCompression.Count * (100 - percentile) / 100); // dividing by 4 lists merged together (not elegant, see comment for merge all stresses)            
            List<StressValue> extremeElementsCompression = sortedForCompression.GetRange(0, countExtremeElements);
            List<StressValue> extremeElementsTension = sortedForTension.GetRange(0, countExtremeElements);

            // get values for output, converted from kN/cm2 to MPa
            double maxComp = extremeElementsCompression.First().Value * 10;
            double maxCompP = extremeElementsCompression.Last().Value * 10;
            double maxTens = extremeElementsTension.First().Value * 10;
            double maxTensP = extremeElementsTension.Last().Value * 10;

            // generate meshes with extreme stress elements
            Mesh meshComp = new Mesh(); 

            // copy vertices from original mesh to extreme mesh
            meshComp.Vertices.AddVertices(originalMesh.Vertices); 

            // copy top valued element faces from original mesh to extreme mesh
            foreach (StressValue sv in extremeElementsCompression)
                meshComp.Faces.AddFace(originalMesh.Faces[sv.Element]);

            // finish off
            meshComp.Normals.ComputeNormals();
            meshComp.Compact();

            // repeat for tension (make function? rule of three?)
            Mesh meshTens = new Mesh();
            meshTens.Vertices.AddVertices(originalMesh.Vertices);
            foreach (StressValue sv in extremeElementsTension)
                meshTens.Faces.AddFace(originalMesh.Faces[sv.Element]);
            meshTens.Normals.ComputeNormals();
            meshTens.Compact();


            // Analyze ThII for buckling and displacement
            Model k3dModelThII = new Model(); // prelim + output for visualization with ShellView
            List<double> maxDispsThII = new List<double>(); // output
            AnalyzeThII.solve(k3dModel, -1, 1.0e-7, 50, false, out maxDispsThII, out _, out _, out k3dModelThII, out _); // using defaults from GH AnalyzeThII component
            double maxDispThII = maxDispsThII[0] * 100; // assuming results in [m], even though component outputs in [cm];

            Model k3dModelThIIbuck = new Model();
            List<double> bucklingFactors = new List<double>();
            // defaults from GH BModes component; 300 MaxIter might be reduced for performance
            Buckling.solve(k3dModelThII, 1, 1, 300, 1.0e-7, 1, out bucklingFactors, out k3dModelThIIbuck, out _); // like GH component, returns positive load factors only
            double bucklingFactor = bucklingFactors[0];


            DA.SetData(0, maxComp);
            DA.SetData(1, maxCompP);
            DA.SetData(2, meshComp);
            DA.SetData(3, maxTens);
            DA.SetData(4, maxTensP);
            DA.SetData(5, meshTens);
            DA.SetData(6, maxDispThI);
            DA.SetData(7, bucklingFactor);
            DA.SetData(8, new GH_Model(k3dModelThI));
            DA.SetData(9, new GH_Model(k3dModelThIIbuck));
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

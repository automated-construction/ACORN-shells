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

namespace ACORN_shells
{
    /// <summary>
    /// Should render AnalysisResults and AnalysisResultsExternal obsolete
    /// Only uses AnalyzeThI if FAST is on, and if it does not work reverts back to ThII
    /// Change name after testing
    /// </summary>
    public class StressResults : GH_Component
    {
        public StressResults()
          : base("Stress Results", "A:StressResults",
              "Gets stress results from shell analysis",
              "ACORN", "Shells")
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

            //------------ STRESSES

            // extract original shell mesh from k3dModel
            List<IMesh> k3dMeshes = new List<IMesh>();
            k3dModelAnalysis.Disassemble(out _, out _, out k3dMeshes, out _, out _, out _, out _, out _, out _, out _, out _);
            // for segmented shell analysis, needs to cope with multipe meshes
            List<Mesh> rhMeshes = new List<Mesh>();
            foreach (IMesh k3dMesh in k3dMeshes) rhMeshes.Add(k3dMesh.Convert());

            // get first and second principal stress values for all elements, top and bottom layers
            var superimp_factors = new feb.VectReal { 1 }; // according to https://discourse.mcneel.com/t/shell-principal-stresses-in-karamba-api/120629
            PrincipalStressDirs.solve(k3dModelAnalysis, 0, -1, superimp_factors, out _, out _, out _,
                out List<double> bottomPS1s, out List<double> bottomPS2s);
            PrincipalStressDirs.solve(k3dModelAnalysis, 0, 1, superimp_factors, out _, out _, out _,
                out List<double> topPS1s, out List<double> topPS2s);

            // merge all stresses - create stressValue lists before? yes if we need them separately
            List<List<double>> PSlists = new List<List<double>> { bottomPS1s, bottomPS2s, topPS1s, topPS2s };
            List<double> allPSs = PSlists.SelectMany(e => e).ToList();

            // create list of stressValue objects, pairing stress values and element indexes,  to sort "asynchronously" as in GH sort component
            List<StressValue> stressValues = new List<StressValue>();

            // determine which mesh it belongs to through list partition, and which face in that mesh
            foreach (List<double> PSlist in PSlists) { 
                int meshIndex = 0;
                int faceIndex = 0;
                for (int elementIndex = 0; elementIndex < PSlist.Count; elementIndex++) { 
                    // element count is for ALL meshes, face count is for belonging mesh

                    // creates instance of StressValue 
                    stressValues.Add(new StressValue { 
                        Value = PSlist[elementIndex], 
                        Element = elementIndex, 
                        Mesh = meshIndex, 
                        Face = faceIndex});

                    // manage counts
                    if (faceIndex < rhMeshes[meshIndex].Faces.Count-1)
                        faceIndex++;
                    else // reached the end of iterating all mesh's faces so next mesh
                    {
                        meshIndex++;
                        faceIndex = 0;
                    }
                }
            }
            
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
            // for visualisation purposes, even if analysing multiple meshes (eg, segmented shell)
            List<Mesh> meshesComp = MakeExtremeMeshes(rhMeshes, extremeElementsCompression);
            List<Mesh> meshesTens = MakeExtremeMeshes(rhMeshes, extremeElementsTension);

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
            get { return new Guid("8eaddef5-3ab9-4e5a-bd1d-6ae4645cdac0"); }
        }
        /// <summary>
        /// Creates meshes with the elements top % stress values
        /// Support and outputs multiple meshes
        /// </summary>
        /// <param name="origMeshes"></param>
        /// <param name="SVs"></param>
        /// <returns></returns>
        private List<Mesh> MakeExtremeMeshes (List<Mesh> origMeshes, List<StressValue> SVs)
        {
            List<Mesh> extMeshes = new List<Mesh>();

            // copy vertices from original mesh(es) to extreme mesh
            foreach (Mesh rhMesh in origMeshes)
            {
                Mesh meshTens = new Mesh();
                meshTens.Vertices.AddVertices(rhMesh.Vertices);
                extMeshes.Add(meshTens);
            }

            // copy top valued element faces from original mesh to extreme mesh
            foreach (StressValue sv in SVs)
                //meshComp.Faces.AddFace(rhMesh.Faces[sv.Element]);
                extMeshes[sv.Mesh].Faces.AddFace(origMeshes[sv.Mesh].Faces[sv.Face]);

            // finish off
            foreach (Mesh extMesh in extMeshes)
            {
                extMesh.Normals.ComputeNormals();
                extMesh.Compact();
            }

            return extMeshes;
        }

        class StressValue
        {
            public double Value { get; set; }
            public int Element { get; set; }
            public int Mesh { get; set; } // mesh to which element belongs - support for multiple meshes
            public int Face { get; set; } // face index in Mesh

        }
    }
}

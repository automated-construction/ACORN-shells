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
using System.Windows.Forms;

namespace ACORN_shells
{
    /// <summary>
    /// Should render AnalysisResults and AnalysisResultsExternal obsolete
    /// Only uses AnalyzeThI if FAST is on, and if it does not work reverts back to ThII
    /// Change name after testing
    /// </summary>
    public class StressProbe : GH_Component
    {
        public StressProbe()
          : base("Stress Probe", "StressProbe",
              "Gets principal stress results for specified point in shell",
              "ACORN Shells", " Analysis")
        // adding spaces to category names as per https://www.grasshopper3d.com/forum/topics/change-order-of-plugin-sub-category-c 
        {
        }


        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddGenericParameter("Model", "AM", "Analysed Model (ThI or ThII)", GH_ParamAccess.item);
            pManager.AddPointParameter("Location", "P", "Stress probe location", GH_ParamAccess.item); // later, make for list?

        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddNumberParameter("Sig1-Top", "T1", "First principal stress value, top layer [MPa]", GH_ParamAccess.item);
            pManager.AddNumberParameter("Sig2-Top", "T2", "Second principal stress value, top layer [MPa]", GH_ParamAccess.item);
            pManager.AddNumberParameter("Sig1-Bottom", "B1", "First principal stress value, bottom layer [MPa]", GH_ParamAccess.item);
            pManager.AddNumberParameter("Sig2-Bottom", "B2", "Second principal stress value, bottom layer [MPa]", GH_ParamAccess.item);
            pManager.AddIntegerParameter("Element index", "E", "Index of FEM element closest to point", GH_ParamAccess.item);
            pManager.AddIntegerParameter("Mesh index", "M", "Index of Mesh closest to point", GH_ParamAccess.item);
            pManager.AddIntegerParameter("Face index", "F", "Index of Mesh face closest to point", GH_ParamAccess.item);
            pManager.AddPointParameter("Element center", "C", "Center of element face", GH_ParamAccess.item);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            GH_Model ghModelAnalysis = new GH_Model();
            Point3d probeLocation = Point3d.Unset;

            if (!DA.GetData(0, ref ghModelAnalysis)) return;
            if (!DA.GetData(1, ref probeLocation)) return;

            // convert GH_Model to Model
            Model k3dModelAnalysis = ghModelAnalysis.Value;

            //------------ STRESSES

            List<ElementStress> elementStresses = ElementStress.GetElementStresses(k3dModelAnalysis, out List<Mesh> rhMeshes);

            // determine element closest to probe location using stress vector origins

            ElementStress bestES = elementStresses[0];
            double bestDist = probeLocation.DistanceTo(bestES.princ_origin_top);
            //Mesh bestMesh;

            foreach (ElementStress currES in elementStresses)
            {
                double currDist = probeLocation.DistanceTo(currES.princ_origin_top);
                if (currDist < bestDist)
                {
                    bestDist = currDist;
                    bestES = currES;
                }
            }
            // output values converted from kN/cm2 to MPa
            DA.SetData(0, bestES.princ_val1_top * 10);
            DA.SetData(1, bestES.princ_val2_top * 10);
            DA.SetData(2, bestES.princ_val1_bottom * 10);
            DA.SetData(3, bestES.princ_val2_bottom * 10);
            DA.SetData(4, bestES.Element);
            DA.SetData(5, bestES.Mesh);
            DA.SetData(6, bestES.Face);
            DA.SetData(7, bestES.princ_origin_top);




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
                return ACORN_shells.Properties.Resources.stressProbe;
            }
        }

        public override Guid ComponentGuid
        {
            get { return new Guid("1c89aac3-644a-4e34-88c3-435bc583a2bb"); }
        }
    }
}

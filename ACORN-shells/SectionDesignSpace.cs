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
            pManager.AddIntegerParameter("Varying dimensions", "VD", "Varying dimensions that define section", GH_ParamAccess.list);
            pManager.AddGenericParameter("Design space", "DS", "Design Vectors in the design space", GH_ParamAccess.list);
            pManager.AddNumberParameter("Reference vector components", "RVC", "Reference vector components", GH_ParamAccess.list);

        }

        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddGenericParameter("Design Vectors in section", "S", "Design Vectors in section", GH_ParamAccess.list);
            pManager.AddGenericParameter("Closest Design Vector", "C", "Design Vector closest to reference vector", GH_ParamAccess.item);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {

            List<int> varyingDimensions = new List<int>();
            List<DesignVector> designSpace = new List<DesignVector>();
            List<double> referenceVectorComponents = new List<double>();

            if (!DA.GetDataList(0, varyingDimensions)) return;
            if (!DA.GetDataList(1, designSpace)) return;
            if (!DA.GetDataList(2, referenceVectorComponents)) return;

            // find vector in DS closest to refVector
            DesignVector referenceVector = new DesignVector(referenceVectorComponents);
            DesignVector closestVector = DesignVector.FindClosestVector(designSpace, referenceVector, out _);

            // determine fixed dimensions from varied - move to DSVcommon?
            List<double> fixedDimensions = new List<double>();
            double numberOfDimensions = designSpace[0].DesignMap.Count;
            for (int dim = 0; dim < numberOfDimensions; dim++)
                if (!varyingDimensions.Contains(dim)) fixedDimensions.Add(dim);

            // find vectors in section, i.e., that have ALL the same values in the Fixed Dimensions as the reference vector
            List<DesignVector> sectionedSpace = new List<DesignVector>();
            foreach (DesignVector dv in designSpace)
            {
                List<bool> inSection = new List<bool>();
                foreach (int fixedDimension in fixedDimensions)
                {
                    if (dv.DesignMap[fixedDimension] == referenceVector.DesignMap[fixedDimension]) inSection.Add(true);
                    else inSection.Add(false);
                }
                // if inSection only contains Trues, then add vector to sectioned space
                if (!inSection.Contains(false)) sectionedSpace.Add(dv);
            }

            DA.SetDataList(0, sectionedSpace);
            DA.SetData(1, closestVector);
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
using System;
using System.Collections;
using System.Collections.Generic;

using Rhino;
using Rhino.Geometry;

using Grasshopper;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;

namespace ACORN
{
    /// <summary>
    /// Generates demo building geometry
    /// </summary>
    public class VisualiseBuilding : GH_Component
    {
        public VisualiseBuilding()
          : base("Visualize Building", "A:VizBldg",
              "Generates demo building geometry",
              "ACORN", "Shells")
        {
        }

        public override Guid ComponentGuid
        {
            get { return new Guid("09b502b0-6794-4f57-87d2-500bdf1c3fd2"); }
        }

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddSurfaceParameter("Segments", "S", "Segment surfaces", GH_ParamAccess.list);
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddSurfaceParameter("Segments", "S", "Segment surfaces", GH_ParamAccess.list);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            List<Surface> segments = new List<Surface>();
            if (!DA.GetDataList(0, segments)) return;

            // for now, nothing happens

            DA.SetDataList(0, segments);
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
    }
}

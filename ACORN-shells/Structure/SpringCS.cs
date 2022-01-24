using System;
using System.Drawing;
using System.Collections.Generic;

using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;
using Rhino.Display;
using Rhino.Geometry;
using System.Linq;
using Rhino.Geometry.Collections;
using Rhino.Collections;
using GH.MiscToolbox.Components.Utilities;
using Grasshopper;

using Karamba.Geometry;
using Karamba.Elements;
using Karamba.CrossSections;
using Karamba.GHopper.Geometry;
using Karamba.GHopper.Elements;
using Karamba.GHopper.CrossSections;

namespace ACORN_shells
{
    /// <summary>
    /// Creates Karamba cross section for springs
    /// </summary>
    public class SpringCS : GH_Component
    {
        public SpringCS()
          : base("Spring Cross Section", "SpringCS",
              "Creates Karamba cross section for springs",
              "ACORN Shells", "  Structure")
        // adding spaces to category names as per https://www.grasshopper3d.com/forum/topics/change-order-of-plugin-sub-category-c 
        {
        }

        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddNumberParameter("Rotational Stiffness", "CrY", "Rotational Stiffness [kNm/rad] about Y axis (along interface)", GH_ParamAccess.item);
        }

        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddGenericParameter("Springs Cross Section", "CS", "Karamba spring cross section", GH_ParamAccess.item);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {

            double CrY = 0;

            if (!DA.GetData(0, ref CrY)) return;

            // define default stiffnesses, other than CrY
            double CtX, CtY, CtZ;
            CtX = CtY = CtZ = 1.0e7;
            double CrX, CrZ;
            CrX = CrZ = 1.0e3;

            // join stiffness values in one array for CS creation
            double[] allStiffnesses = { CtX, CtY, CtZ, CrX, CrY, CrZ };

            // define Karamba cross section 
            CroSec_Spring k3dSection = new CroSec_Spring("", "", "", null, allStiffnesses);
            GH_CrossSection ghSection = new GH_CrossSection (k3dSection);

            DA.SetData(0, ghSection);

        }

        protected override System.Drawing.Bitmap Icon
        {
            get
            {
                return ACORN_shells.Properties.Resources.springCS;
            }
        }

        public override Guid ComponentGuid
        {
            get { return new Guid("776de69c-3c0c-4c05-9013-6c0c53e41680"); }
        }


        public override GH_Exposure Exposure
        {
            get { return GH_Exposure.tertiary; }
        }

    }
}
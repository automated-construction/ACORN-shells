using System;
using System.Collections.Generic;
using System.Linq;

using Rhino.Geometry;
using Rhino.Geometry.Collections;
using Grasshopper;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Types;
using Grasshopper.Kernel.Data;

using Karamba.GHopper.Geometry;
using Karamba.Geometry;
using Karamba.Elements;
using Karamba.Supports;
using Karamba.Materials;
using Karamba.CrossSections;
using Karamba.GHopper.Elements;
using Karamba.GHopper.Supports;
using Karamba.GHopper.Materials;

namespace ACORN_shells
{
    public class DisassembleSupport : GH_Component
    {

        public DisassembleSupport()
          : base("Disassemble Support", "A:DisassembleSupport",
              "Disassembles a Karamba Support",
              "ACORN Shells", "Utilities")
        {
        }

        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddGenericParameter("Support", "S", "Support object to disassemble", GH_ParamAccess.item);
        }

        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddPointParameter("Location", "L", "Support location", GH_ParamAccess.item);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            GH_Support ghSupport = new GH_Support();
            if (!DA.GetData(0, ref ghSupport)) return;

            Support k3dSupport = ghSupport.Value;
            Point3d location = k3dSupport.position.Convert();

            DA.SetData(0, location);
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
            get { return new Guid("f9826c78-cac0-4217-bb7f-dea8492a1db0"); }
        }
    }
}
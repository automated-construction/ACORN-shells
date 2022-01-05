using System;

using Rhino.Geometry;
using Grasshopper.Kernel;

using Karamba.Geometry;
using Karamba.Supports;
using Karamba.GHopper.Geometry;
using Karamba.GHopper.Supports;

namespace ACORN_shells
{
    public class DisassembleSupport : GH_Component
    {

        public DisassembleSupport()
          : base("Disassemble Support", "A:DisassembleSupport",
              "Decomposes Karamba support into position and boundary conditions",
              "ACORN Shells", " Utilities")
        // adding spaces to category names as per https://www.grasshopper3d.com/forum/topics/change-order-of-plugin-sub-category-c 
        {
        }

        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddGenericParameter("Support", "Supp", "Support object to disassemble", GH_ParamAccess.item);
        }

        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddPointParameter("Position", "Pos", "Support position", GH_ParamAccess.item);
            pManager.AddBooleanParameter("Translation X", "Tx", "Translation in global x-direction", GH_ParamAccess.item);
            pManager.AddBooleanParameter("Translation Y", "Ty", "Translation in global y-direction", GH_ParamAccess.item);
            pManager.AddBooleanParameter("Translation Z", "Tz", "Translation in global z-direction", GH_ParamAccess.item);
            pManager.AddBooleanParameter("Rotation X", "Tx", "Rotation about global x-direction", GH_ParamAccess.item);
            pManager.AddBooleanParameter("Rotation Y", "Ty", "Rotation about global y-direction", GH_ParamAccess.item);
            pManager.AddBooleanParameter("Rotation Z", "Tz", "Rotation about global z-direction", GH_ParamAccess.item);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            GH_Support ghSupport = new GH_Support();
            if (!DA.GetData(0, ref ghSupport)) return;

            Support k3dSupport = ghSupport.Value;
            Point3d position = k3dSupport.position.Convert();
            bool Tx = k3dSupport.Condition[0];
            bool Ty = k3dSupport.Condition[1];
            bool Tz = k3dSupport.Condition[2];
            bool Rx = k3dSupport.Condition[3];
            bool Ry = k3dSupport.Condition[4];
            bool Rz = k3dSupport.Condition[5];

            DA.SetData(0, position);
            DA.SetData(1, Tx);
            DA.SetData(2, Ty);
            DA.SetData(3, Tz);
            DA.SetData(4, Rx);
            DA.SetData(5, Ry);
            DA.SetData(6, Rz);
        }

        protected override System.Drawing.Bitmap Icon
        {
            get
            {
                return ACORN_shells.Properties.Resources.disSupp;
            }
        }

        public override Guid ComponentGuid
        {
            get { return new Guid("f9826c78-cac0-4217-bb7f-dea8492a1db0"); }
        }
    }
}
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
          : base("Disassemble Support", "Disassemble",
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
            pManager.AddBooleanParameter("Translation X", "Tx", "Translation in global x-direction", GH_ParamAccess.item);
            pManager.AddBooleanParameter("Translation Y", "Ty", "Translation in global y-direction", GH_ParamAccess.item);
            pManager.AddBooleanParameter("Translation Z", "Tz", "Translation in global z-direction", GH_ParamAccess.item);
            pManager.AddBooleanParameter("Rotation X", "Rx", "Rotation about global x-direction", GH_ParamAccess.item);
            pManager.AddBooleanParameter("Rotation Y", "Ry", "Rotation about global y-direction", GH_ParamAccess.item);
            pManager.AddBooleanParameter("Rotation Z", "Rz", "Rotation about global z-direction", GH_ParamAccess.item);
            pManager.AddPlaneParameter("Orientation", "Ori", "Support local coordinate system (for oriented supports)", GH_ParamAccess.item);
            pManager.AddPointParameter("Position", "Pos", "Support position", GH_ParamAccess.item);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            GH_Support ghSupport = new GH_Support();
            if (!DA.GetData(0, ref ghSupport)) return;

            Support k3dSupport = ghSupport.Value;
            Point3d supportPosition = k3dSupport.position.Convert();
            bool Tx = k3dSupport.Condition[0];
            bool Ty = k3dSupport.Condition[1];
            bool Tz = k3dSupport.Condition[2];
            bool Rx = k3dSupport.Condition[3];
            bool Ry = k3dSupport.Condition[4];
            bool Rz = k3dSupport.Condition[5];

            //get local coordinate system (if oriented supports)
            Plane supportOrientation = Plane.WorldXY;
            if (k3dSupport.hasLocalCoosys) supportOrientation = k3dSupport.local_coosys.Convert();

            DA.SetData(0, Tx);
            DA.SetData(1, Ty);
            DA.SetData(2, Tz);
            DA.SetData(3, Rx);
            DA.SetData(4, Ry);
            DA.SetData(5, Rz);
            DA.SetData(6, supportOrientation);
            DA.SetData(7, supportPosition);
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
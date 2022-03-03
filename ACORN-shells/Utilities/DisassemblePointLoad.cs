using System;

using Rhino.Geometry;
using Grasshopper.Kernel;

using Karamba.Geometry;
using Karamba.Supports;
using Karamba.Loads;
using Karamba.GHopper.Geometry;
using Karamba.GHopper.Supports;
using Karamba.GHopper.Loads;

namespace ACORN_shells
{
    public class DisassemblePointLoad : GH_Component
    {

        public DisassemblePointLoad()
          : base("Disassemble Point Load", "Disassemble",
              "Decomposes Karamba Point Load into position and node index",
              "ACORN Shells", " Utilities")
        // adding spaces to category names as per https://www.grasshopper3d.com/forum/topics/change-order-of-plugin-sub-category-c 
        {
        }

        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddGenericParameter("Point Load", "Load", "Load object to disassemble", GH_ParamAccess.item);
        }

        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddPointParameter("Position", "Pos", "Support position", GH_ParamAccess.item);
            pManager.AddIntegerParameter("Node index", "Ind", "Node index", GH_ParamAccess.item);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            GH_Support ghSupport = new GH_Support();
            GH_Load ghLoad = new GH_Load();
            if (!DA.GetData(0, ref ghLoad)) return;

            Load k3dLoad = ghLoad.Value;
            //if not pointload, return - should throw exception
            if (!k3dLoad.GetType().Equals(typeof(PointLoad))) return;

            PointLoad k3dPointLoad = (PointLoad) k3dLoad;
            Point3d position = k3dPointLoad.position.Convert();
            int index = k3dPointLoad.node_ind;

            DA.SetData(0, position);
            DA.SetData(1, index);
        }

        protected override System.Drawing.Bitmap Icon
        {
            get
            {
                return ACORN_shells.Properties.Resources.disLoad;
            }
        }

        public override Guid ComponentGuid
        {
            get { return new Guid("8dcb7f19-7509-4850-9abd-9220c861166b"); }
        }
    }
}